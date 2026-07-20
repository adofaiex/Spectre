using System;
using System.Collections.Generic;
using SkyHook;
using UnityEngine;
using static Spectre.SpectreState;
using static Spectre.Features.Replay.ReplayKeys;

namespace Spectre.Features.Replay;

internal static class ReplayRecorder
{
    internal static void Start()
    {
        if (!is_recording)
        {
            Stop();
            PlayIndex = 0;
            KeyEventIndex = 0;
            is_recording = true;
            is_playing = false;
            PendingSave = true;
            HitLocked = false;
            AllAutoHits = true;
            LateSaveMode = false;
            IgnoreMarkFail = false;
            if (Options.GetDataFromAsyncInput)
            {
                RecordStartTick = (ulong)DateTime.Now.Ticks;
                var queueField = PatchManager.GetFieldInfo(typeof(scrController), "sortedKeyQueue");
                if (queueField != null && scrController.instance != null)
                    SortedKeyQueue = queueField.GetValue(scrController.instance) as PriorityQueue<SkyHookEvent, ulong>;
                Debug.Log("get sortedKeyQueue HERE");
            }
        }
    }

    internal static void Stop()
    {
        if (!is_recording)
        {
            return;
        }
        is_recording = false;
        IgnoreMarkFail = false;
        for (int i = 0; i < HitMarginCounts.Length; i++)
        {
            HitMarginCounts[i] = scrController.instance.playerOne.marginTracker.hitMarginsCount[i];
        }
        data.ints[EndTile] = scrController.instance.currentSeqID;
        data.strings[JudgmentList] = "[" + string.Join(",", HitMarginCounts) + "]";
        data.doubles[PercentXacc] = ComputePercentXacc();
        data.ints[MaximumUsedKeys] = ADOBase.controller.maximumUsedKeys;
        data.strings[FileName] = CreateFileName();
        if (Options.KeybdSoundRecordActive)
        {
            data.strings[KeybdSoundFileName] = data.strings[FileName] + ".wav";
        }
        if (AudioRecorder.Instance.IsRecording)
        {
            data.strings[KeybdSoundHash] = AudioRecorder.Instance.StopRecording();
        }
        if (!LateSaveMode)
        {
            return;
        }
        if (Options.DontSaveWhenMiss)
        {
            if (HitMarginCounts[8] + HitMarginCounts[9] == 0)
            {
                SaveFile();
            }
        }
        else
        {
            SaveFile();
        }
        LateSaveMode = false;
    }

    internal static void RecordKeyboardState(bool forceSync = false)
    {
        if (!forceSync && UseAsyncInput)
        {
            if (!UseLegacyAsyncRecorder) return;
            if (ADOBase.conductor == null || ADOBase.conductor.songposition_minusi > 0.0)
            {
                return;
            }
        }
        KeyboardSimulation.GetKeyboardState(CurrKeyState);
        for (int i = 1; i < 256; i++)
        {
            if ((PrevKeyState[i] & 0x80) == 0 && (CurrKeyState[i] & 0x80) != 0)
            {
                data.KeyEvent_list.Add(new KeyEvent
                {
                    KeyCode = (ushort)i,
                    IsPressed = true,
                    SongPosition = ADOBase.conductor.songposition_minusi
                });
            }
            else if ((PrevKeyState[i] & 0x80) != 0 && (CurrKeyState[i] & 0x80) == 0)
            {
                data.KeyEvent_list.Add(new KeyEvent
                {
                    KeyCode = (ushort)i,
                    IsPressed = false,
                    SongPosition = ADOBase.conductor.songposition_minusi
                });
            }
            PrevKeyState[i] = CurrKeyState[i];
        }
    }
}
