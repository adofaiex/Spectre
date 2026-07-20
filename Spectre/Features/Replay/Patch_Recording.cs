using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using Spectre.API;
using Object = UnityEngine.Object;
using static Spectre.SpectreState;
using static Spectre.Features.Replay.ReplayKeys;

namespace Spectre.Features.Replay;

// ——— scrPlayer.Hit ————————————————————————————————————————
[HarmonyPatch(typeof(scrPlayer), "Hit")]
internal static class Patch_Hit
{
    [HarmonyPrefix]
    internal static bool Prefix(scrPlayer __instance, bool isAuto = false)
    {
        try
        {
            if (HitLocked) return false;
            if (!__instance.responsive) return false;
            if (ADOBase.isLevelEditor && ADOBase.controller.paused) return false;
            if (!scrController.instance.playerOne.HitInputEvent(isAuto, (InputEventState)0)) return false;
            if (!is_recording) return true;

            if (data == null) data = new LegacyReplayData();
            if (!data.strings.ContainsKey(SceneName)) return true;
            if (SceneManager.GetActiveScene().name != data.strings[SceneName]) return true;
            if (__instance != scrController.instance.playerOne) return true;

            float angle = GetAngle();
            var item = new HitContext
            {
                CurrentFloorID = scrController.instance.currFloor.seqID,
                CurrAngle = angle,
                OverloadCounter = scrController.instance.playerOne.failBar.overloadCounter,
                NoFailHit = scrController.instance.noFailInfiniteMargin,
                IsAuto = isAuto,
                NextFloorAuto = scrController.instance.chosenPlanet.currfloor.nextfloor != (Object)null
                    && scrController.instance.chosenPlanet.currfloor.nextfloor.auto,
                CachedAngle = scrController.instance.chosenPlanet.angle,
                TargetExitAngle = scrController.instance.chosenPlanet.targetExitAngle,
                MidspinInfiniteMargin = scrController.instance.playerOne.midspinInfiniteMargin,
                RDC_auto = RDC.auto,
                curFreeRoamSection = scrController.instance.curFreeRoamSection
            };

            if (!RDC.auto) AllAutoHits = false;

            if (data.HitContext_list.Count == 0)
            {
                CurrentHitFloor = scrController.instance.chosenPlanet.currfloor;
                for (int i = 0; i < HitMarginCounts.Length; i++)
                    HitMarginCounts[i] = scrController.instance.playerOne.marginTracker.hitMarginsCount[i];
            }
            else
            {
                bool changed = false;
                for (int j = 0; j < HitMarginCounts.Length; j++)
                {
                    if (HitMarginCounts[j] != scrController.instance.playerOne.marginTracker.hitMarginsCount[j])
                    { changed = true; break; }
                }
                if (CurrentHitFloor != scrController.instance.chosenPlanet.currfloor) changed = true;
                if (!changed)
                {
                    Debug.Log("hit deleted");
                    data.HitContext_list.RemoveAt(data.HitContext_list.Count - 1);
                }
            }

            CurrentHitFloor = scrController.instance.chosenPlanet.currfloor;
            for (int k = 0; k < HitMarginCounts.Length; k++)
                HitMarginCounts[k] = scrController.instance.playerOne.marginTracker.hitMarginsCount[k];
            data.HitContext_list.Add(item);
            return true;
        }
        catch (Exception ex)
        {
            PlayActions.StopPlaying();
            PlayActions.StopRecording();
            Debug.Log("hit error: " + ex.Message);
        }
        return false;
    }
}

// ——— scrController.OnLandOnPortal ——————————————————————————
[HarmonyPatch(typeof(scrController), "OnLandOnPortal")]
internal class Patch_OnLandOnPortal
{
    [HarmonyPostfix]
    internal static void Postfix(scrController __instance, Portal portalDestination, string portalArguments)
    {
        if ((int)portalDestination != 1 || !data.strings.ContainsKey(SceneName)) return;
        if (SceneManager.GetActiveScene().name != data.strings[SceneName]) return;
        if (!is_recording || !Options.AutoSave) return;
        if (Options.DontSaveWhenAuto && AllAutoHits) return;
        if (Options.CompleteSave && data.ints[StartTile] != 0) return;

        if (Options.LateSave) { LateSaveMode = true; return; }

        PlayActions.StopRecording();
        if (Options.DontSaveWhenMiss)
        {
            if (HitMarginCounts[8] + HitMarginCounts[9] == 0 && SaveFile())
                PendingSave = false;
        }
        else if (SaveFile())
            PendingSave = false;
    }
}

// ——— scrController.Fail2Action —————————————————————————————
[HarmonyPatch(typeof(scrController), "Fail2Action")]
internal class Patch_Fail2Action
{
    [HarmonyPostfix]
    internal static void Postfix(scrController __instance)
    {
        if (is_playing) TriggerMessage(LocalizationManager.GetLocalizedText("note.stop_playing"));
        if (is_recording) TriggerMessage(LocalizationManager.GetLocalizedText("note.stop_recording"));
        PlayActions.StopPlaying();
        PlayActions.StopRecording();
        if (Options.FailSave && PendingSave && SaveFile())
            PendingSave = false;
    }
}
