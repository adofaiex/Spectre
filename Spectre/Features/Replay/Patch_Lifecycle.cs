using System;
using System.Globalization;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using Spectre.API;
using static Spectre.SpectreState;
using static Spectre.Features.Replay.ReplayKeys;

namespace Spectre.Features.Replay;

// ——— scrUIController.WipeToBlack ——————————————————————————
[HarmonyPatch(typeof(scrUIController), "WipeToBlack")]
internal class Patch_WipeToBlack
{
    [HarmonyPrefix]
    internal static void Prefix() => StopAll();
}

// ——— scnEditor.SwitchToEditMode ———————————————----———————
[HarmonyPatch(typeof(scnEditor), "SwitchToEditMode")]
internal class Patch_SwitchToEditMode
{
    [HarmonyPrefix]
    internal static void Prefix(scnEditor __instance) => StopAll();
}

// ——— scrController.Restart ——————————————————————————————————
[HarmonyPatch(typeof(scrController), "Restart")]
internal class Patch_Restart
{
    [HarmonyPrefix]
    internal static void Prefix(scrController __instance) => StopAll();
}

// ——— scrController.QuitToMainMenu ——————————————————————————
[HarmonyPatch(typeof(scrController), "QuitToMainMenu")]
internal class Patch_QuitToMainMenu
{
    [HarmonyPrefix]
    internal static void Prefix(scrController __instance) => StopAll();
}

// ——— scrController.Start_Rewind ————————————————————————————
[HarmonyPatch(typeof(scrController), "Start_Rewind")]
internal class Patch_StartRewind
{
    [HarmonyPrefix]
    internal static void Prefix()
    {
        if (Options.KeybdSoundRecordActive && RecordMode)
        {
            Debug.Log(Options.MicrophoneDeviceName);
            Debug.Log(AudioRecorder.Instance.ValidateDevice(Options.MicrophoneDeviceName));
            if (AudioRecorder.Instance.ValidateDevice(Options.MicrophoneDeviceName))
                AudioRecorder.Instance.startMicrophone(Options.MicrophoneDeviceName);
        }
    }

    [HarmonyPostfix]
    internal static void Postfix()
    {
        PlayActions.StopPlaying();
        PlayActions.StopRecording();
        HitLocked = false;
        Options.checklang();

        Scene scene = SceneManager.GetActiveScene();
        Debug.Log(scene.name);

        switch (scene.name)
        {
            case "scnLevelSelect":
            case "scnLoading":
            case "scnTaroMenu0":
                if (!LateSaveMode) break;
                PlayActions.StopRecording();
                if (Options.DontSaveWhenMiss)
                {
                    if (HitMarginCounts[8] + HitMarginCounts[9] == 0) SaveFile();
                }
                else SaveFile();
                LateSaveMode = false;
                break;

            default:
                if (scene.name == "scnCLS") break;

                if (PlayMode)
                {
                    TryStartPlayback();
                    break;
                }

                if (RecordMode) TryStartRecording();
                break;
        }
    }

    private static void TryStartPlayback()
    {
        try
        {
            if (!DebugMode || !debug_skip_Verification)
            {
                if (DataIntegrity.Check())
                {
                    TriggerMessage(LocalizationManager.GetLocalizedText("note.play_error7"), 3f, NotifType.Warning);
                    return;
                }
                if (SpectreAPI.BlockSources != null && SpectreAPI.BlockSources.Count != 0)
                {
                    string text = "";
                    foreach (var kv in SpectreAPI.BlockSources)
                        text += kv.Key + ":" + kv.Value + "\n";
                    TriggerMessage(text, 3f, NotifType.Warning);
                    return;
                }
                if (scrPlayerManager.instance.players.Count() != 1)
                {
                TriggerMessage(LocalizationManager.GetLocalizedText("note.play_error8"), 3f, NotifType.Warning);
                    return;
                }
                if (data.bools[IsOfficialLevel] != ADOBase.isOfficialLevel)
                {
                    TriggerMessage(LocalizationManager.GetLocalizedText("note.play_error1") + data.strings[SongName], 3f, NotifType.Warning);
                    return;
                }
                if (data.bools[IsOfficialLevel])
                {
                    if (data.strings[SceneName] != SceneManager.GetActiveScene().name)
                    {
                        TriggerMessage(LocalizationManager.GetLocalizedText("note.play_error1") + data.strings[SongName], 3f, NotifType.Warning);
                        return;
                    }
                    if (!string.IsNullOrEmpty(data.strings[InternalLevelName])
                        && data.strings[InternalLevelName] != GCS.internalLevelName)
                    {
                        TriggerMessage(LocalizationManager.GetLocalizedText("note.play_error1") + data.strings[SongName], 3f, NotifType.Warning);
                        return;
                    }
                }
                int floorHash = ADOBase.isOfficialLevel
                    ? 0
                    : (scnGame.instance.levelData.isOldLevel
                        ? scnGame.instance.levelData.pathData.GetHashCode()
                        : string.Join("", scnGame.instance.levelData.angleData
                            .Select(x => x >= 0f ? x % 360f : 360f - -x % 360f)).GetHashCode());
                if (floorHash != data.ints[FloorHash])
                {
                    TriggerMessage(LocalizationManager.GetLocalizedText("note.play_error2"), 3f, NotifType.Warning);
                    return;
                }
                if (!ADOBase.isOfficialLevel
                    && (data.ints[Pitch] != scnGame.instance.levelData.pitch
                        || data.doubles[Bpm] != (double)scnGame.instance.levelData.bpm))
                {
                    TriggerMessage(LocalizationManager.GetLocalizedText("note.play_error2"), 3f, NotifType.Warning);
                    return;
                }
                if (GCS.difficulty.ToString() != data.strings[JudgeMode])
                {
                    TriggerMessage(LocalizationManager.GetLocalizedText("note.play_error3") + data.strings[JudgeMode], 3f, NotifType.Warning);
                    return;
                }
                if (!IsFullRun && GCS.checkpointNum != data.ints[StartTile])
                {
                    TriggerMessage(LocalizationManager.GetLocalizedText("note.play_error4") + data.ints[StartTile], 3f, NotifType.Warning);
                    return;
                }
                if (data.bools[SpeedTrailMode] || data.bools[QuickPitched])
                {
                    if (data.strings[SceneName] != SceneManager.GetActiveScene().name)
                    {
                        TriggerMessage(LocalizationManager.GetLocalizedText("note.play_error6"), 3f, NotifType.Warning);
                        return;
                    }
                }
                double expectedSpeed = 1.0;
                double actualSpeed = 1.0;
                if (SceneManager.GetActiveScene().name == "scnEditor")
                {
                    actualSpeed = GCS.editorQuickPitchedPlaying ? scnEditor.instance.playbackSpeed : 1f;
                    expectedSpeed = data.bools[QuickPitched] ? data.doubles[PlaybackSpeed] : 1.0;
                }
                else
                {
                    actualSpeed = (GCS.speedTrialMode || GCS.practiceMode) ? GCS.currentSpeedTrial : 1f;
                    expectedSpeed = data.bools[SpeedTrailMode] ? data.doubles[SpeedTrail] : 1.0;
                }
                if (actualSpeed != expectedSpeed)
                {
                    TriggerMessage(LocalizationManager.GetLocalizedText("note.play_error5") + expectedSpeed, 3f, NotifType.Warning);
                    return;
                }
            }
            else Debug.Log("Debug模式跳过校验");

            StoredHitMarginLimit = (HitMarginLimit)0;
            if (data.strings.ContainsKey(HitMarginLimitKey)
                && Enum.TryParse<HitMarginLimit>(data.strings[HitMarginLimitKey], out var result))
                StoredHitMarginLimit = result;
            GCS.hitMarginLimit = StoredHitMarginLimit;
            scrController.instance.noFail = true;

            IsFullRun = data.ints.ContainsKey(StartTile) && data.ints.ContainsKey(EndTile)
                && data.ints[StartTile] == 0
                && data.ints[EndTile] == ADOBase.lm.listFloors.Count - 1;

            if (IsFullRun && GCS.checkpointNum != data.ints[StartTile])
                ReplayPlayer.FastForward(GCS.checkpointNum);

            PlayActions.StartPlaying();
            TriggerMessage(LocalizationManager.GetLocalizedText("note.start_playing"));
        }
        catch (Exception ex)
        {
            Debug.Log("load error: " + ex);
            TriggerMessage(LocalizationManager.GetLocalizedText("note.play_error7"), 3f, NotifType.Warning);
        }
    }

    private static void TryStartRecording()
    {
        try
        {
            if (data == null) data = new LegacyReplayData();
            if (LateSaveMode)
            {
                if (Options.DontSaveWhenMiss)
                {
                    if (HitMarginCounts[8] + HitMarginCounts[9] == 0) SaveFile();
                }
                else SaveFile();
                LateSaveMode = false;
            }
            if (scrPlayerManager.instance.players.Count() != 1)
            {
                TriggerMessage(LocalizationManager.GetLocalizedText("note.play_error8"), 3f, NotifType.Warning);
                return;
            }
            data.reset();
            data.ints[StartTile] = GCS.checkpointNum;
            data.strings[SceneName] = SceneManager.GetActiveScene().name;
            data.strings[JudgeMode] = GCS.difficulty.ToString();
            data.doubles[SpeedTrail] = GCS.currentSpeedTrial;
            data.bools[SpeedTrailMode] = GCS.speedTrialMode || GCS.practiceMode;
            data.strings[InternalLevelName] = GCS.internalLevelName ?? "";
            data.bools[IsOfficialLevel] = ADOBase.isOfficialLevel;
            data.bools[QuickPitched] = GCS.editorQuickPitchedPlaying;
            data.bools[IfNoFail] = GCS.useNoFail;
            data.ints[FloorHash] = ADOBase.isOfficialLevel
                ? 0
                : (scnGame.instance.levelData.isOldLevel
                    ? scnGame.instance.levelData.pathData.GetHashCode()
                    : string.Join("", scnGame.instance.levelData.angleData
                        .Select(x => x >= 0f ? x % 360f : 360f - -x % 360f)).GetHashCode());
            var speeds = new System.Collections.Generic.List<double>();
            var times = new System.Collections.Generic.List<double>();
            for (int i = 0; i < ADOBase.lm.listFloors.Count; i++)
            {
                if (i > 0) times.Add(ADOBase.lm.listFloors[i].entryTime - ADOBase.lm.listFloors[i - 1].entryTime);
                speeds.Add(ADOBase.lm.listFloors[i].speed * (ADOBase.lm.listFloors[i].isCCW ? -1f : 1f));
            }
            data.ints[SpeedHash] = string.Join("", speeds).GetHashCode();
            data.ints[TimeHash] = string.Join("", times.Select(d => d.ToString("F4", System.Globalization.CultureInfo.InvariantCulture))).GetHashCode();
            data.strings[LevelPath] = "";
            if (ADOBase.isOfficialLevel)
            {
                data.strings[ArtistName] = "OfficialLevel";
                string songName = GCS.internalLevelName ?? "";
                if (data.strings[SceneName] != "scnGame" && data.strings[SceneName] != "scnEditor")
                    songName = data.strings[SceneName];
                data.strings[SongName] = songName;
                data.strings[LevelPath] = "OfficialLevel " + songName;
                data.ints[Pitch] = 0;
                data.doubles[Bpm] = 0.0;
            }
            else
            {
                data.strings[ArtistName] = (scnGame.instance.levelData.artist ?? "").Trim();
                string song = (scnGame.instance.levelData.song ?? "").Trim();
                if (string.IsNullOrEmpty(song))
                {
                    song = scnGame.instance.levelData.songFilename;
                    int dot = song.IndexOf('.');
                    if (dot != -1) song = song.Substring(0, dot);
                }
                data.strings[SongName] = song;
                data.strings[LevelPath] = scnGame.instance.levelPath ?? "";
                data.ints[Pitch] = scnGame.instance.levelData.pitch;
                data.doubles[Bpm] = scnGame.instance.levelData.bpm;
            }
            data.doubles[PlaybackSpeed] = SceneManager.GetActiveScene().name == "scnEditor"
                ? scnEditor.instance.playbackSpeed : 1.0;
            data.ints[AudioBufferSize] = Persistence.audioBufferSize;
            data.strings[HoldBehaviorKey] = Persistence.holdBehavior.ToString();
            data.strings[HitMarginLimitKey] = Persistence.hitMarginLimit.ToString();
            data.strings[LoadedMods] = string.Join("\n", Main.GetLoadedMods().Select(m => Main.mod2string(m)));
            data.strings[DeviceID] = SystemInfo.deviceUniqueIdentifier;
            data.strings[ModVersion] = Main.mod.Info.Version;
            PlayActions.StartRecording();
            TriggerMessage(LocalizationManager.GetLocalizedText("note.start_recording"));
        }
        catch (Exception ex)
        {
            Debug.Log("start recording error: " + ex);
        }
    }
}
