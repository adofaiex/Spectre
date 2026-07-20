using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using static Spectre.Features.Replay.ReplayKeys;

namespace Spectre.Features.Replay;

internal static class ReplayManager
{
    public enum ReplaySaveFormat
    {
        New,   // sprp / psprp  (new GZip binary)
        Crp2,  // crpl2 / pcrpl2 (legacy CRP2 compact binary)
        Json   // crpl / pcrpl  (legacy JSON)
    }
    private class ConfigData
    {
        public CompactReplayData CompactReplayFile { get; set; }
    }

    public class CompactReplayData
    {
        public Dictionary<string, string> s = [];
        public Dictionary<string, bool> b = [];
        public Dictionary<string, int> i = [];
        public Dictionary<string, double> d = [];
        public List<ushort> keyCodes = [];
        public List<int> keyPresses = [];
        public List<double> keySongPositions = [];
        public List<int> hitCurrentFloorIDs = [];
        public List<double> hitCurrAngles = [];
        public List<float> hitOverloadCounters = [];
        public List<int> hitNoFailHits = [];
        public List<int> hitIsAutos = [];
        public List<int> hitNextFloorAutos = [];
        public List<double> hitCachedAngles = [];
        public List<double> hitTargetExitAngles = [];
        public List<int> hitMidspinInfiniteMargins = [];
        public List<int> hitRDCautos = [];
        public List<int> hitCurFreeRoamSections = [];

        public void reset()
        {
            s.Clear();
            b.Clear();
            i.Clear();
            d.Clear();
            keyCodes.Clear();
            keyPresses.Clear();
            keySongPositions.Clear();
            hitCurrentFloorIDs.Clear();
            hitCurrAngles.Clear();
            hitOverloadCounters.Clear();
            hitNoFailHits.Clear();
            hitIsAutos.Clear();
            hitNextFloorAutos.Clear();
            hitCachedAngles.Clear();
            hitTargetExitAngles.Clear();
            hitMidspinInfiniteMargins.Clear();
            hitRDCautos.Clear();
            hitCurFreeRoamSections.Clear();
        }
    }

    public class OptimizedReplayData
    {
        public int FormatVersion = 2;

        public Dictionary<string, string> s;

        public Dictionary<string, bool> b;

        public Dictionary<string, int> i;

        public Dictionary<string, double> d;

        public List<ushort> keyCodes;

        public List<int> keyPresses;

        public List<double> keySongPositions;

        public List<int> hitCurrentFloorIDs;

        public List<double> hitCurrAngles;

        public List<float> hitOverloadCounters;

        public List<double> hitCachedAngles;

        public List<double> hitTargetExitAngles;

        public List<int> hitCurFreeRoamSections;

        public List<byte> hitFlags;
    }

    [Flags]
    public enum HitContextFlags : byte
    {
        None = 0,
        NoFailHit = 1,
        IsAuto = 2,
        NextFloorAuto = 4,
        MidspinInfiniteMargin = 8,
        RDC_auto = 0x10
    }

    private static readonly string KeyStorageKey;

    private static readonly string IVStorageKey;

    private const int KeyLength = 32;

    private const int IVLength = 16;

    private static readonly byte[] Key;

    private static readonly byte[] IV;

    static ReplayManager()
    {
        KeyStorageKey = "qwerty";
        IVStorageKey = "potato";
        Key = GenerateKeyFromStorageKey(KeyStorageKey);
        IV = GenerateIVFromStorageKey(IVStorageKey);
    }

    private static byte[] GenerateKeyFromStorageKey(string storageKey)
    {
        using SHA256 sHA = SHA256.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(storageKey);
        byte[] array = sHA.ComputeHash(bytes);
        if (array.Length >= 32)
        {
            return array.Take(32).ToArray();
        }
        byte[] array2 = new byte[32];
        int num = array.Length;
        for (int i = 0; i < 32; i++)
        {
            array2[i] = array[i % num];
        }
        return array2;
    }

    private static byte[] GenerateIVFromStorageKey(string storageKey)
    {
        using SHA256 sHA = SHA256.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(storageKey);
        byte[] array = sHA.ComputeHash(bytes);
        if (array.Length >= 16)
        {
            return array.Take(16).ToArray();
        }
        byte[] array2 = new byte[16];
        int num = array.Length;
        for (int i = 0; i < 16; i++)
        {
            array2[i] = array[i % num];
        }
        return array2;
    }

    private static byte[] Encrypt(byte[] plainText, byte[] key, byte[] iv)
    {
        if (plainText == null)
        {
            Debug.Log("Encrypt Error: null input");
            return null;
        }
        try
        {
            using Aes aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            ICryptoTransform transform = aes.CreateEncryptor(aes.Key, aes.IV);
            using MemoryStream memoryStream = new MemoryStream();
            using (CryptoStream cryptoStream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Write))
            {
                cryptoStream.Write(plainText, 0, plainText.Length);
            }
            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            Debug.Log(("Encrypt Error: " + ex.Message));
            return null;
        }
    }

    private static byte[] Decrypt(byte[] cipherText, byte[] key, byte[] iv)
    {
        try
        {
            using Aes aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            ICryptoTransform transform = aes.CreateDecryptor(aes.Key, aes.IV);
            using MemoryStream memoryStream = new MemoryStream();
            using (CryptoStream cryptoStream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Write))
            {
                cryptoStream.Write(cipherText, 0, cipherText.Length);
            }
            return memoryStream.ToArray();
        }
        catch (CryptographicException ex)
        {
            Debug.Log(("Decrypt Error: " + ex.Message));
            return null;
        }
    }

    internal static string RemoveInvalidPathChars(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }
        char[] invalidPathChars = Path.GetInvalidPathChars();
        StringBuilder stringBuilder = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            bool flag = true;
            if (c < ' ' || c == '\u007f' || c == '/' || c == '\\' || c == ':' || c == '?' || c == '*' || c == '<' || c == '>' || c == '|' || c == '"' || c == '\n')
            {
                flag = false;
            }
            else
            {
                char[] array = invalidPathChars;
                foreach (char c2 in array)
                {
                    if (c == c2)
                    {
                        flag = false;
                        break;
                    }
                }
            }
            if (flag)
            {
                stringBuilder.Append(c);
            }
            else
            {
                stringBuilder.Append('_');
            }
        }
        return stringBuilder.ToString().Trim();
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
                data = Encrypt(data, Key, IV);

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
                data = Encrypt(data, Key, IV);

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
            Debug.Log(("File not found: " + filePath));
            return false;
        }
        byte[] array = File.ReadAllBytes(filePath);
        if (!no_encryption)
        {
            byte[] array2 = Decrypt(array, Key, IV);
            if (array2 == null)
            {
                Debug.Log("Decryption failed.");
                return false;
            }
            array = array2;
        }
        try
        {
            string text = Encoding.UTF8.GetString(array);
            text = NormalizeJsonFieldNames(text);
            ConfigData configData = JsonConvert.DeserializeObject<ConfigData>(text);
            if (configData?.CompactReplayFile != null)
            {
                ConvertFromCompact(configData.CompactReplayFile, out replayData);
            }
            else
            {
                LegacyReplayData direct = JsonConvert.DeserializeObject<LegacyReplayData>(text);
                if (direct != null)
                {
                    replayData = direct;
                }
                else
                {
                    Debug.Log("Error loading data: unrecognized JSON structure");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.Log(("Error loading data: " + ex.Message));
            return false;
        }
        if (replayData.strings.ContainsKey(KeybdSoundFileName) && replayData.strings.ContainsKey(KeybdSoundHash))
        {
            string directoryName = Path.GetDirectoryName(filePath);
            string filePath2 = Path.Combine(directoryName, replayData.strings[KeybdSoundFileName]);
            WavLoader.loaded_clip = WavLoader.LoadFromFile(filePath2, replayData.strings[KeybdSoundHash]);
            SpectreState.HasKeybdSound = WavLoader.loaded_clip != null;
        }
        Debug.Log("replay load!");
        return true;
    }

    private static string NormalizeJsonFieldNames(string json)
    {
        return json
            .Replace("\"CompactCreplayfile\"", "\"CompactReplayFile\"")
            .Replace("\"key_event_list\"", "\"KeyEvent_list\"")
            .Replace("\"key_code\"", "\"KeyCode\"")
            .Replace("\"if_press\"", "\"IsPressed\"")
            .Replace("\"songposition\"", "\"SongPosition\"");
    }

    private static string DemoteJsonFieldNames(string json)
    {
        return json
            .Replace("\"KeyEvent_list\"", "\"key_event_list\"")
            .Replace("\"KeyCode\"", "\"key_code\"")
            .Replace("\"IsPressed\"", "\"if_press\"")
            .Replace("\"SongPosition\"", "\"songposition\"");
    }

    internal static void ConvertToCompact(LegacyReplayData original, out CompactReplayData compact)
    {
        compact = new CompactReplayData();
        if (original == null)
        {
            Debug.Log("ConvertToCompact Error: null input");
            return;
        }
        compact.s = new Dictionary<string, string>(original.strings);
        compact.b = new Dictionary<string, bool>(original.bools);
        compact.i = new Dictionary<string, int>(original.ints);
        compact.d = new Dictionary<string, double>(original.doubles);
        foreach (KeyEvent item in original.KeyEvent_list)
        {
            compact.keyCodes.Add(item.KeyCode);
            compact.keyPresses.Add(item.IsPressed ? 1 : 0);
            compact.keySongPositions.Add(item.SongPosition);
        }
        foreach (HitContext item2 in original.HitContext_list)
        {
            compact.hitCurrentFloorIDs.Add(item2.CurrentFloorID);
            compact.hitCurrAngles.Add(item2.CurrAngle);
            compact.hitOverloadCounters.Add(item2.OverloadCounter);
            compact.hitNoFailHits.Add(item2.NoFailHit ? 1 : 0);
            compact.hitIsAutos.Add(item2.IsAuto ? 1 : 0);
            compact.hitNextFloorAutos.Add(item2.NextFloorAuto ? 1 : 0);
            compact.hitCachedAngles.Add(item2.CachedAngle);
            compact.hitTargetExitAngles.Add(item2.TargetExitAngle);
            compact.hitMidspinInfiniteMargins.Add(item2.MidspinInfiniteMargin ? 1 : 0);
            compact.hitRDCautos.Add(item2.RDC_auto ? 1 : 0);
            compact.hitCurFreeRoamSections.Add(item2.curFreeRoamSection);
        }
    }

    internal static void ConvertFromCompact(CompactReplayData compact, out LegacyReplayData original)
    {
        original = new LegacyReplayData();
        original.reset();
        if (compact != null)
        {
            original.strings = new Dictionary<string, string>(compact.s);
            original.bools = new Dictionary<string, bool>(compact.b);
            original.ints = new Dictionary<string, int>(compact.i);
            original.doubles = new Dictionary<string, double>(compact.d);
            for (int i = 0; i < compact.keyCodes.Count; i++)
            {
                original.KeyEvent_list.Add(new KeyEvent
                {
                    KeyCode = compact.keyCodes[i],
                    IsPressed = (compact.keyPresses[i] == 1),
                    SongPosition = compact.keySongPositions[i]
                });
            }
            for (int j = 0; j < compact.hitCurrentFloorIDs.Count; j++)
            {
                original.HitContext_list.Add(new HitContext
                {
                    CurrentFloorID = compact.hitCurrentFloorIDs[j],
                    CurrAngle = compact.hitCurrAngles[j],
                    OverloadCounter = compact.hitOverloadCounters[j],
                    NoFailHit = (compact.hitNoFailHits[j] == 1),
                    IsAuto = (compact.hitIsAutos[j] == 1),
                    NextFloorAuto = (compact.hitNextFloorAutos[j] == 1),
                    CachedAngle = compact.hitCachedAngles[j],
                    TargetExitAngle = compact.hitTargetExitAngles[j],
                    MidspinInfiniteMargin = (compact.hitMidspinInfiniteMargins[j] == 1),
                    RDC_auto = (compact.hitRDCautos[j] == 1),
                    curFreeRoamSection = compact.hitCurFreeRoamSections[j]
                });
            }
        }
    }

    private static byte[] SerializeOptimizedData(OptimizedReplayData data)
    {
        using MemoryStream memoryStream = new MemoryStream();
        using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
        binaryWriter.Write(data.FormatVersion);
        WriteStringDictionary(binaryWriter, data.s);
        WriteBoolDictionary(binaryWriter, data.b);
        WriteIntDictionary(binaryWriter, data.i);
        WriteDoubleDictionary(binaryWriter, data.d);
        WriteUshortList(binaryWriter, data.keyCodes);
        WriteIntList(binaryWriter, data.keyPresses);
        WriteDoubleList(binaryWriter, data.keySongPositions);
        WriteIntList(binaryWriter, data.hitCurrentFloorIDs);
        WriteDoubleList(binaryWriter, data.hitCurrAngles);
        WriteFloatList(binaryWriter, data.hitOverloadCounters);
        WriteDoubleList(binaryWriter, data.hitCachedAngles);
        WriteDoubleList(binaryWriter, data.hitTargetExitAngles);
        WriteIntList(binaryWriter, data.hitCurFreeRoamSections);
        WriteByteList(binaryWriter, data.hitFlags);
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }

    private static OptimizedReplayData DeserializeOptimizedData(byte[] bytes)
    {
        using MemoryStream input = new MemoryStream(bytes);
        using BinaryReader binaryReader = new BinaryReader(input);
        OptimizedReplayData optimizedReplayData = new OptimizedReplayData();
        optimizedReplayData.FormatVersion = binaryReader.ReadInt32();
        optimizedReplayData.s = ReadStringDictionary(binaryReader);
        optimizedReplayData.b = ReadBoolDictionary(binaryReader);
        optimizedReplayData.i = ReadIntDictionary(binaryReader);
        optimizedReplayData.d = ReadDoubleDictionary(binaryReader);
        optimizedReplayData.keyCodes = ReadUshortList(binaryReader);
        optimizedReplayData.keyPresses = ReadIntList(binaryReader);
        optimizedReplayData.keySongPositions = ReadDoubleList(binaryReader);
        optimizedReplayData.hitCurrentFloorIDs = ReadIntList(binaryReader);
        optimizedReplayData.hitCurrAngles = ReadDoubleList(binaryReader);
        optimizedReplayData.hitOverloadCounters = ReadFloatList(binaryReader);
        optimizedReplayData.hitCachedAngles = ReadDoubleList(binaryReader);
        optimizedReplayData.hitTargetExitAngles = ReadDoubleList(binaryReader);
        optimizedReplayData.hitCurFreeRoamSections = ReadIntList(binaryReader);
        optimizedReplayData.hitFlags = ReadByteList(binaryReader);
        return optimizedReplayData;
    }

    private static void WriteStringDictionary(BinaryWriter writer, Dictionary<string, string> dict)
    {
        writer.Write(dict?.Count ?? 0);
        if (dict == null)
        {
            return;
        }
        foreach (KeyValuePair<string, string> item in dict)
        {
            writer.Write(item.Key ?? "");
            writer.Write(item.Value ?? "");
        }
    }

    private static void WriteBoolDictionary(BinaryWriter writer, Dictionary<string, bool> dict)
    {
        writer.Write(dict?.Count ?? 0);
        if (dict == null)
        {
            return;
        }
        foreach (KeyValuePair<string, bool> item in dict)
        {
            writer.Write(item.Key ?? "");
            writer.Write(item.Value);
        }
    }

    private static void WriteIntDictionary(BinaryWriter writer, Dictionary<string, int> dict)
    {
        writer.Write(dict?.Count ?? 0);
        if (dict == null)
        {
            return;
        }
        foreach (KeyValuePair<string, int> item in dict)
        {
            writer.Write(item.Key ?? "");
            writer.Write(item.Value);
        }
    }

    private static void WriteDoubleDictionary(BinaryWriter writer, Dictionary<string, double> dict)
    {
        writer.Write(dict?.Count ?? 0);
        if (dict == null)
        {
            return;
        }
        foreach (KeyValuePair<string, double> item in dict)
        {
            writer.Write(item.Key ?? "");
            writer.Write(item.Value);
        }
    }

    private static Dictionary<string, string> ReadStringDictionary(BinaryReader reader)
    {
        int num = reader.ReadInt32();
        Dictionary<string, string> dictionary = new Dictionary<string, string>();
        for (int i = 0; i < num; i++)
        {
            string key = reader.ReadString();
            string value = reader.ReadString();
            dictionary[key] = value;
        }
        return dictionary;
    }

    private static Dictionary<string, bool> ReadBoolDictionary(BinaryReader reader)
    {
        int num = reader.ReadInt32();
        Dictionary<string, bool> dictionary = new Dictionary<string, bool>();
        for (int i = 0; i < num; i++)
        {
            string key = reader.ReadString();
            bool value = reader.ReadBoolean();
            dictionary[key] = value;
        }
        return dictionary;
    }

    private static Dictionary<string, int> ReadIntDictionary(BinaryReader reader)
    {
        int num = reader.ReadInt32();
        Dictionary<string, int> dictionary = new Dictionary<string, int>();
        for (int i = 0; i < num; i++)
        {
            string key = reader.ReadString();
            int value = reader.ReadInt32();
            dictionary[key] = value;
        }
        return dictionary;
    }

    private static Dictionary<string, double> ReadDoubleDictionary(BinaryReader reader)
    {
        int num = reader.ReadInt32();
        Dictionary<string, double> dictionary = new Dictionary<string, double>();
        for (int i = 0; i < num; i++)
        {
            string key = reader.ReadString();
            double value = reader.ReadDouble();
            dictionary[key] = value;
        }
        return dictionary;
    }

    private static void WriteUshortList(BinaryWriter writer, List<ushort> list)
    {
        writer.Write(list?.Count ?? 0);
        if (list == null)
        {
            return;
        }
        foreach (ushort item in list)
        {
            writer.Write(item);
        }
    }

    private static void WriteIntList(BinaryWriter writer, List<int> list)
    {
        writer.Write(list?.Count ?? 0);
        if (list == null)
        {
            return;
        }
        foreach (int item in list)
        {
            writer.Write(item);
        }
    }

    private static void WriteDoubleList(BinaryWriter writer, List<double> list)
    {
        writer.Write(list?.Count ?? 0);
        if (list == null)
        {
            return;
        }
        foreach (double item in list)
        {
            writer.Write(item);
        }
    }

    private static void WriteFloatList(BinaryWriter writer, List<float> list)
    {
        writer.Write(list?.Count ?? 0);
        if (list == null)
        {
            return;
        }
        foreach (float item in list)
        {
            writer.Write(item);
        }
    }

    private static void WriteByteList(BinaryWriter writer, List<byte> list)
    {
        writer.Write(list?.Count ?? 0);
        if (list == null)
        {
            return;
        }
        foreach (byte item in list)
        {
            writer.Write(item);
        }
    }

    private static List<ushort> ReadUshortList(BinaryReader reader)
    {
        int num = reader.ReadInt32();
        List<ushort> list = new List<ushort>(num);
        for (int i = 0; i < num; i++)
        {
            list.Add(reader.ReadUInt16());
        }
        return list;
    }

    private static List<int> ReadIntList(BinaryReader reader)
    {
        int num = reader.ReadInt32();
        List<int> list = new List<int>(num);
        for (int i = 0; i < num; i++)
        {
            list.Add(reader.ReadInt32());
        }
        return list;
    }

    private static List<double> ReadDoubleList(BinaryReader reader)
    {
        int num = reader.ReadInt32();
        List<double> list = new List<double>(num);
        for (int i = 0; i < num; i++)
        {
            list.Add(reader.ReadDouble());
        }
        return list;
    }

    private static List<float> ReadFloatList(BinaryReader reader)
    {
        int num = reader.ReadInt32();
        List<float> list = new List<float>(num);
        for (int i = 0; i < num; i++)
        {
            list.Add(reader.ReadSingle());
        }
        return list;
    }

    private static List<byte> ReadByteList(BinaryReader reader)
    {
        int num = reader.ReadInt32();
        List<byte> list = new List<byte>(num);
        for (int i = 0; i < num; i++)
        {
            list.Add(reader.ReadByte());
        }
        return list;
    }

    internal static byte[] CompactToOptimizedBinary(CompactReplayData compact)
    {
        if (compact == null)
        {
            Debug.Log("CompactToOptimizedBinary Error: null input");
            return null;
        }
        OptimizedReplayData optimizedReplayData = new OptimizedReplayData
        {
            FormatVersion = 2,
            s = compact.s,
            b = compact.b,
            i = compact.i,
            d = compact.d,
            keyCodes = compact.keyCodes,
            keyPresses = compact.keyPresses,
            keySongPositions = compact.keySongPositions,
            hitCurrentFloorIDs = compact.hitCurrentFloorIDs,
            hitCurrAngles = compact.hitCurrAngles,
            hitOverloadCounters = compact.hitOverloadCounters,
            hitCachedAngles = compact.hitCachedAngles,
            hitTargetExitAngles = compact.hitTargetExitAngles,
            hitCurFreeRoamSections = compact.hitCurFreeRoamSections,
            hitFlags = new List<byte>(compact.hitCurrentFloorIDs.Count)
        };
        for (int i = 0; i < compact.hitCurrentFloorIDs.Count; i++)
        {
            HitContextFlags hitContextFlags = HitContextFlags.None;
            if (compact.hitNoFailHits[i] == 1)
            {
                hitContextFlags |= HitContextFlags.NoFailHit;
            }
            if (compact.hitIsAutos[i] == 1)
            {
                hitContextFlags |= HitContextFlags.IsAuto;
            }
            if (compact.hitNextFloorAutos[i] == 1)
            {
                hitContextFlags |= HitContextFlags.NextFloorAuto;
            }
            if (compact.hitMidspinInfiniteMargins[i] == 1)
            {
                hitContextFlags |= HitContextFlags.MidspinInfiniteMargin;
            }
            if (compact.hitRDCautos[i] == 1)
            {
                hitContextFlags |= HitContextFlags.RDC_auto;
            }
            optimizedReplayData.hitFlags.Add((byte)hitContextFlags);
        }
        return SerializeOptimizedData(optimizedReplayData);
    }

    internal static CompactReplayData OptimizedBinaryToCompact(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            Debug.Log("OptimizedBinaryToCompact Error: null or empty input");
            return null;
        }
        try
        {
            OptimizedReplayData optimizedReplayData = DeserializeOptimizedData(data);
            if (optimizedReplayData.FormatVersion != 2)
            {
                Debug.Log($"Warning: Unexpected format version {optimizedReplayData.FormatVersion}");
            }
            CompactReplayData compact = new CompactReplayData();
            compact.s = optimizedReplayData.s ?? new Dictionary<string, string>();
            compact.b = optimizedReplayData.b ?? new Dictionary<string, bool>();
            compact.i = optimizedReplayData.i ?? new Dictionary<string, int>();
            compact.d = optimizedReplayData.d ?? new Dictionary<string, double>();
            compact.keyCodes = optimizedReplayData.keyCodes ?? new List<ushort>();
            compact.keyPresses = optimizedReplayData.keyPresses ?? new List<int>();
            compact.keySongPositions = optimizedReplayData.keySongPositions ?? new List<double>();
            compact.hitCurrentFloorIDs = optimizedReplayData.hitCurrentFloorIDs ?? new List<int>();
            compact.hitCurrAngles = optimizedReplayData.hitCurrAngles ?? new List<double>();
            compact.hitOverloadCounters = optimizedReplayData.hitOverloadCounters ?? new List<float>();
            compact.hitCachedAngles = optimizedReplayData.hitCachedAngles ?? new List<double>();
            compact.hitTargetExitAngles = optimizedReplayData.hitTargetExitAngles ?? new List<double>();
            compact.hitCurFreeRoamSections = optimizedReplayData.hitCurFreeRoamSections ?? new List<int>();
            int count = compact.hitCurrentFloorIDs.Count;
            compact.hitNoFailHits = new List<int>(count);
            compact.hitIsAutos = new List<int>(count);
            compact.hitNextFloorAutos = new List<int>(count);
            compact.hitMidspinInfiniteMargins = new List<int>(count);
            compact.hitRDCautos = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                byte b = (byte)((i < optimizedReplayData.hitFlags.Count) ? optimizedReplayData.hitFlags[i] : 0);
                HitContextFlags hitContextFlags = (HitContextFlags)b;
                compact.hitNoFailHits.Add(hitContextFlags.HasFlag(HitContextFlags.NoFailHit) ? 1 : 0);
                compact.hitIsAutos.Add(hitContextFlags.HasFlag(HitContextFlags.IsAuto) ? 1 : 0);
                compact.hitNextFloorAutos.Add(hitContextFlags.HasFlag(HitContextFlags.NextFloorAuto) ? 1 : 0);
                compact.hitMidspinInfiniteMargins.Add(hitContextFlags.HasFlag(HitContextFlags.MidspinInfiniteMargin) ? 1 : 0);
                compact.hitRDCautos.Add(hitContextFlags.HasFlag(HitContextFlags.RDC_auto) ? 1 : 0);
            }
            return compact;
        }
        catch (Exception ex)
        {
            Debug.Log(("OptimizedBinaryToCompact Error: " + ex.Message));
            return null;
        }
    }

    internal static bool SaveReplayOptimized(LegacyReplayData replayData, string filePath, bool no_encryption = false)
    {
        try
        {
            filePath += (no_encryption ? ".pcrpl2" : ".crpl2");
            ConvertToCompact(replayData, out CompactReplayData compact);
            byte[] array = CompactToOptimizedBinary(compact);
            using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
            {
                fileStream.Write(new byte[4] { 67, 82, 80, 50 }, 0, 4);
                fileStream.Write(BitConverter.GetBytes(2), 0, 4);
                if (!no_encryption)
                {
                    array = Encrypt(array, Key, IV);
                }
                fileStream.Write(array, 0, array.Length);
            }
            Debug.Log("replay saved!");
        }
        catch (Exception ex)
        {
            Debug.Log(ex);
        }
        return File.Exists(filePath);
    }

    internal static bool LoadReplayAuto(out LegacyReplayData replayData, string filePath, bool no_encryption = false)
    {
        replayData = new LegacyReplayData();
        if (!File.Exists(filePath))
            return false;

        byte[] raw = File.ReadAllBytes(filePath);
        if (raw.Length < 8)
            return false;

        // Try CRP2 format first (magic "CRP2" at offset 0 in the raw file)
        if (raw[0] == 67 && raw[1] == 82 && raw[2] == 80 && raw[3] == 50)
        {
            byte[] body = new byte[raw.Length - 8];
            Buffer.BlockCopy(raw, 8, body, 0, body.Length);
            if (!no_encryption)
            {
                body = Decrypt(body, Key, IV);
                if (body == null) return false;
            }
            var compact = OptimizedBinaryToCompact(body);
            if (compact == null) return false;
            ConvertFromCompact(compact, out replayData);
            TryLoadKeybdSound(filePath, replayData);
            return true;
        }

        // For non-CRP2 files, decrypt the whole file
        byte[] decrypted;
        if (no_encryption)
            decrypted = raw;
        else
        {
            decrypted = Decrypt(raw, Key, IV);
            if (decrypted == null)
                return false;
        }

        // Try new format (GZip compressed binary)
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

        // Old JSON format
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
}
