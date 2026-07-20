using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Spectre;

public static class ConfigManager
{
    internal static void SaveConfigs(string filePath)
    {
        var data = new
        {
            Sets = new
            {
                Options.AutoSave,
                Options.CompleteSave,
                Options.DontSaveWhenAuto,
                Options.DontSaveWhenMiss,
                Options.LateSave,
                Options.FailSave,
                Options.Backup,
                Options.ManualSave,
                Options.LegacyEngine,
                Options.KeybdSoundRecordActive,
                Options.FollowGameLanguage,
                Options.KeyConvertEnabled,
                Options.LimitKey,
                Options.GetDataFromAsyncInput,

                Options.EffectRemoverOn,
                Options.EffectRemoverEnableSave,
                Options.EffectRemoverFilters,
                Options.EffectRemoverAdvancedFilters,
                Options.EffectRemoverParticles,
                Options.EffectRemoverDecorations,
                Options.EffectRemoverBackgrounds,
                Options.EffectRemoverCameras,
                Options.EffectRemoverRepeatEvents,
                Options.EffectRemoverFrameRate,
                Options.EffectRemoverHitSounds,
                Options.EffectRemoverPlanetOrbit,
                Options.EffectRemoverPlanetScale,
                Options.EffectRemoverPlanetRadius,
                Options.EffectRemoverTrackAnimations,
                Options.EffectRemoverTrackPositions,
                Options.EffectRemoverTrackMoves,
                Options.EffectRemoverTrackColors,
                Options.EffectRemoverHoldSounds,
                Options.EffectRemoverHideIcons,
                Options.EffectRemoverRemoveAllDecorations,
                Options.EffectRemoverResetTrackOpacity,
                Options.EffectRemoverResetTrackAnimation,
                Options.EffectRemoverResetTrackColor,
                Options.EffectRemoverSetCameraZoom
            },
            Ints = new
            {
                text_size = Options.TextSize,
                save_button = Options.SaveButton,
                KeybdSoundVolume = Options.KeybdSoundVolume,
                MicrophoneOffset = Options.MicrophoneOffset,
                EffectRemoverCameraZoomScale = Options.EffectRemoverCameraZoomScale
            },
            Strings = new
            {
                savepath = Options.SavePath,
                inputedKey = Options.InputedKey,
                currlanguage = Options.CurrLanguage,
                microphone_device_name = Options.MicrophoneDeviceName,
                key_code_convert = Options.KeyCodeConvert
            }
        };
        string contents = JsonConvert.SerializeObject(data, Formatting.Indented);
        string directoryName = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directoryName))
            Directory.CreateDirectory(directoryName);
        File.WriteAllText(filePath, contents);
    }

    public static void LoadConfigs(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.Log(("Config file not found: " + filePath));
            return;
        }
        try
        {
            var json = JObject.Parse(File.ReadAllText(filePath));
            var sets = json["Sets"];
            if (sets != null)
            {
                Options.AutoSave = sets.Value<bool?>("AutoSave") ?? Options.AutoSave;
                Options.CompleteSave = sets.Value<bool?>("CompleteSave") ?? Options.CompleteSave;
                Options.DontSaveWhenAuto = sets.Value<bool?>("DontSaveWhenAuto") ?? Options.DontSaveWhenAuto;
                Options.DontSaveWhenMiss = sets.Value<bool?>("DontSaveWhenMiss") ?? Options.DontSaveWhenMiss;
                Options.LateSave = sets.Value<bool?>("LateSave") ?? Options.LateSave;
                Options.FailSave = sets.Value<bool?>("FailSave") ?? Options.FailSave;
                Options.Backup = sets.Value<bool?>("Backup") ?? Options.Backup;
                Options.ManualSave = sets.Value<bool?>("ManualSave") ?? Options.ManualSave;
                Options.LegacyEngine = sets.Value<bool?>("LegacyEngine") ?? Options.LegacyEngine;
                Options.KeybdSoundRecordActive = sets.Value<bool?>("KeybdSoundRecordActive") ?? Options.KeybdSoundRecordActive;
                Options.FollowGameLanguage = sets.Value<bool?>("FollowGameLanguage") ?? Options.FollowGameLanguage;
                Options.KeyConvertEnabled = sets.Value<bool?>("KeyConvertEnabled") ?? Options.KeyConvertEnabled;
                Options.LimitKey = sets.Value<bool?>("LimitKey") ?? Options.LimitKey;
                Options.GetDataFromAsyncInput = sets.Value<bool?>("GetDataFromAsyncInput") ?? Options.GetDataFromAsyncInput;

                Options.EffectRemoverOn = sets.Value<bool?>("EffectRemoverOn") ?? Options.EffectRemoverOn;
                Options.EffectRemoverEnableSave = sets.Value<bool?>("EffectRemoverEnableSave") ?? Options.EffectRemoverEnableSave;
                Options.EffectRemoverFilters = sets.Value<bool?>("EffectRemoverFilters") ?? Options.EffectRemoverFilters;
                Options.EffectRemoverAdvancedFilters = sets.Value<bool?>("EffectRemoverAdvancedFilters") ?? Options.EffectRemoverAdvancedFilters;
                Options.EffectRemoverParticles = sets.Value<bool?>("EffectRemoverParticles") ?? Options.EffectRemoverParticles;
                Options.EffectRemoverDecorations = sets.Value<bool?>("EffectRemoverDecorations") ?? Options.EffectRemoverDecorations;
                Options.EffectRemoverBackgrounds = sets.Value<bool?>("EffectRemoverBackgrounds") ?? Options.EffectRemoverBackgrounds;
                Options.EffectRemoverCameras = sets.Value<bool?>("EffectRemoverCameras") ?? Options.EffectRemoverCameras;
                Options.EffectRemoverRepeatEvents = sets.Value<bool?>("EffectRemoverRepeatEvents") ?? Options.EffectRemoverRepeatEvents;
                Options.EffectRemoverFrameRate = sets.Value<bool?>("EffectRemoverFrameRate") ?? Options.EffectRemoverFrameRate;
                Options.EffectRemoverHitSounds = sets.Value<bool?>("EffectRemoverHitSounds") ?? Options.EffectRemoverHitSounds;
                Options.EffectRemoverPlanetOrbit = sets.Value<bool?>("EffectRemoverPlanetOrbit") ?? Options.EffectRemoverPlanetOrbit;
                Options.EffectRemoverPlanetScale = sets.Value<bool?>("EffectRemoverPlanetScale") ?? Options.EffectRemoverPlanetScale;
                Options.EffectRemoverPlanetRadius = sets.Value<bool?>("EffectRemoverPlanetRadius") ?? Options.EffectRemoverPlanetRadius;
                Options.EffectRemoverTrackAnimations = sets.Value<bool?>("EffectRemoverTrackAnimations") ?? Options.EffectRemoverTrackAnimations;
                Options.EffectRemoverTrackPositions = sets.Value<bool?>("EffectRemoverTrackPositions") ?? Options.EffectRemoverTrackPositions;
                Options.EffectRemoverTrackMoves = sets.Value<bool?>("EffectRemoverTrackMoves") ?? Options.EffectRemoverTrackMoves;
                Options.EffectRemoverTrackColors = sets.Value<bool?>("EffectRemoverTrackColors") ?? Options.EffectRemoverTrackColors;
                Options.EffectRemoverHoldSounds = sets.Value<bool?>("EffectRemoverHoldSounds") ?? Options.EffectRemoverHoldSounds;
                Options.EffectRemoverHideIcons = sets.Value<bool?>("EffectRemoverHideIcons") ?? Options.EffectRemoverHideIcons;
                Options.EffectRemoverRemoveAllDecorations = sets.Value<bool?>("EffectRemoverRemoveAllDecorations") ?? Options.EffectRemoverRemoveAllDecorations;
                Options.EffectRemoverResetTrackOpacity = sets.Value<bool?>("EffectRemoverResetTrackOpacity") ?? Options.EffectRemoverResetTrackOpacity;
                Options.EffectRemoverResetTrackAnimation = sets.Value<bool?>("EffectRemoverResetTrackAnimation") ?? Options.EffectRemoverResetTrackAnimation;
                Options.EffectRemoverResetTrackColor = sets.Value<bool?>("EffectRemoverResetTrackColor") ?? Options.EffectRemoverResetTrackColor;
                Options.EffectRemoverSetCameraZoom = sets.Value<bool?>("EffectRemoverSetCameraZoom") ?? Options.EffectRemoverSetCameraZoom;
            }
            var ints = json["Ints"];
            if (ints != null)
            {
                Options.TextSize = ints.Value<int?>("text_size") ?? Options.TextSize;
                Options.SaveButton = ints.Value<int?>("save_button") ?? Options.SaveButton;
                Options.KeybdSoundVolume = ints.Value<int?>("KeybdSoundVolume") ?? Options.KeybdSoundVolume;
                Options.MicrophoneOffset = ints.Value<int?>("MicrophoneOffset") ?? Options.MicrophoneOffset;
                Options.EffectRemoverCameraZoomScale = ints.Value<float?>("EffectRemoverCameraZoomScale") ?? Options.EffectRemoverCameraZoomScale;
            }
            var strs = json["Strings"];
            if (strs != null)
            {
                Options.SavePath = strs.Value<string>("savepath") ?? Options.SavePath;
                Options.InputedKey = strs.Value<string>("inputedKey") ?? Options.InputedKey;
                Options.CurrLanguage = strs.Value<string>("currlanguage") ?? Options.CurrLanguage;
                Options.MicrophoneDeviceName = strs.Value<string>("microphone_device_name") ?? Options.MicrophoneDeviceName;
                Options.KeyCodeConvert = strs.Value<string>("key_code_convert") ?? Options.KeyCodeConvert;
            }
            Debug.Log("Config load success");
        }
        catch (JsonException ex)
        {
            Debug.Log(("Error parsing JSON file: " + ex.Message));
        }
    }
}
