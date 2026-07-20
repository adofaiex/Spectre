using System;
using System.Linq;
using UnityEngine;
using static Spectre.SpectreState;

namespace Spectre.Features.Replay;

internal static class ReplayPlayer
{
    internal static void Start()
    {
        if (!is_playing && !ApiBlockPlaying)
        {
            Stop();
            CurrKeyState = new byte[256];
            PlayIndex = 0;
            KeyEventIndex = 0;
            is_playing = true;
            is_recording = false;
            HitLocked = true;
            LateSaveMode = false;
            IgnoreMarkFail = true;
        }
    }

    internal static void Stop()
    {
        if (!is_playing)
        {
            return;
        }
        GCS.hitMarginLimit = Persistence.hitMarginLimit;
        is_playing = false;
        if (PlayMode && Options.LegacyEngine)
        {
            for (int i = 1; i < 256; i++)
            {
                if ((CurrKeyState[i] & 0x80) != 0)
                {
                    KeyboardSimulation.ReleaseKey((byte)i);
                }
            }
        }
        scrController.instance.noFail = false;
        IgnoreMarkFail = false;
        try
        {
            WavLoader.Stop_keybdsound();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    internal static void FastForward(int targetFloor)
    {
        if (data == null) return;

        PlayIndex = 0;
        for (int i = 0; i < data.HitContext_list.Count; i++)
        {
            if (data.HitContext_list[i].CurrentFloorID >= targetFloor)
            {
                PlayIndex = i;
                break;
            }
        }

        if (ADOBase.lm != null && targetFloor < ADOBase.lm.listFloors.Count)
        {
            double targetTime = ADOBase.lm.listFloors[targetFloor].entryTime;
            KeyEventIndex = 0;
            for (int i = 0; i < data.KeyEvent_list.Count; i++)
            {
                if (data.KeyEvent_list[i].SongPosition >= targetTime)
                {
                    KeyEventIndex = i;
                    break;
                }
            }
        }

        CurrKeyState = new byte[256];
        AllAutoHits = false;
    }

    internal static void PlayKeyboardEvent(bool flag)
    {
        if (KeyEventIndex < 0 || KeyEventIndex >= data.KeyEvent_list.Count)
        {
            return;
        }
        KeyEvent KeyEvent2 = data.KeyEvent_list[KeyEventIndex];
        while (ADOBase.conductor.songposition_minusi >= KeyEvent2.SongPosition)
        {
            if (Options.LegacyEngine && Application.isFocused)
            {
                ushort num = Options.key_code_convert[KeyEvent2.KeyCode];
                if (num != 0 && num != 27 && !IsBlockedKey(num))
                {
                    KeyboardSimulation.SendKey((byte)num, KeyEvent2.IsPressed, IsExtendedKey(num));
                    CurrKeyState[num] = (byte)(KeyEvent2.IsPressed ? 128u : 0u);
                }
            }
            KeyEventIndex++;
            if (KeyEventIndex >= data.KeyEvent_list.Count)
            {
                if (flag)
                {
                    KeyEventIndex = 0;
                    PlayIndex = 0;
                    Stop();
                    TriggerMessage(LocalizationManager.GetLocalizedText("note.stop_playing"));
                }
                break;
            }
            KeyEvent2 = data.KeyEvent_list[KeyEventIndex];
        }
    }

    internal static bool ReplayHitOld(scrPlayer player, HitContext hitdata)
    {
        player.consecMultipressCounter = 0;
        scrController.instance.multipressPenalty = false;
        player.failBar.overloadCounter = 0f;
        scrController.instance.noFailInfiniteMargin = hitdata.NoFailHit;
        bool isAuto = hitdata.IsAuto;
        player.planetarySystem.chosenPlanet.SetTargetExitAngle(hitdata.TargetExitAngle);
        player.midspinInfiniteMargin = hitdata.MidspinInfiniteMargin;
        player.failBar.overloadCounter = hitdata.OverloadCounter;
        player.planetarySystem.chosenPlanet.angle = hitdata.CachedAngle;
        RDC.auto = hitdata.IsAuto;
        player.responsive = true;
        IgnoreMarkFail = false;
        if (hitdata.NoFailHit)
        {
            scrMissIndicator val = player.planetarySystem.chosenPlanet.MarkFail();
            if (val != null)
            {
                val.BlinkForSeconds(3f);
            }
        }
        scrMisc.Vibrate(50L);
        if (!player.responsive || player.isReunifying)
        {
            return false;
        }
        if (ADOBase.isLevelEditor && ADOBase.controller.paused)
        {
            return false;
        }
        bool flag = player.planetarySystem.chosenPlanet.currfloor.nextfloor != null && player.planetarySystem.chosenPlanet.currfloor.nextfloor.auto;
        player.planetarySystem.chosenPlanet.cachedAngle = player.planetarySystem.chosenPlanet.angle;
        scrFloor currFloor = player.planetarySystem.chosenPlanet.player.currFloor;
        player.planetarySystem.chosenPlanet.next.planetRenderer.ChangeFace(true, false);
        scrPlanet chosenPlanet = player.planetarySystem.chosenPlanet;
        player.planetarySystem.chosenPlanet = player.planetarySystem.chosenPlanet.SwitchChosen();
        bool result = chosenPlanet != player.planetarySystem.chosenPlanet;
        if (ADOBase.controller.errorMeter != null && ADOBase.controller.gameworld && (int)Persistence.hitErrorMeterSize > 0)
        {
            float num = (float)(chosenPlanet.cachedAngle - chosenPlanet.targetExitAngle);
            if (currFloor.isCCW)
            {
                num *= -1f;
            }
            if (!player.midspinInfiniteMargin)
            {
                if ((player.auto || flag) && !RDC.useOldAuto)
                {
                    ADOBase.controller.errorMeter.AddHit(0f, 1f, player.planetarySystem.chosenPlanet, currFloor);
                }
                else
                {
                    ADOBase.controller.errorMeter.AddHit(num, (float)player.currFloor.marginScale, player.planetarySystem.chosenPlanet, currFloor);
                }
            }
        }
        if (ADOBase.playerIsOnIntroScene)
        {
            return result;
        }
        bool flag2 = player.planetarySystem.chosenPlanet.currfloor.holdLength == -1 || (player.planetarySystem.chosenPlanet.currfloor.holdLength > -1 && ADOBase.controller.lastCamPulseFloor < player.planetarySystem.chosenPlanet.currfloor.seqID);
        ADOBase.controller.lastCamPulseFloor = player.planetarySystem.chosenPlanet.currfloor.seqID;
        scrCamera camy = ADOBase.controller.camy;
        if (flag2)
        {
            camy.UpdateFollowCam(false);
        }
        if (camy.isPulsingOnHit && flag2)
        {
            camy.Pulse();
        }
        bool flag3 = true;
        if (ADOBase.lm != null && ADOBase.controller.gameworld)
        {
            if (player.currFloor.midSpin || (player.currFloor.seqID > 0 && ADOBase.lm.listFloors[player.currFloor.seqID - 1].holdLength > -1))
            {
                flag3 = false;
            }
            if (player.currFloor.seqID > 1 && ADOBase.lm.listFloors[player.currFloor.seqID - 1].midSpin && ADOBase.lm.listFloors[player.currFloor.seqID - 2].holdLength > -1)
            {
                flag3 = false;
            }
        }
        if (flag3)
        {
            if (scnEditor.instance != null)
            {
                scnEditor.instance.OttoBlink();
            }
            if (VirtualAvatarCanvas.instance != null)
            {
                VirtualAvatarCanvas.instance.Hit(player.playerID);
            }
        }
        if (player.currFloor.midSpin)
        {
            player.midspinInfiniteMargin = true;
            player.keyTimes.Add(Time.unscaledTimeAsDouble);
        }
        else
        {
            player.midspinInfiniteMargin = false;
        }
        player.planetarySystem.chosenPlanet.Update_RefreshAngles();
        if (!hitdata.NextFloorAuto && hitdata.RDC_auto)
        {
            RDC.auto = true;
        }
        else
        {
            RDC.auto = false;
        }
        scrController.instance.noFailInfiniteMargin = false;
        IgnoreMarkFail = true;
        return result;
    }

    internal static bool ReplayHit(scrPlayer player, HitContext hitdata)
    {
        if (debug_use_old_hit)
        {
            return ReplayHitOld(player, hitdata);
        }
        player.consecMultipressCounter = 0;
        scrController.instance.multipressPenalty = false;
        player.failBar.overloadCounter = 0f;
        scrController.instance.noFailInfiniteMargin = hitdata.NoFailHit;
        bool isAuto = hitdata.IsAuto;
        player.planetarySystem.chosenPlanet.SetTargetExitAngle(hitdata.TargetExitAngle);
        player.midspinInfiniteMargin = hitdata.MidspinInfiniteMargin;
        player.failBar.overloadCounter = hitdata.OverloadCounter;
        player.planetarySystem.chosenPlanet.angle = hitdata.CachedAngle;
        RDC.auto = hitdata.IsAuto;
        player.responsive = true;
        IgnoreMarkFail = false;
        if (hitdata.NoFailHit)
        {
            scrMissIndicator val = player.planetarySystem.chosenPlanet.MarkFail();
            if (val != null)
            {
                val.BlinkForSeconds(3f);
            }
        }
        HitLocked = false;
        bool result = player.Hit(isAuto);
        HitLocked = true;
        if (!hitdata.NextFloorAuto && hitdata.RDC_auto)
        {
            RDC.auto = true;
        }
        else
        {
            RDC.auto = false;
        }
        scrController.instance.noFailInfiniteMargin = false;
        IgnoreMarkFail = true;
        return result;
    }
}
