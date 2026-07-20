using System.Collections.Generic;
using System.Linq;

namespace Spectre.API;

public static class SpectreAPI
{
    public static Dictionary<string, string> BlockSources = new Dictionary<string, string>();

    public static bool IsPlayMode()
    {
        return SpectreState.PlayMode;
    }

    public static bool IsPlaying()
    {
        return SpectreState.is_playing;
    }

    public static bool IsRecordMode()
    {
        return SpectreState.RecordMode;
    }

    public static bool IsRecording()
    {
        return SpectreState.is_recording;
    }

    public static void BlockPlaying(string source, string msg)
    {
        SpectreState.PlayActions.StopPlaying();
        SpectreState.ApiBlockPlaying = true;
        if (BlockSources == null)
        {
            BlockSources = new Dictionary<string, string>();
        }
        if (msg != null)
        {
            BlockSources[source] = msg;
        }
        else
        {
            BlockSources[source] = "";
        }
    }

    public static void BlockPlaying(string msg)
    {
        SpectreState.PlayActions.StopPlaying();
        SpectreState.ApiBlockPlaying = true;
        if (BlockSources == null)
        {
            BlockSources = new Dictionary<string, string>();
        }
        if (msg != null)
        {
            BlockSources[""] = msg;
        }
        else
        {
            BlockSources[""] = "";
        }
    }

    public static void AllowPlaying(string source)
    {
        if (BlockSources == null)
        {
            BlockSources = new Dictionary<string, string>();
        }
        BlockSources.Remove(source);
        if (BlockSources.Count() == 0)
        {
            SpectreState.ApiBlockPlaying = false;
        }
    }

    public static void AllowPlaying()
    {
        if (BlockSources == null)
        {
            BlockSources = new Dictionary<string, string>();
        }
        BlockSources.Remove("");
        if (BlockSources.Count() == 0)
        {
            SpectreState.ApiBlockPlaying = false;
        }
    }
}
