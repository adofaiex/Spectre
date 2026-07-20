using ADOFAI;
using ADOFAI.Editor.Actions;
using HarmonyLib;

namespace Spectre.Features.EffectRemover;

[HarmonyPatch(typeof(LevelData), "Decode")]
internal static class Patch_LevelDataDecode
{
    [HarmonyPostfix]
    private static void Postfix(LevelData __instance) => EffectRemover.OnLevelDataDecode(__instance);
}

[HarmonyPatch(typeof(SaveLevelEditorAction), "Execute")]
internal static class Patch_SaveLevelEditorAction
{
    [HarmonyPrefix]
    private static bool Prefix() => EffectRemover.OnSaveLevelEditorActionPrefix();
}

[HarmonyPatch(typeof(scnEditor), "LoadGameScene")]
internal static class Patch_EditorLoadGameScene
{
    [HarmonyPostfix]
    private static void Postfix(scnEditor __instance) => EffectRemover.OnEditorLoadGameScenePostfix(__instance);
}
