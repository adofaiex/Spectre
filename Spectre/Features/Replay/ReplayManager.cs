using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using static Spectre.Features.Replay.ReplayKeys;

namespace Spectre.Features.Replay;

internal static class ReplayManager
{
    public enum ReplaySaveFormat
    {
        New,   // sprp / psprp
        Crp2,  // crpl2 / pcrpl2
        Json   // crpl / pcrpl
    }

    private class ConfigData
    {
        public CompactReplayData CompactReplayFile { get; set; }
    }

    internal static string RemoveInvalidPathChars(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        char[] invalidPathChars = Path.GetInvalidPathChars();
        StringBuilder sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            bool valid = true;
            if (c < ' ' || c == '\u007f' || c == '/' || c == '\\' || c == ':' || c == '?' || c == '*' || c == '<' || c == '>' || c == '|' || c == '"' || c == '\n')
                valid = false;
            else
            {
                foreach (char ic in invalidPathChars)
                {
                    if (c == ic) { valid = false; break; }
                }
            }
            sb.Append(valid ? c : '_');
        }
        return sb.ToString().Trim();
    }

    internal static bool SaveReplay(LegacyReplayData replayData, string filePath, bool no_encryption = false)
        => SaveReplay(replayData, filePath, ReplaySaveFormat.New, no_encryption);

    internal static bool SaveReplay(LegacyReplayData replayData, string filePath, ReplaySaveFormat format, bool no_encryption = false)
    {
        switch (format)
        {
            case ReplaySaveFormat.Json:
                return SaveReplayJson(replayData, filePath, no_encryption);
            case ReplaySaveFormat.Crp2:
                return SaveReplayOptimized(replayData, filePath, no_encryption);
            default:
                return SaveReplayNew(replayData, filePath, no_encryption);
        }
    }

    internal static bool SaveReplayNew(LegacyReplayData replayData, string filePath, bool no_encryption = false)
    {
        try
        {
            var replay = ReplayDecoder.ConvertFromOldFormat(replayData);
            if (replay == null) return false;

            byte[] data = ReplayEncoder.Encode(replay);
            string ext = no_encryption ? ReplayConstants.ExtensionNoEncrypt : ReplayConstants.Extension;
            filePath += ext;

            if (!no_encryption)
                data = ReplayCrypto.Encrypt(data);

            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(filePath, data);
            Debug.Log("replay saved! (" + ext + ")");
        }
        catch (Exception ex)
        {
            Debug.Log("SaveReplay error: " + ex);
        }
        return File.Exists(filePath);
    }

    internal static bool SaveReplayJson(LegacyReplayData replayData, string filePath, bool no_encryption = false)
    {
        try
        {
            string ext = no_encryption ? ".pcrpl" : ".crpl";
            filePath += ext;
            string json = JsonConvert.SerializeObject(replayData, Formatting.Indented);
            json = DemoteJsonFieldNames(json);
            byte[] data = Encoding.UTF8.GetBytes(json);
            if (!no_encryption)
                data = ReplayCrypto.Encrypt(data);

            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(filePath, data);
            Debug.Log("replay saved! (" + ext + ")");
        }
        catch (Exception ex)
        {
            Debug.Log("SaveReplayJson error: " + ex);
        }
        return File.Exists(filePath);
    }

    internal static bool SaveReplayOptimized(LegacyReplayData replayData, string filePath, bool no_encryption = false)
    {
        try
        {
            filePath += (no_encryption ? ".pcrpl2" : ".crpl2");
            ReplayCrp2Codec.ConvertToCompact(replayData, out CompactReplayData compact);
            byte[] body = ReplayCrp2Codec.CompactToOptimizedBinary(compact);
            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            {
                fs.Write(new byte[4] { 67, 82, 80, 50 }, 0, 4);
                fs.Write(BitConverter.GetBytes(2), 0, 4);
                if (!no_encryption)
                    body = ReplayCrypto.Encrypt(body);
                fs.Write(body, 0, body.Length);
            }
            Debug.Log("replay saved!");
        }
        catch (Exception ex)
        {
            Debug.Log(ex);
        }
        return File.Exists(filePath);
    }

    internal static void UnLoadReplay(out LegacyReplayData replayData)
    {
        replayData = new LegacyReplayData();
        WavLoader.loaded_clip = null;
        SpectreState.HasKeybdSound = false;
    }

    internal static bool LoadReplay(out LegacyReplayData replayData, string filePath, bool no_encryption = false)
    {
        replayData = new LegacyReplayData();
        if (!File.Exists(filePath))
        {
            Debug.Log("File not found: " + filePath);
            return false;
        }
        byte[] raw = File.ReadAllBytes(filePath);
        if (!no_encryption)
        {
            byte[] decrypted = ReplayCrypto.Decrypt(raw);
            if (decrypted == null)
            {
                Debug.Log("Decryption failed.");
                return false;
            }
            raw = decrypted;
        }
        try
        {
            string text = Encoding.UTF8.GetString(raw);
            text = NormalizeJsonFieldNames(text);
            ConfigData configData = JsonConvert.DeserializeObject<ConfigData>(text);
            if (configData?.CompactReplayFile != null)
            {
                ReplayCrp2Codec.ConvertFromCompact(configData.CompactReplayFile, out replayData);
            }
            else
            {
                LegacyReplayData direct = JsonConvert.DeserializeObject<LegacyReplayData>(text);
                if (direct != null)
                    replayData = direct;
                else
                {
                    Debug.Log("Error loading data: unrecognized JSON structure");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.Log("Error loading data: " + ex.Message);
            return false;
        }
        TryLoadKeybdSound(filePath, replayData);
        Debug.Log("replay load!");
        return true;
    }

    internal static bool LoadReplayAuto(out LegacyReplayData replayData, string filePath, bool no_encryption = false)
    {
        replayData = new LegacyReplayData();
        if (!File.Exists(filePath))
            return false;

        byte[] raw = File.ReadAllBytes(filePath);
        if (raw.Length < 8)
            return false;

        if (raw[0] == 67 && raw[1] == 82 && raw[2] == 80 && raw[3] == 50)
        {
            byte[] body = new byte[raw.Length - 8];
            Buffer.BlockCopy(raw, 8, body, 0, body.Length);
            if (!no_encryption)
            {
                body = ReplayCrypto.Decrypt(body);
                if (body == null) return false;
            }
            var compact = ReplayCrp2Codec.OptimizedBinaryToCompact(body);
            if (compact == null) return false;
            ReplayCrp2Codec.ConvertFromCompact(compact, out replayData);
            TryLoadKeybdSound(filePath, replayData);
            return true;
        }

        byte[] decrypted;
        if (no_encryption)
            decrypted = raw;
        else
        {
            decrypted = ReplayCrypto.Decrypt(raw);
            if (decrypted == null)
                return false;
        }

        try
        {
            var replay = ReplayDecoder.Decode(decrypted);
            replayData = ReplayDecoder.ConvertToOldFormat(replay);
            TryLoadKeybdSound(filePath, replayData);
            return true;
        }
        catch (Exception ex)
        {
            Debug.Log("New format load failed, trying old: " + ex.Message);
        }

        return LoadReplay(out replayData, filePath, no_encryption);
    }

    private static void TryLoadKeybdSound(string filePath, LegacyReplayData data)
    {
        if (data.strings.ContainsKey(KeybdSoundFileName) && data.strings.ContainsKey(KeybdSoundHash))
        {
            string dir = Path.GetDirectoryName(filePath);
            string wavPath = Path.Combine(dir, data.strings[KeybdSoundFileName]);
            WavLoader.loaded_clip = WavLoader.LoadFromFile(wavPath, data.strings[KeybdSoundHash]);
            SpectreState.HasKeybdSound = WavLoader.loaded_clip != null;
        }
    }

    internal static string NormalizeJsonFieldNames(string json)
    {
        return json
            .Replace("\"CompactCreplayfile\"", "\"CompactReplayFile\"")
            .Replace("\"key_event_list\"", "\"KeyEvent_list\"")
            .Replace("\"key_code\"", "\"KeyCode\"")
            .Replace("\"if_press\"", "\"IsPressed\"")
            .Replace("\"songposition\"", "\"SongPosition\"");
    }

    internal static string DemoteJsonFieldNames(string json)
    {
        return json
            .Replace("\"KeyEvent_list\"", "\"key_event_list\"")
            .Replace("\"KeyCode\"", "\"key_code\"")
            .Replace("\"IsPressed\"", "\"if_press\"")
            .Replace("\"SongPosition\"", "\"songposition\"");
    }
}
