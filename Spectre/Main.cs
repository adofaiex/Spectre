using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using SkyHook;
using UnityEngine;
using UnityModManagerNet;
using Spectre.Features.Replay;
using Spectre.Features.EffectRemover;
using ModEntry = UnityModManagerNet.UnityModManager.ModEntry;

namespace Spectre;

public static class Main
{
    public static ModEntry mod;

    public static void Load(ModEntry modEntry)
    {
        mod = modEntry;
        mod.OnToggle = OnToggle;
        mod.OnGUI = Options.OnGUI;
        mod.OnSaveGUI = Options.OnHideGUI;
        mod.OnHideGUI = Options.OnHideGUI;
        ConfigManager.LoadConfigs(Path.Combine(modEntry.Path, "Configs.json"));
        Options.check(modEntry.Path);
        WavLoader.reset();
        Debug.Log(mod.Version);
    }

    private static bool OnToggle(ModEntry modEntry, bool isToggled)
    {
        Harmony val = new Harmony(modEntry.Info.Id);
        if (isToggled)
        {
            Options.check(modEntry.Path);
            PatchManager.Initialize(val);
            PatchManager.RegisterPatches(() => true,
                typeof(Patch_TogglePauseGame), typeof(Patch_LightUp), typeof(Patch_Fail2Action),
                typeof(Patch_ConductorUpdate), typeof(Patch_CaptureEnqueue), typeof(Patch_Won_Update),
                typeof(Patch_PlayerControlUpdate), typeof(Patch_StartRewind),
                typeof(Patch_SwitchToEditMode), typeof(Patch_Restart), typeof(Patch_QuitToMainMenu),
                typeof(Patch_WipeToBlack), typeof(Patch_OnLandOnPortal), typeof(Patch_Hit),
                typeof(Patch_UpdateFreeroam), typeof(Patch_MarkFail),
                typeof(Patch_UpdateInput_ConsumeYch),
                typeof(Patch_LevelDataDecode), typeof(Patch_SaveLevelEditorAction),
                typeof(Patch_EditorLoadGameScene));
            PatchManager.ApplyAll();
            SpectreState.SetGetKeyAsyncEnabled(Options.GetDataFromAsyncInput);
            SpectreState.data = new LegacyReplayData();
            SpectreState.data.reset();
            SpectreState.PlayActions.SwitchToRecordMode();
        }
        else
        {
            PatchManager.UnpatchAll();
            AudioRecorder.StopAllMicrophones();
        }
        return true;
    }

    public static List<ModEntry> GetLoadedMods()
    {
        try
        {
            return UnityModManager.modEntries;
        }
        catch (Exception arg)
        {
            Debug.LogError($"获取模组列表失败: {arg}");
        }
        return new List<ModEntry>();
    }

    public static string mod2string(ModEntry mod)
    {
        return "[" + mod.Info.Id + "] " + mod.Info.DisplayName + " v" + mod.Info.Version + " by " + mod.Info.Author + " : " + (mod.Active ? "Enabled" : "Disabled");
    }
}
