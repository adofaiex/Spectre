using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Spectre.SpectreState;

namespace Spectre.Features.Replay;

// ——— scrController.TogglePauseGame ————————————————————----——
[HarmonyPatch(typeof(scrController), "TogglePauseGame")]
internal class Patch_TogglePauseGame
{
    [HarmonyPrefix]
    internal static bool Prefix()
    {
        if (RDC.auto
            && SceneManager.GetActiveScene().name == "scnEditor"
            && scnEditor.instance.playMode
            && is_playing)
        {
            scnEditor.instance.pausedInPlayMode = false;
            scnEditor.instance.buttonAuto.interactable = true;
            return false;
        }
        return true;
    }
}

// ——— scrConductor.Update ————————————————————————————————————
[HarmonyPatch(typeof(scrConductor), "Update")]
internal class Patch_ConductorUpdate
{
    [HarmonyPostfix]
    internal static void Postfix()
    {
        if (!Input.GetKeyDown(save_button)) return;
        if (!Options.ManualSave) return;
        int state = (int)(States)scrController.instance.stateMachine.GetState();
        if (state != 6 && state != 5 && state != 7) return;
        if (!PendingSave) return;

        PlayActions.StopRecording();
        if (SaveFile()) PendingSave = false;
    }
}

// ——— scrPlanet.MarkFail —————————————————————————————————————
[HarmonyPatch(typeof(scrPlanet), "MarkFail")]
internal static class Patch_MarkFail
{
    [HarmonyPrefix]
    internal static bool Prefix(ref scrMissIndicator __result)
    {
        if (IgnoreMarkFail) { __result = null; return false; }
        return true;
    }
}

// ——— scrFloor.LightUp ———————————————————————————————————————
[HarmonyPatch(typeof(scrFloor), "LightUp")]
internal class Patch_LightUp
{
    [HarmonyPrefix]
    internal static void Prefix()
    {
        if (is_playing) GCS.hitMarginLimit = Persistence.hitMarginLimit;
    }

    [HarmonyPostfix]
    internal static void Postfix()
    {
        if (is_playing) GCS.hitMarginLimit = StoredHitMarginLimit;
    }
}

// ——— scrController.UpdateFreeroam ——————————————————————————
[HarmonyPatch(typeof(scrController), "UpdateFreeroam")]
internal static class Patch_UpdateFreeroam
{
    [HarmonyPrefix]
    internal static bool Prefix(scrController __instance)
    {
        if (__instance == null) return true;
        if (is_playing && PlayIndex < data.HitContext_list.Count
            && data.HitContext_list[PlayIndex].curFreeRoamSection == __instance.curFreeRoamSection)
        {
            if (ADOBase.lm == null) return false;
            var starts = ADOBase.lm.listFreeroamStartTiles;
            if (starts == null || starts.Count == 0 || __instance.curFreeRoamSection >= starts.Count)
                return false;
            return false;
        }
        return true;
    }
}
