using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityFileDialog;
using Spectre.Features.Replay;
using static Spectre.Features.Replay.ReplayKeys;
using ModEntry = UnityModManagerNet.UnityModManager.ModEntry;
using Object = UnityEngine.Object;

namespace Spectre;

public static class Options
{
    internal static bool showLangPopup = false;
    internal static bool showDevicePopup = false;

    public static bool AutoSave = true;
    public static bool CompleteSave = true;
    public static bool DontSaveWhenAuto = true;
    public static bool DontSaveWhenMiss = true;
    public static bool LateSave = true;
    public static bool FailSave = false;
    public static bool Backup = true;
    public static bool ManualSave = true;
    public static bool LegacyEngine = true;
    public static bool KeybdSoundRecordActive = true;
    public static bool FollowGameLanguage = true;
    public static bool KeyConvertEnabled = false;
    public static bool LimitKey = false;
    public static bool GetDataFromAsyncInput = false;

    public static int TextSize = 20;
    public static int SaveButton = 290;
    public static int KeybdSoundVolume = 10;
    public static int MicrophoneOffset = 0;

    public static string SavePath = "";
    public static string InputedKey = "";
    public static string CurrLanguage = "";
    public static string MicrophoneDeviceName = "";
    public static string KeyCodeConvert = "";

    internal static Vector2 scrollPosLang = Vector2.zero;
    internal static Vector2 scrollPosDevice = Vector2.zero;
    internal static Vector2 scrollPosKeyRemap = Vector2.zero;



    internal static string debugInputText = "";

    internal static bool debugKeyError = false;

    internal static int selectedTab = 0;
    internal static bool showReplayDetails = false;
    internal static Vector2 scrollPosReplayDetails = Vector2.zero;
    internal static Vector2 scrollPosEffectRemover = Vector2.zero;
    private static readonly string[] tabKeys = {
        "savesets.title",
        "replayingsets.title",
        "audiorecordsets.title",
        "modUI.title",
        "debugsets.title",
        "effectremover.title"
    };

    // ── EffectRemover settings ──────────────────────────
    public static bool EffectRemoverOn = true;
    public static bool EffectRemoverEnableSave = false;
    public static bool EffectRemoverFilters = true;
    public static bool EffectRemoverAdvancedFilters = true;
    public static bool EffectRemoverParticles = true;
    public static bool EffectRemoverDecorations = true;
    public static bool EffectRemoverBackgrounds = true;
    public static bool EffectRemoverCameras = false;
    public static bool EffectRemoverRepeatEvents = false;
    public static bool EffectRemoverFrameRate = true;
    public static bool EffectRemoverHitSounds = true;
    public static bool EffectRemoverPlanetOrbit = false;
    public static bool EffectRemoverPlanetScale = false;
    public static bool EffectRemoverPlanetRadius = false;
    public static bool EffectRemoverTrackAnimations = false;
    public static bool EffectRemoverTrackPositions = false;
    public static bool EffectRemoverTrackMoves = true;
    public static bool EffectRemoverTrackColors = true;
    public static bool EffectRemoverHoldSounds = false;
    public static bool EffectRemoverHideIcons = false;
    public static bool EffectRemoverRemoveAllDecorations = true;
    public static bool EffectRemoverResetTrackOpacity = false;
    public static bool EffectRemoverResetTrackAnimation = false;
    public static bool EffectRemoverResetTrackColor = true;
    public static bool EffectRemoverSetCameraZoom = false;
    public static float EffectRemoverCameraZoomScale = 250f;
    internal static bool debugTimeError = false;

    private static byte[] prevKeyState = new byte[256];
    private static byte[] currKeyState = new byte[256];

    internal static List<KeyValuePair<ushort, ushort>> key_code_convert_list = new List<KeyValuePair<ushort, ushort>>();
    internal static ushort[] key_code_convert = new ushort[256];
    internal static KeyValuePair<int, bool> key_code_convert_list_modify_pos = new KeyValuePair<int, bool>(-1, value: false);

    // ── Helpers ──────────────────────────────────────────

    private static string Fmt(string s) => $"<size={TextSize}>{s}</size>";
    private static string Loc(string key) => LocalizationManager.GetLocalizedText(key);
    private static string Loc(string key, string arg) => LocalizationManager.GetLocalizedText(key, arg);

    private static GUILayoutOption[] Opts(float w, float h = 1.5f)
        => new GUILayoutOption[2] { GUILayout.Width(TextSize * w), GUILayout.Height(TextSize * h) };

    private static void Label(string text)
        => GUILayout.Label(Fmt(text));

    private static void Label(string text, float w, float h = 1.5f)
        => GUILayout.Label(Fmt(text), Opts(w, h));

    private static bool Button(string text, float w = 20f, float h = 1.5f)
        => GUILayout.Button(Fmt(text), Opts(w, h));

    private static bool ButtonSmall(string text, float w = 4f, float h = 1.5f)
        => GUILayout.Button(Fmt(text), Opts(w, h));

    private static bool Tog(bool value, string text)
        => GUILayout.Toggle(value, Fmt(" " + text + " "), GUILayout.Height(TextSize * 1.5f));

    private static void H(Action draw)
    {
        GUILayout.BeginHorizontal();
        draw();
        GUILayout.EndHorizontal();
    }

    // ── Core ──────────────────────────────────────────────

    internal static void Reset()
    {
        scrollPosLang = Vector2.zero;
        scrollPosDevice = Vector2.zero;
        scrollPosKeyRemap = Vector2.zero;
        showLangPopup = false;
        showDevicePopup = false;
        debugKeyError = false;
        debugTimeError = false;
        key_code_convert_list_modify_pos = new KeyValuePair<int, bool>(-1, value: false);
    }

    internal static void key_convert_list2array()
    {
        key_code_convert = new ushort[256];
        for (int i = 0; i < 256; i++)
            key_code_convert[i] = 0xFFFF;
        if (!LimitKey || !KeyConvertEnabled)
        {
            for (ushort num = 0; num < 256; num++)
                key_code_convert[num] = num;
        }
        if (!KeyConvertEnabled)
            return;
        foreach (KeyValuePair<ushort, ushort> item in key_code_convert_list)
        {
            if (item.Key < 256)
                key_code_convert[item.Key] = item.Value;
        }
        for (ushort num = 0; num < 256; num++)
        {
            if (key_code_convert[num] == 0xFFFF)
                key_code_convert[num] = num;
        }
    }

    internal static void OnHideGUI(ModEntry modEntry)
    {
        if (SpectreState.is_recording || SpectreState.is_playing)
            return;
        InputedKey = debugInputText;
        KeyCodeConvert = JsonConvert.SerializeObject(key_code_convert_list, Formatting.Indented);
        key_convert_list2array();
        ConfigManager.SaveConfigs(Path.Combine(modEntry.Path, "Configs.json"));
        Reset();
        AudioRecorder.StopAllMicrophones();
        key_code_convert_list_modify_pos = new KeyValuePair<int, bool>(-1, value: false);
        if (KeybdSoundRecordActive && SpectreState.RecordMode)
        {
            if (AudioRecorder.Instance.ValidateDevice(MicrophoneDeviceName))
                AudioRecorder.Instance.startMicrophone(MicrophoneDeviceName);
        }
        SpectreState.SetGetKeyAsyncEnabled(GetDataFromAsyncInput);
    }

    internal static void checklang()
    {
        if (FollowGameLanguage)
            LocalizationManager.GetLanguages();
        else if (LocalizationManager.currentLanguage != CurrLanguage)
            LocalizationManager.SwitchLanguage(CurrLanguage);
    }

    internal static void check(string path = "")
    {
        SavePath = string.IsNullOrEmpty(SavePath) ? path : SavePath;
        CurrLanguage = string.IsNullOrEmpty(CurrLanguage) ? LocalizationManager.GetLangCode(RDString.language) : CurrLanguage;
        if (string.IsNullOrEmpty(MicrophoneDeviceName))
            MicrophoneDeviceName = Microphone.devices.Length != 0 ? Microphone.devices[0] : "None";
        if (Microphone.devices.Length == 0)
            KeybdSoundRecordActive = false;
        if (string.IsNullOrEmpty(KeyCodeConvert))
        {
            key_code_convert_list = new List<KeyValuePair<ushort, ushort>>();
            KeyCodeConvert = JsonConvert.SerializeObject(key_code_convert_list, Formatting.Indented);
        }
        else
        {
            try
            {
                key_code_convert_list = JsonConvert.DeserializeObject<List<KeyValuePair<ushort, ushort>>>(KeyCodeConvert);
            }
            catch
            {
                key_code_convert_list = new List<KeyValuePair<ushort, ushort>>();
                KeyCodeConvert = JsonConvert.SerializeObject(key_code_convert_list, Formatting.Indented);
            }
            key_code_convert_list ??= new List<KeyValuePair<ushort, ushort>>();
        }
        checklang();
        key_convert_list2array();
        for (ushort num = 0; num < 256; num++)
        {
            if (key_code_convert[num] == 0xFFFF)
                key_code_convert[num] = num;
        }
        SpectreState.save_button = (KeyCode)SaveButton;
    }

    // ── OnGUI ─────────────────────────────────────────────

    internal static void OnGUI(ModEntry modEntry)
    {
        checklang();
        GUI.enabled = true;

        if (SpectreState.is_recording || SpectreState.is_playing)
        {
            Label(Loc("main.text1"));
            return;
        }

        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(TextSize * 14), GUILayout.ExpandHeight(true));
        GUI.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
        DrawPlaybackTab();
        DrawTabBar();
        GUI.backgroundColor = Color.white;
        GUILayout.EndVertical();

        GUILayout.BeginVertical();
        DrawTabContent();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    private static void DrawPlaybackTab()
    {
        GUI.backgroundColor = new Color(0.95f, 0.95f, 0.95f);
        if (SpectreState.PlayMode)
        {
            if (Button(Loc("playsets.unload"), 14f, 3f))
            {
                SpectreState.PlayActions.StopPlaying();
                SpectreState.PlayActions.StopRecording();
                ReplayManager.UnLoadReplay(out SpectreState.data);
                SpectreState.IsFullRun = false;
                SpectreState.PlayActions.SwitchToRecordMode();
            }
        }
        else
        {
            if (Button(Loc("playsets.load"), 14f, 3f))
            {
                string[] exts = SpectreState.debug_no_encryption
                    ? new[] { "psprp", "pcrpl", "pcrpl2" }
                    : new[] { "sprp", "crpl", "crpl2" };
                string file = FileBrowser.PickFile(SavePath, "Spectre replay file", exts, Loc("playsets.select"));
                if (!string.IsNullOrEmpty(file) && File.Exists(file))
                {
                    SpectreState.PlayActions.StopPlaying();
                    SpectreState.PlayActions.StopRecording();
                    if (ReplayManager.LoadReplayAuto(out SpectreState.data, file, SpectreState.debug_no_encryption))
                    {
                        SpectreState.IsFullRun = SpectreState.data.ints.ContainsKey(StartTile)
                            && SpectreState.data.ints[StartTile] == 0
                            && SpectreState.data.ints.ContainsKey(EndTile)
                            && ADOBase.lm != null
                            && SpectreState.data.ints[EndTile] == ADOBase.lm.listFloors.Count - 1;
                        SpectreState.PlayActions.SwitchToPlayMode();
                        selectedTab = 1;
                    }
                }
            }
        }
    }

    private static void DrawTabBar()
    {
        for (int i = 0; i < tabKeys.Length; i++)
        {
            if (selectedTab == i)
                GUI.backgroundColor = new Color(0.95f, 0.95f, 0.95f);
            else
                GUI.backgroundColor = new Color(0.3f, 0.3f, 0.3f);

            if (Button(Loc(tabKeys[i]), 14f))
                selectedTab = i;
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawTabContent()
    {
        if (!(SpectreState.PlayMode && SpectreState.data != null))
        {
            showReplayDetails = false;
        }
        switch (selectedTab)
        {
            case 0: DrawSaveSettingsContent(); break;
            case 1: DrawReplayingSettingsContent(); break;
            case 2: DrawAudioSettingsContent(); break;
            case 3: DrawUISettingsContent(); break;
            case 4: DrawDebugSettingsContent(); break;
            case 5: DrawEffectRemoverContent(); break;
        }
    }

    // ── Save settings ─────────────────────────────────────

    private static void DrawSaveSettingsContent()
    {
        AutoSave = Tog(AutoSave, Loc("savesets.autosave"));
        if (!AutoSave) GUI.enabled = false;
        CompleteSave = Tog(CompleteSave, Loc("savesets.completesave"));
        DontSaveWhenAuto = Tog(DontSaveWhenAuto, Loc("savesets.dontsavewhenauto"));
        DontSaveWhenMiss = Tog(DontSaveWhenMiss, Loc("savesets.dontsavewhenmiss"));
        LateSave = Tog(LateSave, Loc("savesets.latesave"));
        GUI.enabled = true;
        FailSave = Tog(FailSave, Loc("savesets.failsave"));
        Backup = Tog(Backup, Loc("savesets.backup"));
        ManualSave = Tog(ManualSave, Loc("savesets.manualsave"));

        if (!ManualSave) GUI.enabled = false;

        string saveKeyLabel = Loc("savesets.choosekey");
        if (SaveButton > 0)
            saveKeyLabel = ((KeyCode)SaveButton).ToString();
        else if (Input.anyKeyDown)
        {
            foreach (KeyCode v in Enum.GetValues(typeof(KeyCode)))
            {
                if (Input.GetKeyDown(v))
                {
                    SaveButton = (int)v;
                    SpectreState.save_button = (KeyCode)SaveButton;
                    break;
                }
            }
        }
        if (Button(saveKeyLabel))
            SaveButton = 0;

        GUI.enabled = true;
        Label(Loc("savesets.path") + SavePath);
        if (Button(Loc("savesets.select")))
        {
            string folder = FileBrowser.PickFolder(SavePath, "", new[] { "" }, Loc("savesets.select"));
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                SavePath = folder;
                Debug.Log("Selected folder path: " + folder);
            }
        }
    }

    // ── Replaying settings ────────────────────────────────

    private static void DrawReplayingSettingsContent()
    {
        if (SpectreState.PlayMode && SpectreState.data != null)
        {
            string path = SpectreState.data.strings.ContainsKey(LevelPath) ? SpectreState.data.strings[LevelPath] : "";
            Label(Loc("playsets.currfile") + " " + path);
            if (SpectreState.IsFullRun)
                Label("<color=#88FF88>" + Loc("playsets.fullrun") + "</color>");

            if (GUILayout.Button(Fmt((showReplayDetails ? "◢" : "?") + Loc("playsets.showdatainfo")),
                GUILayout.Width(TextSize * 30), GUILayout.Height(TextSize * 1.5f)))
            {
                showReplayDetails = !showReplayDetails;
            }
            if (showReplayDetails)
            {
                scrollPosReplayDetails = GUILayout.BeginScrollView(scrollPosReplayDetails,
                    GUILayout.Width(TextSize * 30), GUILayout.Height(TextSize * 10));
                foreach (var key in SpectreState.data.strings.Keys)
                {
                    if (SpectreState.data.strings[key] != null)
                    {
                        string text = "<color=#C2FEFF>" + key + ":</color>\n" + SpectreState.data.strings[key].Replace("<", "<\u200b");
                        GUILayout.Label(Fmt(text), GUILayout.MaxWidth(TextSize * 30));
                    }
                }
                foreach (var key in SpectreState.data.ints.Keys)
                {
                    string text = "<color=#C2FEFF>" + key + ":</color>\n" + SpectreState.data.ints[key];
                    GUILayout.Label(Fmt(text), GUILayout.MaxWidth(TextSize * 30));
                }
                foreach (var key in SpectreState.data.doubles.Keys)
                {
                    string text = "<color=#C2FEFF>" + key + ":</color>\n" + SpectreState.data.doubles[key];
                    GUILayout.Label(Fmt(text), GUILayout.MaxWidth(TextSize * 30));
                }
                foreach (var key in SpectreState.data.bools.Keys)
                {
                    string text = "<color=#C2FEFF>" + key + ":</color>\n" + SpectreState.data.bools[key];
                    GUILayout.Label(Fmt(text), GUILayout.MaxWidth(TextSize * 30));
                }
                GUILayout.EndScrollView();
            }
        }
        Label(Loc("replayingsets.text2") + ":" + KeybdSoundVolume, 20f);
        H(() =>
        {
            if (ButtonSmall("-", 2.5f) && KeybdSoundVolume > 0) KeybdSoundVolume--;
            if (ButtonSmall("+", 2.5f) && KeybdSoundVolume < 10) KeybdSoundVolume++;
        });
        LegacyEngine = Tog(LegacyEngine, Loc("replayingsets.text1"));
        GetDataFromAsyncInput = Tog(GetDataFromAsyncInput, Loc("replayingsets.experimental") + Loc("keyrecordsets.get_data_from_asyncinput"));
        KeyConvertEnabled = Tog(KeyConvertEnabled, Loc("replayingsets.keyremapping"));
        if (!KeyConvertEnabled) GUI.enabled = false;
        LimitKey = Tog(LimitKey, Loc("replayingsets.keyremapping_limitkey"));
        if (KeyConvertEnabled) DrawKeyRemapList();
        GUI.enabled = true;
    }

    private static void DrawKeyRemapList()
    {
        scrollPosKeyRemap = GUILayout.BeginScrollView(scrollPosKeyRemap, GUILayout.Height(TextSize * 10));
        int removeIdx = -1;
        for (int i = 0; i < key_code_convert_list.Count; i++)
        {
            var pair = key_code_convert_list[i];
            if (key_code_convert_list_modify_pos.Key != -1)
            {
                SpectreState.GetKeyboardState(currKeyState);
                for (ushort k = 1; k < 256; k++)
                {
                    if (!SpectreState.IsBlockedKey(k) && (prevKeyState[k] & 0x80) == 0 && (currKeyState[k] & 0x80) != 0)
                    {
                        if (key_code_convert_list_modify_pos.Value)
                            key_code_convert_list[key_code_convert_list_modify_pos.Key] = new KeyValuePair<ushort, ushort>(k, key_code_convert_list[key_code_convert_list_modify_pos.Key].Value);
                        else
                            key_code_convert_list[key_code_convert_list_modify_pos.Key] = new KeyValuePair<ushort, ushort>(key_code_convert_list[key_code_convert_list_modify_pos.Key].Key, k);
                        key_code_convert_list_modify_pos = new KeyValuePair<int, bool>(-1, value: false);
                        break;
                    }
                    prevKeyState[k] = currKeyState[k];
                }
            }
            H(() =>
            {
                string from = pair.Key == 0 ? "-" : pair.Key.ToString();
                if (key_code_convert_list_modify_pos.Key == i && key_code_convert_list_modify_pos.Value)
                    from = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 2 == 0L) ? "_" : " ";
                if (ButtonSmall(from))
                {
                    key_code_convert_list_modify_pos = new KeyValuePair<int, bool>(i, value: true);
                    SpectreState.GetKeyboardState(prevKeyState);
                }
                GUILayout.Label(Fmt("→"), Opts(1f));
                string to = pair.Value == 0 ? "-" : pair.Value.ToString();
                if (key_code_convert_list_modify_pos.Key == i && !key_code_convert_list_modify_pos.Value)
                    to = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 2 == 0L) ? "_" : " ";
                if (ButtonSmall(to))
                {
                    key_code_convert_list_modify_pos = new KeyValuePair<int, bool>(i, value: false);
                    SpectreState.GetKeyboardState(prevKeyState);
                }
                if (Button(Loc("replayingsets.keyremapping_setempty"), 8f, 1.5f))
                    key_code_convert_list[i] = new KeyValuePair<ushort, ushort>(key_code_convert_list[i].Key, 0);
                if (Button(Loc("replayingsets.keyremapping_delete"), 8f, 1.5f))
                    removeIdx = i;
            });
        }
        if (ButtonSmall("+"))
            key_code_convert_list.Add(new KeyValuePair<ushort, ushort>(0, 0));
        GUILayout.EndScrollView();
        if (removeIdx != -1)
        {
            key_code_convert_list.RemoveAt(removeIdx);
            key_code_convert_list_modify_pos = new KeyValuePair<int, bool>(-1, value: false);
        }
    }

    // ── Audio settings ────────────────────────────────────

    private static void DrawAudioSettingsContent()
    {
        if (Microphone.devices.Length == 0)
        {
            KeybdSoundRecordActive = false;
            MicrophoneDeviceName = "None";
            Label(Loc("audiorecordsets.no_device") + ":" + TextSize, 10f);
            return;
        }

        KeybdSoundRecordActive = Tog(KeybdSoundRecordActive, Loc("audiorecordsets.if_active"));
        if (!KeybdSoundRecordActive) GUI.enabled = false;

        if (Button(Loc("audiorecordsets.device") + ":" + MicrophoneDeviceName, 30f))
        {
            showDevicePopup = !showDevicePopup;
            if (!AudioRecorder.Instance.ValidateDevice(MicrophoneDeviceName))
                MicrophoneDeviceName = Microphone.devices[0];
        }
        if (!KeybdSoundRecordActive) showDevicePopup = false;
        if (showDevicePopup)
        {
            scrollPosDevice = GUILayout.BeginScrollView(scrollPosDevice, GUILayout.Height(TextSize * 10));
            foreach (string d in Microphone.devices)
            {
                if (Button(d, 30f))
                {
                    MicrophoneDeviceName = d;
                    showDevicePopup = false;
                }
            }
            GUILayout.EndScrollView();
        }

        Label(Loc("audiorecordsets.microphone_offset") + ":" + MicrophoneOffset, 20f);
        H(() =>
        {
            if (Button("-10", 2.5f)) MicrophoneOffset -= 10;
            if (Button("-1", 2.5f)) MicrophoneOffset--;
            if (Button("+1", 2.5f)) MicrophoneOffset++;
            if (Button("+10", 2.5f)) MicrophoneOffset += 10;
            if (Button(Loc("audiorecordsets.reset"), 4f)) MicrophoneOffset = 0;
        });
        GUI.enabled = true;
    }

    // ── Mod UI settings ───────────────────────────────────

    private static void DrawUISettingsContent()
    {
        FollowGameLanguage = Tog(FollowGameLanguage, Loc("modUI.gamelanguage"));
        if (FollowGameLanguage)
        {
            CurrLanguage = LocalizationManager.GetLangCode(RDString.language);
            GUI.enabled = false;
            showLangPopup = false;
        }

        if (Button(Loc("modUI.language") + ":" + Loc("modUI.currlanguage")))
            showLangPopup = !showLangPopup;

        if (showLangPopup)
        {
            scrollPosLang = GUILayout.BeginScrollView(scrollPosLang, GUILayout.Height(TextSize * 10));
            foreach (string code in LocalizationManager.langCodeToLanguage.Keys)
            {
                if (Button(Loc("modUI.currlanguage", code)))
                {
                    CurrLanguage = code;
                    showLangPopup = false;
                }
            }
            GUILayout.EndScrollView();
        }

        GUI.enabled = true;
        Label(Loc("modUI.textsize") + ":" + TextSize, 20f);
        H(() =>
        {
            if (Button("-", 2.5f) && TextSize > 10) TextSize--;
            if (Button("+", 2.5f) && TextSize < 40) TextSize++;
        });
    }

    // ── Debug settings ────────────────────────────────────

    private static void FormatButton(string label, ReplayManager.ReplaySaveFormat fmt)
    {
        if (SpectreState.debug_save_format == fmt)
            GUI.backgroundColor = new Color(0.3f, 0.55f, 0.7f);
        if (Button(label))
            SpectreState.debug_save_format = fmt;
        GUI.backgroundColor = Color.white;
    }

    private static void DrawDebugSettingsContent()
    {
        Label(Loc("debugsets.device_id") + SystemInfo.deviceUniqueIdentifier);
        if (Button(Loc("debugsets.copy"), 5f))
            GUIUtility.systemCopyBuffer = SystemInfo.deviceUniqueIdentifier;

        if (!SpectreState.DebugMode)
        {
            DrawDebugAuth();
            return;
        }

        Label(Loc("debugsets.debug_on"));
        SpectreState.debug_skip_Verification = Tog(SpectreState.debug_skip_Verification, Loc("debugsets.skip_verification"));
        SpectreState.debug_no_encryption = Tog(SpectreState.debug_no_encryption, Loc("debugsets.disable_encryption"));
        SpectreState.UseLegacyAsyncRecorder = Tog(SpectreState.UseLegacyAsyncRecorder, Loc("debugsets.use_ych_async"));

        Label(Loc("debugsets.save_format"));
        FormatButton("sprp / psprp", ReplayManager.ReplaySaveFormat.New);
        FormatButton("crpl2 / pcrpl2", ReplayManager.ReplaySaveFormat.Crp2);
        FormatButton("crpl / pcrpl", ReplayManager.ReplaySaveFormat.Json);

        if (!SpectreState.PlayMode) GUI.enabled = false;
        if (Button(Loc("debugsets.save_as"), 10f) && SpectreState.PlayMode)
        {
            if (!SpectreState.data.strings.ContainsKey(FileName))
                SpectreState.data.strings[FileName] = "1";
            string orig = SpectreState.data.strings[FileName];
            SpectreState.data.strings[FileName] = orig + "_1";
            SpectreState.SaveFile(without_sound: true);
            SpectreState.data.strings[FileName] = orig;
        }
        GUI.enabled = true;

        SpectreState.debug_hide_text = Tog(SpectreState.debug_hide_text, Loc("debugsets.hide_message"));
        SpectreState.debug_use_old_hit = Tog(SpectreState.debug_use_old_hit, Loc("debugsets.use_old_hit"));

        if (Button(Loc("debugsets.turn_off")))
        {
            SpectreState.DebugMode = false;
            SpectreState.debug_hide_text = false;
            SpectreState.debug_no_encryption = false;
            SpectreState.debug_skip_Verification = false;
            SpectreState.debug_save_format = ReplayManager.ReplaySaveFormat.New;
        }
    }

    private static void DrawDebugAuth()
    {
        Label(Loc("debugsets.enter_key"));
        debugInputText = GUILayout.TextField(debugInputText, Opts(20f, 1.5f));
        if (Button(Loc("debugsets.confirm")) && debugInputText != "")
        {
            string time = "";
            for (int i = 0; i < 5; i++)
            {
                time = KeyManager.TryGetInternetTime();
                if (time != "") break;
            }
            if (time == "")
            {
                debugTimeError = true;
                debugKeyError = false;
            }
            else
            {
                debugTimeError = false;
                if (KeyManager.ValidateKey(debugInputText, time))
                {
                    SpectreState.DebugMode = true;
                    debugKeyError = false;
                }
                else
                {
                    SpectreState.DebugMode = false;
                    debugKeyError = true;
                }
            }
        }
        if (debugKeyError) Label(Loc("debugsets.key_error"));
        if (debugTimeError) Label(Loc("debugsets.time_error"));
    }

    // ── EffectRemover settings ──────────────────────────

    private static void DrawEffectRemoverContent()
    {
        bool prev = EffectRemoverOn;
        EffectRemoverOn = Tog(EffectRemoverOn, Loc("effectremover.title"));
        if (prev != EffectRemoverOn)
        {
            Features.EffectRemover.EffectRemover.RefreshEditorSaveButtons();
            PatchManager.RefreshPatches();
        }

        if (!EffectRemoverOn)
        {
            scrollPosEffectRemover = Vector2.zero;
            return;
        }

        scrollPosEffectRemover = GUILayout.BeginScrollView(scrollPosEffectRemover, GUILayout.Height(TextSize * 25));

        H(() =>
        {
            Label(Loc("effectremover.save") + ": " + (EffectRemoverEnableSave ? Loc("common.on") : Loc("common.off")));
            if (Button(Loc("effectremover.toggle"), 10f))
            {
                EffectRemoverEnableSave = !EffectRemoverEnableSave;
                Features.EffectRemover.EffectRemover.RefreshEditorSaveButtons();
                PatchManager.RefreshPatches();
            }
        });

        Label(Loc("effectremover.nondlc"));
        EffectRemoverFilters = Tog(EffectRemoverFilters, Loc("effectremover.filters"));
        EffectRemoverAdvancedFilters = Tog(EffectRemoverAdvancedFilters, Loc("effectremover.advancedfilters"));
        EffectRemoverParticles = Tog(EffectRemoverParticles, Loc("effectremover.particles"));
        EffectRemoverDecorations = Tog(EffectRemoverDecorations, Loc("effectremover.decorations"));
        EffectRemoverBackgrounds = Tog(EffectRemoverBackgrounds, Loc("effectremover.backgrounds"));
        EffectRemoverCameras = Tog(EffectRemoverCameras, Loc("effectremover.cameras"));
        EffectRemoverRepeatEvents = Tog(EffectRemoverRepeatEvents, Loc("effectremover.repeatevents"));
        EffectRemoverFrameRate = Tog(EffectRemoverFrameRate, Loc("effectremover.framerate"));
        EffectRemoverHitSounds = Tog(EffectRemoverHitSounds, Loc("effectremover.hitsounds"));

        Label(Loc("effectremover.planetevents"));
        H(() =>
        {
            int count = (EffectRemoverPlanetOrbit ? 1 : 0) + (EffectRemoverPlanetScale ? 1 : 0) + (EffectRemoverPlanetRadius ? 1 : 0);
            if (Button(Loc("effectremover.toggleall"), 10f))
            {
                bool val = count == 0;
                EffectRemoverPlanetOrbit = val;
                EffectRemoverPlanetScale = val;
                EffectRemoverPlanetRadius = val;
            }
        });
        EffectRemoverPlanetOrbit = Tog(EffectRemoverPlanetOrbit, Loc("effectremover.planetorbit"));
        EffectRemoverPlanetScale = Tog(EffectRemoverPlanetScale, Loc("effectremover.planetscale"));
        EffectRemoverPlanetRadius = Tog(EffectRemoverPlanetRadius, Loc("effectremover.planetradius"));

        Label(Loc("effectremover.trackevents"));
        H(() =>
        {
            int count = (EffectRemoverTrackAnimations ? 1 : 0) + (EffectRemoverTrackPositions ? 1 : 0) + (EffectRemoverTrackMoves ? 1 : 0) + (EffectRemoverTrackColors ? 1 : 0);
            if (Button(Loc("effectremover.toggleall"), 10f))
            {
                bool val = count == 0;
                EffectRemoverTrackAnimations = val;
                EffectRemoverTrackPositions = val;
                EffectRemoverTrackMoves = val;
                EffectRemoverTrackColors = val;
            }
        });
        EffectRemoverTrackAnimations = Tog(EffectRemoverTrackAnimations, Loc("effectremover.trackanimations"));
        EffectRemoverTrackMoves = Tog(EffectRemoverTrackMoves, Loc("effectremover.trackmoves"));
        EffectRemoverTrackPositions = Tog(EffectRemoverTrackPositions, Loc("effectremover.trackpositions"));
        EffectRemoverTrackColors = Tog(EffectRemoverTrackColors, Loc("effectremover.trackcolors"));

        Label(Loc("effectremover.dlc"));
        EffectRemoverHoldSounds = Tog(EffectRemoverHoldSounds, Loc("effectremover.holdsounds"));
        EffectRemoverHideIcons = Tog(EffectRemoverHideIcons, Loc("effectremover.hideicons"));

        Label(Loc("effectremover.misc"));
        if (EffectRemoverDecorations)
            EffectRemoverRemoveAllDecorations = Tog(EffectRemoverRemoveAllDecorations, Loc("effectremover.removealldecorations"));
        EffectRemoverResetTrackOpacity = Tog(EffectRemoverResetTrackOpacity, Loc("effectremover.resettrackopacity"));
        if (EffectRemoverCameras)
        {
            EffectRemoverSetCameraZoom = Tog(EffectRemoverSetCameraZoom, Loc("effectremover.setcamerazoom"));
            if (EffectRemoverSetCameraZoom)
            {
                Label(Loc("effectremover.zoom") + ": " + EffectRemoverCameraZoomScale, 20f);
                H(() =>
                {
                    if (Button("-50", 2.5f)) EffectRemoverCameraZoomScale = Mathf.Max(100f, EffectRemoverCameraZoomScale - 50f);
                    if (Button("-10", 2.5f)) EffectRemoverCameraZoomScale = Mathf.Max(100f, EffectRemoverCameraZoomScale - 10f);
                    if (Button("+10", 2.5f)) EffectRemoverCameraZoomScale = Mathf.Min(1000f, EffectRemoverCameraZoomScale + 10f);
                    if (Button("+50", 2.5f)) EffectRemoverCameraZoomScale = Mathf.Min(1000f, EffectRemoverCameraZoomScale + 50f);
                });
            }
        }
        if (EffectRemoverTrackAnimations)
            EffectRemoverResetTrackAnimation = Tog(EffectRemoverResetTrackAnimation, Loc("effectremover.resettrackanimation"));
        if (EffectRemoverTrackColors)
            EffectRemoverResetTrackColor = Tog(EffectRemoverResetTrackColor, Loc("effectremover.resettrackcolor"));

        GUILayout.EndScrollView();
    }
}
