using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using static Spectre.SpectreState;
using static Spectre.Features.Replay.ReplayKeys;

namespace Spectre.Features.Replay;

// ——— scrController.Won_Update ———————————————————————————————
[HarmonyPatch(typeof(scrController), "Won_Update")]
internal class Patch_Won_Update
{
    [HarmonyPostfix]
    internal static void Postfix(scrController __instance)
    {
        if (__instance == null) return;
        if (is_recording && Application.isFocused) Record_kbdstate(forceSync: true);
        if (is_playing) Play_keybd_event(flag: true);
    }
}

// ——— scrController.PlayerControl_Update / Countdown_Update / Checkpoint_Update ——
[HarmonyPatch]
internal class Patch_PlayerControlUpdate
{
    private static System.Collections.Generic.IEnumerable<MethodBase> TargetMethods()
    {
        yield return PatchManager.GetMethodInfo(typeof(scrController), "PlayerControl_Update");
        yield return PatchManager.GetMethodInfo(typeof(scrController), "Countdown_Update");
        yield return PatchManager.GetMethodInfo(typeof(scrController), "Checkpoint_Update");
    }

    [HarmonyPostfix]
    internal static void Postfix(scrController __instance)
    {
        if (__instance == (UnityEngine.Object)null) return;

        // Start keyboard sound recording
        if (is_recording && Options.KeybdSoundRecordActive
            && !AudioRecorder.Instance.IsRecording
            && !data.doubles.ContainsKey(KeybdSoundStartTick))
        {
            string filePath = Path.Combine(Options.SavePath, "tmp.wav");
            AudioRecorder.Instance.StartRecording(filePath, Options.MicrophoneDeviceName);
            data.doubles[KeybdSoundStartTick] = ADOBase.conductor.songposition_minusi;
        }

        if (is_recording && Application.isFocused) Record_kbdstate();
        if (!is_playing) return;

        if (PlayIndex < 0) { KeyEventIndex = 0; PlayIndex = 0; }

        if (data.HitContext_list.Count == 0)
        {
            PlayActions.StopPlaying();
            TriggerMessage(LocalizationManager.GetLocalizedText("note.data_empty"));
            return;
        }

        if (PlayIndex < data.HitContext_list.Count)
        {
            var hc = data.HitContext_list[PlayIndex];
            if (__instance.currFloor.seqID != hc.CurrentFloorID
                || __instance.curFreeRoamSection > hc.curFreeRoamSection)
            {
                if (IsFullRun)
                {
                    while (PlayIndex < data.HitContext_list.Count
                        && (data.HitContext_list[PlayIndex].CurrentFloorID < __instance.currFloor.seqID
                            || data.HitContext_list[PlayIndex].curFreeRoamSection < __instance.curFreeRoamSection))
                        PlayIndex++;
                    if (PlayIndex >= data.HitContext_list.Count)
                    {
                        PlayActions.StopPlaying();
                        TriggerMessage(LocalizationManager.GetLocalizedText("note.data_empty"));
                        return;
                    }
                    hc = data.HitContext_list[PlayIndex];
                }
                else
                {
                    PlayActions.StopPlaying();
                    TriggerMessage(LocalizationManager.GetLocalizedText("note.play_error"), 4f, NotifType.Warning);
                    Debug.Log($"log{PlayIndex}: {__instance.currFloor.seqID} != {hc.CurrentFloorID}");
                    return;
                }
            }

            float angle = GetAngle();
            while (__instance.currFloor.seqID == hc.CurrentFloorID
                && (double)angle >= hc.CurrAngle
                && __instance.curFreeRoamSection == hc.curFreeRoamSection)
            {
                ReplayHit(scrController.instance.playerOne, hc);
                PlayIndex++;
                if (PlayIndex >= data.HitContext_list.Count) break;
                angle = GetAngle();
                hc = data.HitContext_list[PlayIndex];
                if (__instance.currFloor.seqID != hc.CurrentFloorID
                    || __instance.curFreeRoamSection > hc.curFreeRoamSection)
                {
                    if (IsFullRun) break;
                    PlayActions.StopPlaying();
                    TriggerMessage(LocalizationManager.GetLocalizedText("note.play_error"), 4f, NotifType.Warning);
                    Debug.Log($"log{PlayIndex}: {__instance.currFloor.seqID} != {hc.CurrentFloorID}");
                    return;
                }
            }
        }

        Play_keybd_event(flag: false);

        if (PlayIndex >= data.HitContext_list.Count
            && KeyEventIndex >= data.KeyEvent_list.Count)
        {
            KeyEventIndex = 0;
            PlayIndex = 0;
            PlayActions.StopPlaying();
            TriggerMessage(LocalizationManager.GetLocalizedText("note.stop_playing"));
            return;
        }

        // Keyboard sound playback sync
        if (!HasKeybdSound)
            return;

        if (!data.doubles.ContainsKey(KeybdSoundStartTick))
            return;

        float num = ((float)(ADOBase.conductor.songposition_minusi
            - data.doubles[KeybdSoundStartTick])
            + (float)scrConductor.currentPreset.inputOffset / 1000f
            + (float)Options.MicrophoneOffset / 1000f);

        if (num >= 0f && (double)num < WavLoader.loaded_clip.length - 0.1)
        {
            if (Math.Abs(WavLoader.musicSource.time - num) > 0.02f)
                WavLoader.musicSource.time = num;
            WavLoader.musicSource.pitch = ADOBase.conductor.song.pitch;
            if (!WavLoader.musicSource.isPlaying)
                WavLoader.Play_keybdsound(WavLoader.loaded_clip, (float)Options.KeybdSoundVolume / 10f);
        }
        else if (WavLoader.musicSource.isPlaying)
            WavLoader.Stop_keybdsound();
    }
}
