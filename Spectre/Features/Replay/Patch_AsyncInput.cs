using System;
using System.Collections.Generic;
using HarmonyLib;
using SkyHook;
using UnityEngine;
using static Spectre.SpectreState;

namespace Spectre.Features.Replay;

// ——— PriorityQueue<SkyHookEvent, ulong>.Enqueue ————————————
[HarmonyPatch(typeof(PriorityQueue<SkyHookEvent, ulong>), "Enqueue")]
internal class Patch_CaptureEnqueue
{
    [HarmonyPrefix]
    internal static void Prefix(PriorityQueue<SkyHookEvent, ulong> __instance, SkyHookEvent element, ulong priority)
    {
        if (!UseLegacyAsyncRecorder) return;
        if (!UseAsyncInput) return;
        if (__instance != SortedKeyQueue) return;
        if (!is_recording) return;
        if (AsyncInputManager.prevFrameTick > AsyncInputManager.currFrameTick) return;
        if (AsyncInputManager.prevFrameTick < RecordStartTick) return;
        if (priority < AsyncInputManager.prevFrameTick) return;

        double dsp = ((long)priority + (long)(AudioSettings.dspTime * 10000000.0) - DateTime.Now.Ticks)
                     / 10000000.0;
        var c = ADOBase.conductor;
        double songPos = c.song.pitch * (dsp - c.dspTimeSong - scrConductor.calibration_i)
                         - c.addoffset;
        data.KeyEvent_list.Add(new KeyEvent
        {
            KeyCode = element.Key,
            IsPressed = element.Type == SkyHook.EventType.KeyPressed,
            SongPosition = songPos
        });
    }
}

// ——— scrController.UpdateInput (consume YCH async key queue) ——————
[HarmonyPatch(typeof(scrController), "UpdateInput")]
internal class Patch_UpdateInput_ConsumeYch
{
    private static void Prefix()
    {
        try
        {
            ReplayRecorderYch.Consume();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}
