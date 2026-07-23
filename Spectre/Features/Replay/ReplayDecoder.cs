using System;
using System.Globalization;
using static Spectre.Features.Replay.ReplayKeys;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Spectre.Features.Replay;

internal static class ReplayDecoder
{
    private static string GvS(Dictionary<string, string> d, string k)
    {
        if (d.TryGetValue(k, out var v) && v != null) return v;
        return "";
    }
    private static int GvI(Dictionary<string, int> d, string k) => d.ContainsKey(k) ? d[k] : 0;
    private static double GvD(Dictionary<string, double> d, string k) => d.ContainsKey(k) ? d[k] : 0.0;
    private static bool GvB(Dictionary<string, bool> d, string k) => d.ContainsKey(k) && d[k];

    public static Replay Decode(byte[] data)
    {
        var decompressed = Decompress(data);
        using var ms = new MemoryStream(decompressed);
        using var r = new BinaryReader(ms);

        var magic = r.ReadUInt64();
        if (magic != ReplayConstants.Magic && magic != ReplayConstants.LegacyMagic)
            throw new InvalidDataException($"Unknown magic: 0x{magic:X}");

        var version = r.ReadInt32();
        if (version > ReplayConstants.FormatVersion)
            throw new InvalidDataException($"Format version {version} too new");

        var replay = new Replay();
        replay.Metadata = ReadMetadata(r);
        if (version >= 2)
        {
            string endTimeStr = r.ReadString();
            if (!string.IsNullOrEmpty(endTimeStr))
                replay.EndTime = DateTimeOffset.Parse(endTimeStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }
        replay.KeyEvents = ReadKeyEvents(r);
        replay.HitContexts = ReadHitContexts(r);
        return replay;
    }

    public static bool IsNewFormat(byte[] data)
    {
        if (data.Length < 8) return false;
        var magic = BitConverter.ToUInt64(data, 0);
        return magic == ReplayConstants.Magic || magic == ReplayConstants.LegacyMagic;
    }

    private static byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private static Replay.ReplayMetadata ReadMetadata(BinaryReader r)
    {
        var magic = r.ReadUInt64();
        if (magic != ReplayConstants.MagicMetadata)
            throw new InvalidDataException($"Bad metadata magic: 0x{magic:X}");

        var ver = r.ReadInt32();
        var m = new Replay.ReplayMetadata
        {
            StartingFloorId = r.ReadInt32(),
            TotalFloorCount = r.ReadInt32(),
            SceneName = r.ReadString(),
            SongName = r.ReadString(),
            ArtistName = r.ReadString(),
            FileName = r.ReadString(),
            JudgeMode = r.ReadString(),
            LevelPath = r.ReadString(),
            InternalLevelName = r.ReadString(),
            HitMarginLimit = r.ReadString(),
            HoldBehavior = r.ReadString(),
            LoadedMods = r.ReadString(),
            DeviceID = r.ReadString(),
            ModVersion = r.ReadString(),
            KeybdSoundFileName = r.ReadString(),
            KeybdSoundHash = r.ReadString(),
            StartTile = r.ReadInt32(),
            FloorHash = r.ReadInt32(),
            SpeedHash = r.ReadInt32(),
            TimeHash = r.ReadInt32(),
            Pitch = r.ReadInt32(),
            LevelID = r.ReadInt32(),
            AudioBufferSize = r.ReadInt32(),
            Bpm = r.ReadDouble(),
            SpeedTrail = r.ReadDouble(),
            PlaybackSpeed = r.ReadDouble(),
            KeybdSoundStartTick = r.ReadDouble(),
            IsOfficialLevel = r.ReadBoolean(),
            SpeedTrailMode = r.ReadBoolean(),
            QuickPitched = r.ReadBoolean(),
            IfNoFail = r.ReadBoolean(),
        };
        var startTimeMs = r.ReadInt64();
        m.StartTime = startTimeMs == long.MinValue ? null : DateTimeOffset.FromUnixTimeMilliseconds(startTimeMs);
        m.SpVersion = r.ReadString();
        if (ver >= 2)
        {
            m.PercentXacc = r.ReadDouble();
            m.MaximumUsedKeys = r.ReadInt32();
            m.JudgmentList = r.ReadString();
        }
        return m;
    }

    private static List<KeyEvent> ReadKeyEvents(BinaryReader r)
    {
        var magic = r.ReadUInt64();
        if (magic != ReplayConstants.MagicKeyEvent)
            throw new InvalidDataException($"Bad key event magic: 0x{magic:X}");

        var ver = r.ReadInt32();
        var count = r.ReadInt32();
        var events = new List<KeyEvent>(count);
        for (int i = 0; i < count; i++)
            events.Add(new KeyEvent
            {
                SongPosition = r.ReadDouble(),
                KeyCode = r.ReadUInt16(),
                IsPressed = r.ReadBoolean()
            });
        return events;
    }

    private static List<HitContext> ReadHitContexts(BinaryReader r)
    {
        var magic = r.ReadUInt64();
        if (magic != ReplayConstants.MagicHitContext)
            throw new InvalidDataException($"Bad hit context magic: 0x{magic:X}");

        var ver = r.ReadInt32();
        var count = r.ReadInt32();
        var contexts = new List<HitContext>(count);
        for (int i = 0; i < count; i++)
            contexts.Add(new HitContext
            {
                CurrentFloorID = r.ReadInt32(),
                CurrAngle = r.ReadDouble(),
                OverloadCounter = r.ReadSingle(),
                NoFailHit = r.ReadBoolean(),
                IsAuto = r.ReadBoolean(),
                NextFloorAuto = r.ReadBoolean(),
                CachedAngle = r.ReadDouble(),
                TargetExitAngle = r.ReadDouble(),
                MidspinInfiniteMargin = r.ReadBoolean(),
                RDC_auto = r.ReadBoolean(),
                curFreeRoamSection = r.ReadInt32()
            });
        return contexts;
    }

    public static Replay ConvertFromOldFormat(LegacyReplayData old)
    {
        if (old == null) return null;
        var replay = new Replay();
        var m = replay.Metadata;

        m.SceneName = GvS(old.strings, SceneName);
        m.SongName = GvS(old.strings, SongName);
        m.ArtistName = GvS(old.strings, ArtistName);
        m.FileName = GvS(old.strings, FileName);
        m.JudgeMode = GvS(old.strings, JudgeMode);
        m.LevelPath = GvS(old.strings, LevelPath);
        m.InternalLevelName = GvS(old.strings, InternalLevelName);
        m.HitMarginLimit = GvS(old.strings, HitMarginLimitKey);
        m.HoldBehavior = GvS(old.strings, HoldBehaviorKey);
        m.LoadedMods = GvS(old.strings, LoadedMods);
        m.DeviceID = GvS(old.strings, DeviceID);
        m.ModVersion = GvS(old.strings, ModVersion);
        m.KeybdSoundFileName = GvS(old.strings, KeybdSoundFileName);
        m.KeybdSoundHash = GvS(old.strings, KeybdSoundHash);

        m.StartTile = GvI(old.ints, StartTile);
        m.TotalFloorCount = GvI(old.ints, EndTile) + 1;
        m.FloorHash = GvI(old.ints, FloorHash);
        m.SpeedHash = GvI(old.ints, SpeedHash);
        m.TimeHash = GvI(old.ints, TimeHash);
        m.Pitch = GvI(old.ints, Pitch);
        m.LevelID = GvI(old.ints, LevelID);
        m.AudioBufferSize = GvI(old.ints, AudioBufferSize);

        m.Bpm = GvD(old.doubles, Bpm);
        m.SpeedTrail = GvD(old.doubles, SpeedTrail);
        m.PlaybackSpeed = GvD(old.doubles, PlaybackSpeed);
        m.KeybdSoundStartTick = GvD(old.doubles, KeybdSoundStartTick);

        m.IsOfficialLevel = GvB(old.bools, IsOfficialLevel);
        m.SpeedTrailMode = GvB(old.bools, SpeedTrailMode);
        m.QuickPitched = GvB(old.bools, QuickPitched);
        m.IfNoFail = GvB(old.bools, IfNoFail);

        m.PercentXacc = GvD(old.doubles, PercentXacc);
        m.MaximumUsedKeys = GvI(old.ints, MaximumUsedKeys);
        m.JudgmentList = GvS(old.strings, JudgmentList);

        replay.KeyEvents = old.KeyEvent_list.ConvertAll(k => new KeyEvent
        {
            KeyCode = k.KeyCode,
            IsPressed = k.IsPressed,
            SongPosition = k.SongPosition
        });

        replay.HitContexts = old.HitContext_list;
        return replay;
    }

    public static LegacyReplayData ConvertToOldFormat(Replay replay)
    {
        var old = new LegacyReplayData();
        old.reset();
        var m = replay.Metadata;

        old.strings[SceneName] = m.SceneName;
        old.strings[SongName] = m.SongName;
        old.strings[ArtistName] = m.ArtistName;
        old.strings[FileName] = m.FileName;
        old.strings[JudgeMode] = m.JudgeMode;
        old.strings[LevelPath] = m.LevelPath;
        old.strings[InternalLevelName] = m.InternalLevelName;
        old.strings[HitMarginLimitKey] = m.HitMarginLimit;
        old.strings[HoldBehaviorKey] = m.HoldBehavior;
        old.strings[LoadedMods] = m.LoadedMods;
        old.strings[DeviceID] = m.DeviceID;
        old.strings[ModVersion] = m.ModVersion;
        old.strings[KeybdSoundFileName] = m.KeybdSoundFileName;
        old.strings[KeybdSoundHash] = m.KeybdSoundHash;

        old.ints[StartTile] = m.StartTile;
        old.ints[EndTile] = m.TotalFloorCount - 1;
        old.ints[FloorHash] = m.FloorHash;
        old.ints[SpeedHash] = m.SpeedHash;
        old.ints[TimeHash] = m.TimeHash;
        old.ints[Pitch] = m.Pitch;
        old.ints[LevelID] = m.LevelID;
        old.ints[AudioBufferSize] = m.AudioBufferSize;

        old.doubles[Bpm] = m.Bpm;
        old.doubles[SpeedTrail] = m.SpeedTrail;
        old.doubles[PlaybackSpeed] = m.PlaybackSpeed;
        old.doubles[KeybdSoundStartTick] = m.KeybdSoundStartTick;

        old.bools[IsOfficialLevel] = m.IsOfficialLevel;
        old.bools[SpeedTrailMode] = m.SpeedTrailMode;
        old.bools[QuickPitched] = m.QuickPitched;
        old.bools[IfNoFail] = m.IfNoFail;

        old.doubles[PercentXacc] = m.PercentXacc;
        old.ints[MaximumUsedKeys] = m.MaximumUsedKeys;
        old.strings[JudgmentList] = m.JudgmentList;

        foreach (var k in replay.KeyEvents)
            old.KeyEvent_list.Add(new KeyEvent
            {
                KeyCode = k.KeyCode,
                IsPressed = k.IsPressed,
                SongPosition = k.SongPosition
            });

        old.HitContext_list = replay.HitContexts;
        return old;
    }
}
