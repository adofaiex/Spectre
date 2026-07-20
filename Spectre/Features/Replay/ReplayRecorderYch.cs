using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using SkyHook;
using UnityEngine;
using static Spectre.SpectreState;

namespace Spectre.Features.Replay;

internal static class ReplayRecorderYch
{
    private static readonly ConcurrentQueue<SkyHookEvent> KeyQueue = new ConcurrentQueue<SkyHookEvent>();

    internal static void OnSkyHookEvent(SkyHookEvent keyEvent)
    {
        if (keyEvent.Label == KeyLabel.MouseLeft
            || keyEvent.Label == KeyLabel.MouseMiddle
            || keyEvent.Label == KeyLabel.MouseRight
            || keyEvent.Label == KeyLabel.MouseX1
            || keyEvent.Label == KeyLabel.MouseX2)
        {
            return;
        }
        KeyQueue.Enqueue(keyEvent);
    }

    internal static void FlushQueue()
    {
        while (KeyQueue.TryDequeue(out _)) { }
    }

    internal static void Consume()
    {
        if (UseLegacyAsyncRecorder || !is_recording || !Options.GetDataFromAsyncInput
            || !Application.isFocused || !AsyncInputManager.isActive)
        {
            FlushQueue();
            return;
        }

        var sorted = new PriorityQueue<SkyHookEvent, long>();
        while (KeyQueue.TryDequeue(out var key))
            sorted.Enqueue(key, (long)key.GetTimeInTicks());
        while (sorted.TryDequeue(out var key, out var ticks))
        {
            double dsp = (ticks + (long)(AudioSettings.dspTime * 10000000.0) - DateTime.Now.Ticks)
                         / 10000000.0;
            var c = ADOBase.conductor;
            double songPos = c.song.pitch * (dsp - c.dspTimeSong - scrConductor.calibration_i)
                             - c.addoffset;
            data.KeyEvent_list.Add(new KeyEvent
            {
                KeyCode = key.Key,
                IsPressed = key.Type == SkyHook.EventType.KeyPressed,
                SongPosition = songPos
            });
        }
    }
}
