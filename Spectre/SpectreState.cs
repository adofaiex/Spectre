using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Spectre.API;
using Spectre.Features.Replay;
using static Spectre.Features.Replay.ReplayKeys;
using HarmonyLib;
using MonsterLove.StateMachine;
using SkyHook;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityModManagerNet;
using Object = UnityEngine.Object;

namespace Spectre;

public static class SpectreState
{
    internal static class DataIntegrity
    {
        public static List<string> strings_ = new List<string> { SongName, SceneName, InternalLevelName, JudgeMode };

        public static List<string> bools_ = new List<string> { IsOfficialLevel, SpeedTrailMode, QuickPitched };

        public static List<string> ints_ = new List<string> { FloorHash, TimeHash, SpeedHash, Pitch, StartTile };

        public static List<string> doubles_ = new List<string> { Bpm, PlaybackSpeed, SpeedTrail };

        internal static bool Check()
        {
            foreach (string item in strings_)
            {
                if (!data.strings.ContainsKey(item))
                {
                    Debug.Log(("Missing String:" + item));
                    return true;
                }
            }
            foreach (string item2 in bools_)
            {
                if (!data.bools.ContainsKey(item2))
                {
                    Debug.Log(("Missing Bool:" + item2));
                    return true;
                }
            }
            foreach (string item3 in ints_)
            {
                if (!data.ints.ContainsKey(item3))
                {
                    Debug.Log(("Missing Int:" + item3));
                    return true;
                }
            }
            foreach (string item4 in doubles_)
            {
                if (!data.doubles.ContainsKey(item4))
                {
                    Debug.Log(("Missing Double:" + item4));
                    return true;
                }
            }
            return false;
        }
    }

    internal class PlayActions
    {
        internal static void SwitchToPlayMode()
        {
            PlayMode = true;
            RecordMode = false;
            is_playing = false;
            is_recording = false;
            HitLocked = false;
            PendingSave = false;
            LateSaveMode = false;
            PlayIndex = 0;
            KeyEventIndex = 0;
            AllAutoHits = true;
            IgnoreMarkFail = false;
        }

        internal static void SwitchToRecordMode()
        {
            HitLocked = false;
            PlayMode = false;
            RecordMode = true;
            is_playing = false;
            is_recording = false;
            PendingSave = false;
            LateSaveMode = false;
            PlayIndex = 0;
            KeyEventIndex = 0;
            AllAutoHits = true;
            IgnoreMarkFail = false;
            IsFullRun = false;
        }

        internal static void StopPlaying() => ReplayPlayer.Stop();

        internal static void StartPlaying() => ReplayPlayer.Start();

        internal static void StartRecording() => ReplayRecorder.Start();

        internal static void StopRecording() => ReplayRecorder.Stop();
    }

    internal static bool ApiBlockPlaying = false;

    internal static bool is_playing = false;

    internal static bool is_recording = false;

    internal static bool? CachedNoFail;

    internal static void SyncEditorNoFailButton()
    {
        if (scnEditor.instance == null) return;
        var btn = scnEditor.instance.buttonNoFail;
        if (btn == null) return;
        var img = btn.GetComponent<UnityEngine.UI.Image>();
        img.color = data.bools[IfNoFail] ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
    }

    internal static bool RecordMode = true;

    internal static bool PlayMode = false;

    internal static bool HitLocked = false;

    internal static bool IgnoreMarkFail = false;

    internal static bool UseAsyncInput = false;

    internal static bool UseLegacyAsyncRecorder = false;

    internal static ulong RecordStartTick = 0uL;

    internal static bool PendingSave = false;

    internal static bool LateSaveMode = false;

    internal static int[] HitMarginCounts = new int[Enum.GetValues(typeof(HitMargin)).Cast<int>().Max() + 1];

    internal static scrFloor CurrentHitFloor = new scrFloor();

    internal static LegacyReplayData data;

    internal static int PlayIndex = 0;

    internal static int KeyEventIndex = 0;

    internal static bool AllAutoHits = true;

    internal static bool IsFullRun = false;
    internal static bool HasKeybdSound = false;

    internal static HitMarginLimit StoredHitMarginLimit = (HitMarginLimit)0;

    internal static bool DebugMode = false;

    internal static bool debug_skip_Verification = false;

    internal static bool debug_no_encryption = false;

    internal static ReplayManager.ReplaySaveFormat debug_save_format = ReplayManager.ReplaySaveFormat.New;

    internal static bool debug_hide_text = false;

    internal static bool debug_use_old_hit = false;

    internal static KeyCode save_button = (KeyCode)290;

    internal static byte[] PrevKeyState = new byte[256];

    internal static byte[] CurrKeyState = new byte[256];

    private static readonly ushort[] extendedKeys = new ushort[6] { 165, 163, 38, 40, 37, 39 };

    private static readonly ushort[] BlockKeys = new ushort[4] { 16, 17, 18, 27 };

    internal static int GetKeyboardState(byte[] pbKeyState)
        => KeyboardSimulation.GetKeyboardState(pbKeyState);

    internal static PriorityQueue<SkyHookEvent, ulong> SortedKeyQueue;

    private static MethodInfo EnqueueMethod = null;

    internal static bool IsExtendedKey(ushort keyCode)
    {
        ushort[] array = extendedKeys;
        foreach (ushort num in array)
        {
            if (num == keyCode)
            {
                return true;
            }
        }
        return false;
    }

    internal static bool IsBlockedKey(ushort keyCode)
    {
        ushort[] blockKeys = BlockKeys;
        foreach (ushort num in blockKeys)
        {
            if (num == keyCode)
            {
                return true;
            }
        }
        return false;
    }

    internal static float GetAngle()
    {
        float num = (float)(scrController.instance.chosenPlanet.angle - scrController.instance.chosenPlanet.targetExitAngle);
        if (!scrController.instance.playerOne.planetarySystem.isCW)
        {
            num *= -1f;
        }
        return num;
    }

    internal static bool SaveFile(bool without_sound = false, bool retry = false)
    {
        if (data == null)
        {
            return true;
        }
        string text = "";
        bool flag = true;
        if (retry)
        {
            data.strings[FileName] = CreateFileName(safe_mode: true);
            data.strings[KeybdSoundFileName] = data.strings[FileName] + ".wav";
        }
        string text3 = Path.Combine(path2: (!data.strings.ContainsKey(FileName)) ? CreateFileName() : data.strings[FileName], path1: Options.SavePath);
        if (ReplayManager.SaveReplay(data, text3, debug_save_format, debug_no_encryption))
        {
            text = text + LocalizationManager.GetLocalizedText("note.save_success") + "\n";
        }
        else
        {
            text = text + LocalizationManager.GetLocalizedText("note.save_fail") + text3 + "\n";
            flag = false;
        }
        if (Options.Backup)
        {
            string path = "backup";
            string filePath = Path.Combine(Main.mod.Path, path);
            ReplayManager.SaveReplay(data, filePath, debug_save_format, debug_no_encryption);
        }
        if (!without_sound && data.strings.ContainsKey(KeybdSoundFileName))
        {
            string text4 = Path.Combine(Options.SavePath, "tmp.wav");
            string text5 = Path.Combine(Options.SavePath, data.strings[KeybdSoundFileName]);
            try
            {
                if (File.Exists(text4))
                {
                    if (File.Exists(text5))
                    {
                        File.Delete(text5);
                    }
                    File.Move(text4, text5);
                }
            }
            catch (Exception ex)
            {
                Debug.Log(ex);
            }
            if (!File.Exists(text5))
            {
                text = text + LocalizationManager.GetLocalizedText("note.keybdsoundfile_save_fail") + text5 + "\n";
            }
        }
        TriggerMessage(text);
        if (!flag && !retry)
        {
            return SaveFile(without_sound: false, retry: true);
        }
        return flag;
    }

    internal enum NotifType { Info, Warning }

    internal static void TriggerMessage(string message, float dur = 3f, NotifType type = NotifType.Info)
    {
        if (debug_hide_text) return;
        try
        {
            var n = Notification.instance;
            n.text.text = message.TrimEnd('\n');
            n.icon.enabled = true;
            n.button.onClick.RemoveAllListeners();
            switch (type)
            {
                case NotifType.Warning:
                    n.icon.sprite = n.warningIcon;
                    n.icon.color = new Color(1f, 0.2f, 0.2f);
                    break;
                default:
                    n.icon.sprite = n.completeIcon;
                    n.icon.color = new Color(0.2f, 0.7215686f, 0.3921569f);
                    break;
            }
            var graphics = n.bar.GetComponentsInChildren<Graphic>(true);
            var states = graphics.Select(g => g.raycastTarget).ToArray();
            foreach (var g in graphics) g.raycastTarget = false;
            n.StartCoroutine(RestoreGraphics(graphics, states, dur));
            var m = PatchManager.GetMethodInfo(typeof(Notification), "SetupNotification",
                [typeof(float), typeof(float), typeof(bool)]);
            m.Invoke(n, [dur, 1f, true]);
        }
        catch (Exception ex)
        {
            Debug.Log(ex);
        }
    }

    private static System.Collections.IEnumerator RestoreGraphics(Graphic[] graphics, bool[] states, float dur)
    {
        yield return new WaitForSecondsRealtime(0.5f + 0.5f + dur + 0.3f);
        for (int i = 0; i < graphics.Length; i++)
            if (graphics[i] != null) graphics[i].raycastTarget = states[i];
    }

    public static bool ReplayHit(scrPlayer player, HitContext hitdata)
        => ReplayPlayer.ReplayHit(player, hitdata);

    internal static void Play_keybd_event(bool flag)
        => ReplayPlayer.PlayKeyboardEvent(flag);

    internal static void Record_kbdstate(bool forceSync = false)
        => ReplayRecorder.RecordKeyboardState(forceSync);

    private static UnityEngine.Events.UnityAction<SkyHook.SkyHookEvent> _ychListener;

    public static void SetGetKeyAsyncEnabled(bool enabled)
    {
        if (enabled != UseAsyncInput)
        {
            EnqueueMethod = PatchManager.GetMethodInfo(typeof(PriorityQueue<SkyHookEvent, ulong>), "Enqueue");
            if (enabled)
            {
                PatchManager.RegisterManualPrefix(EnqueueMethod, PatchManager.GetMethodInfo(typeof(Patch_CaptureEnqueue), "Prefix"));
                UseAsyncInput = true;
                if (_ychListener == null)
                    _ychListener = ReplayRecorderYch.OnSkyHookEvent;
                SkyHook.SkyHookManager.KeyUpdated.AddListener(_ychListener);
            }
            else
            {
                PatchManager.UnpatchManualPrefix(EnqueueMethod);
                UseAsyncInput = false;
                if (_ychListener != null)
                    SkyHook.SkyHookManager.KeyUpdated.RemoveListener(_ychListener);
            }
        }
    }

    internal static void StopAll()
    {
        if (is_playing)
        {
            TriggerMessage(LocalizationManager.GetLocalizedText("note.stop_playing"));
        }
        if (is_recording)
        {
            TriggerMessage(LocalizationManager.GetLocalizedText("note.stop_recording"));
        }
        PlayActions.StopPlaying();
        PlayActions.StopRecording();
    }

    public static double ComputePercentXacc()
    {
        var tracker = scrController.instance?.playerOne?.marginTracker;
        if (tracker == null || tracker.hitMargins == null || tracker.hitMargins.Count == 0)
            return 0.0;
        return (1.0 * (double)HitMarginCounts[3] + 1.0 * (double)HitMarginCounts[10] + 0.75 * (double)HitMarginCounts[2] + 0.75 * (double)HitMarginCounts[4] + 0.4 * (double)HitMarginCounts[1] + 0.4 * (double)HitMarginCounts[5] + 0.2 * (double)HitMarginCounts[0] + 0.2 * (double)HitMarginCounts[6]) / (double)tracker.hitMargins.Count;
    }

    public static double ComputePercentComplete(int curr_floor)
    {
        if (scrController.instance != null && ADOBase.lm != null)
        {
            return (float)(curr_floor + 1) / (float)ADOBase.lm.listFloors.Count;
        }
        return 0.0;
    }

    internal static string CreateFileName(bool safe_mode = false)
    {
        string text = "";
        if (safe_mode)
        {
            return DateTime.Now.ToString("yyyyMMddHHmmss") + "_";
        }
        if (data.strings.ContainsKey(ArtistName) && data.strings.ContainsKey(SongName))
        {
            text = data.strings[ArtistName] + "_" + data.strings[SongName] + text;
        }
        text = Regex.Replace(text, "<[^>]*>", string.Empty);
        text = DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + text;
        try
        {
            text = ((data.ints[EndTile] != ADOBase.lm.listFloors.Count - 1 || data.ints[StartTile] != 0) ? (text + "_" + (100.0 * ComputePercentComplete(data.ints[StartTile])).ToString("F0") + "-" + (100.0 * ComputePercentComplete(data.ints[EndTile])).ToString("F0")) : (text + "_" + (100.0 * ComputePercentXacc()).ToString("F2")));
        }
        catch
        {
        }
        return ReplayManager.RemoveInvalidPathChars(text);
    }
}
