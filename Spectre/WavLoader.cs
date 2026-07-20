using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spectre;

public static class WavLoader
{
    private struct WavHeader
    {
        public int Channels;

        public int SampleRate;

        public int BitsPerSample;

        public int DataOffset;

        public int DataSize;

        public int TotalSamples;

        public bool IsValid => Channels > 0 && SampleRate > 0 && (BitsPerSample == 8 || BitsPerSample == 16 || BitsPerSample == 24 || BitsPerSample == 32);
    }

    public static AudioClip loaded_clip;

    internal static AudioSource musicSource;

    internal static GameObject audioGO;

    internal static AudioClip LoadFromFile(string filePath, string expectedHash = null, bool streamAudio = false)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError(("[WavLoader] 文件不存在: " + filePath));
            return null;
        }
        if (!SpectreState.DebugMode || !SpectreState.debug_skip_Verification)
        {
            if (!string.IsNullOrEmpty(expectedHash))
            {
                if (!AudioRecorder.VerifyAudioFile(filePath, expectedHash))
                {
                    Debug.LogError(("[WavLoader] 文件哈希验证失败: " + filePath));
                    return null;
                }
                Debug.Log(("[WavLoader] 文件哈希验证通过: " + filePath));
            }
        }
        else
        {
            Debug.Log("Debug模式跳过音频校验");
        }
        try
        {
            byte[] wavData = File.ReadAllBytes(filePath);
            return LoadFromMemory(wavData, Path.GetFileNameWithoutExtension(filePath), streamAudio);
        }
        catch (Exception ex)
        {
            Debug.LogError(("[WavLoader] 读取文件失败: " + ex.Message));
            return null;
        }
    }

    internal static AudioClip LoadFromMemory(byte[] wavData, string clipName = "WAV_Audio", bool streamAudio = false)
    {
        if (wavData == null || wavData.Length < 44)
        {
            Debug.LogError("[WavLoader] 数据无效或过小");
            return null;
        }
        try
        {
            WavHeader header = ParseHeader(wavData);
            if (!header.IsValid)
            {
                Debug.LogError("[WavLoader] WAV 头部解析失败");
                return null;
            }
            Debug.Log($"[WavLoader] 加载 WAV: {header.Channels}ch, {header.SampleRate}Hz, {header.BitsPerSample}bit, {header.TotalSamples} 样本");
            float[] array = ConvertWavToFloat(wavData, header);
            AudioClip val = AudioClip.Create(clipName, header.TotalSamples, header.Channels, header.SampleRate, streamAudio);
            var setDataMethod = typeof(AudioClip).GetMethod("SetData", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(float[]), typeof(int) }, null);
            if (!(bool)setDataMethod.Invoke(val, new object[] { array, 0 }))
            {
                Debug.LogError("[WavLoader] SetData 失败");
                Object.Destroy(val);
                return null;
            }
            return val;
        }
        catch (Exception ex)
        {
            Debug.LogError(("[WavLoader] 加载失败: " + ex.Message));
            return null;
        }
    }

    private static WavHeader ParseHeader(byte[] data)
    {
        WavHeader result = default(WavHeader);
        using (MemoryStream memoryStream = new MemoryStream(data))
        {
            using BinaryReader binaryReader = new BinaryReader(memoryStream);
            string text = new string(binaryReader.ReadChars(4));
            if (text != "RIFF")
            {
                Debug.LogError(("[WavLoader] 不是 RIFF 格式: " + text));
                return result;
            }
            binaryReader.ReadInt32();
            string text2 = new string(binaryReader.ReadChars(4));
            if (text2 != "WAVE")
            {
                Debug.LogError(("[WavLoader] 不是 WAVE 格式: " + text2));
                return result;
            }
            while (memoryStream.Position < memoryStream.Length - 8)
            {
                string text3 = new string(binaryReader.ReadChars(4));
                int num = binaryReader.ReadInt32();
                if (text3 == "fmt ")
                {
                    int num2 = binaryReader.ReadInt16();
                    if (num2 != 1 && num2 != 3)
                    {
                        Debug.LogError($"[WavLoader] 不支持的音频格式: {num2}（仅支持 PCM）");
                        return result;
                    }
                    result.Channels = binaryReader.ReadInt16();
                    result.SampleRate = binaryReader.ReadInt32();
                    binaryReader.ReadInt32();
                    binaryReader.ReadInt16();
                    result.BitsPerSample = binaryReader.ReadInt16();
                    if (num > 16)
                    {
                        binaryReader.ReadBytes(num - 16);
                    }
                }
                else
                {
                    if (text3 == "data")
                    {
                        result.DataSize = num;
                        result.DataOffset = (int)memoryStream.Position;
                        result.TotalSamples = num / (result.Channels * (result.BitsPerSample / 8));
                        break;
                    }
                    binaryReader.ReadBytes(num);
                }
            }
        }
        return result;
    }

    private static float[] ConvertWavToFloat(byte[] wavData, WavHeader header)
    {
        int num = header.BitsPerSample / 8;
        int num2 = header.DataSize / num;
        float[] array = new float[num2];
        for (int i = 0; i < num2; i++)
        {
            int num3 = header.DataOffset + i * num;
            if (num3 + num > wavData.Length)
            {
                break;
            }
            switch (header.BitsPerSample)
            {
                case 8:
                    array[i] = (float)(wavData[num3] - 128) / 128f;
                    break;
                case 16:
                    {
                        short num5 = (short)(wavData[num3] | (wavData[num3 + 1] << 8));
                        array[i] = (float)num5 / 32768f;
                        break;
                    }
                case 24:
                    {
                        int num6 = wavData[num3] | (wavData[num3 + 1] << 8) | (wavData[num3 + 2] << 16);
                        if ((num6 & 0x800000) != 0)
                        {
                            num6 |= -16777216;
                        }
                        array[i] = (float)num6 / 8388608f;
                        break;
                    }
                case 32:
                    {
                        if (header.DataSize / header.TotalSamples == 4 * header.Channels)
                        {
                            array[i] = BitConverter.ToSingle(wavData, num3);
                            break;
                        }
                        int num4 = BitConverter.ToInt32(wavData, num3);
                        array[i] = (float)num4 / 2.1474836E+09f;
                        break;
                    }
                default:
                    array[i] = 0f;
                    break;
            }
        }
        return array;
    }

    private static float[] ConvertBytesToFloat(byte[] data, int length, int bitsPerSample, int channels)
    {
        int num = bitsPerSample / 8;
        int num2 = length / num;
        float[] array = new float[num2];
        for (int i = 0; i < num2; i++)
        {
            int num3 = i * num;
            if (num3 + num > length)
            {
                break;
            }
            switch (bitsPerSample)
            {
                case 16:
                    {
                        short num5 = (short)(data[num3] | (data[num3 + 1] << 8));
                        array[i] = (float)num5 / 32768f;
                        break;
                    }
                case 24:
                    {
                        int num4 = data[num3] | (data[num3 + 1] << 8) | (data[num3 + 2] << 16);
                        if ((num4 & 0x800000) != 0)
                        {
                            num4 |= -16777216;
                        }
                        array[i] = (float)num4 / 8388608f;
                        break;
                    }
                case 32:
                    array[i] = BitConverter.ToSingle(data, num3);
                    break;
                default:
                    array[i] = (float)(data[num3] - 128) / 128f;
                    break;
            }
        }
        return array;
    }

    internal static void Play_keybdsound(AudioClip snd, float volume)
    {
        musicSource.clip = snd;
        musicSource.volume = volume;
        musicSource.Play();
        Debug.Log("开始播放！");
    }

    internal static void Stop_keybdsound()
    {
        musicSource.Stop();
    }

    internal static void reset()
    {
        if (musicSource == null)
        {
            audioGO = new GameObject("Spectre_AudioSource");
            audioGO.hideFlags = (HideFlags)61;
            Object.DontDestroyOnLoad(audioGO);
            musicSource = audioGO.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
        }
    }
}
