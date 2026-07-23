using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Security.Cryptography;
using System.Text;
using ThreadPriority = System.Threading.ThreadPriority;
using Object = UnityEngine.Object;
using UnityEngine;

namespace Spectre;

public class AudioRecorder : MonoBehaviour
{
    private static AudioRecorder _instance;

    [Header("录制配置")]
    [SerializeField]
    private int recordingFrequency = 44100;

    [SerializeField]
    private int maxRecordingLength = 10;

    [SerializeField]
    private int chunkSize = 4096;

    private HashAlgorithm hashAlgorithm;

    private readonly object hashLock = new object();

    private bool isHashFinalized = false;

    private bool isRecording = false;

    internal AudioClip recordingClip;
    private static MethodInfo _getDataMethod;

    private string microphoneDevice;

    private Thread processingThread;

    private readonly object bufferLock = new object();

    private List<byte> wavDataBuffer = new List<byte>();

    private FileStream fileStream;

    private string currentFilePath;

    private long wavDataSizePosition;

    internal static AudioRecorder Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject val = new GameObject("AudioRecorder");
                _instance = val.AddComponent<AudioRecorder>();
                Object.DontDestroyOnLoad(val);
            }
            return _instance;
        }
    }

    internal string AudioHash { get; private set; }

    internal float ProcessingLatency { get; private set; }

    internal bool IsRecording => isRecording;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Object.Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
        }
    }

    internal bool StartRecording(string filePath = null, string deviceName = null, int duration = 10, int frequency = 44100)
    {
        if (isRecording)
        {
            Debug.Log("已经在录制中");
            return false;
        }
        if (Microphone.devices.Length == 0)
        {
            Debug.Log("没有找到可用的麦克风设备");
            return false;
        }
        try
        {
            lock (bufferLock)
            {
                wavDataBuffer.Clear();
            }
            fileStream = null;
            if (!string.IsNullOrEmpty(filePath))
            {
                processingThread = new Thread(InitializeWavFile)
                {
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal
                };
                processingThread.Start(filePath);
            }
            if (!Microphone.IsRecording(microphoneDevice))
            {
                Debug.Log("未能预先开始录制，可能存在卡顿");
                recordingFrequency = frequency;
                maxRecordingLength = duration;
                currentFilePath = filePath;
                startMicrophone(microphoneDevice);
            }
            if (!Microphone.IsRecording(microphoneDevice))
            {
                Debug.Log("开始录制失败！");
                return false;
            }
            isRecording = true;
            processingThread = new Thread(StreamProcessingLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            processingThread.Start();
            Debug.Log($"[AudioRecorder] 流式录制开始 - 设备: {microphoneDevice}, 采样率: {frequency}Hz");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            StopAllCoroutines();
            Cleanup();
            return false;
        }
    }

    private void StreamProcessingLoop()
    {
        float[] tempBuffer = new float[chunkSize];
        AudioClip obj = recordingClip;
        int channels = ((obj == null) ? 1 : obj.channels);
        int num = -1;
        while (isRecording && recordingClip != null)
        {
            DateTime now = DateTime.Now;
            int position = Microphone.GetPosition(microphoneDevice);
            if (num == -1)
            {
                num = position;
            }
            if (position < num)
            {
                ProcessRange(num, recordingClip.samples, tempBuffer, channels);
                ProcessRange(0, position, tempBuffer, channels);
            }
            else if (position > num)
            {
                ProcessRange(num, position, tempBuffer, channels);
            }
            num = position;
            ProcessingLatency = (float)(DateTime.Now - now).TotalMilliseconds;
            Thread.Sleep(10);
        }
    }

    private void ProcessRange(int startSample, int endSample, float[] tempBuffer, int channels)
    {
        int num = endSample - startSample;
        if (num > 0)
        {
            int num2 = startSample;
            while (num > 0)
            {
                int num3 = Mathf.Min(chunkSize, num);
                float[] array = new float[num3 * channels];
                if (_getDataMethod == null)
                    _getDataMethod = typeof(AudioClip).GetMethod("GetData", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(float[]), typeof(int) }, null);
                var clip = recordingClip;
                if (clip == null) break;
                _getDataMethod.Invoke(clip, new object[] { array, num2 });
                ProcessAudioChunk(array);
                num2 += num3;
                num -= num3;
            }
        }
    }

    private void ProcessAudioChunk(float[] samples)
    {
        byte[] array = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short num = (short)Mathf.Clamp(samples[i] * 32767f, -32768f, 32767f);
            array[i * 2] = (byte)(num & 0xFF);
            array[i * 2 + 1] = (byte)((num >> 8) & 0xFF);
        }
        lock (bufferLock)
        {
            if (fileStream != null && fileStream.CanWrite)
            {
                if (wavDataBuffer.Count != 0)
                {
                    byte[] array2 = wavDataBuffer.ToArray();
                    fileStream.Write(array2, 0, array2.Length);
                    lock (hashLock)
                    {
                        if (hashAlgorithm != null && !isHashFinalized)
                        {
                            hashAlgorithm.TransformBlock(array2, 0, array2.Length, null, 0);
                        }
                    }
                    wavDataBuffer.Clear();
                }
                fileStream.Write(array, 0, array.Length);
                lock (hashLock)
                {
                    if (hashAlgorithm != null && !isHashFinalized)
                    {
                        hashAlgorithm.TransformBlock(array, 0, array.Length, null, 0);
                    }
                }
            }
            else
            {
                wavDataBuffer.AddRange(array);
            }
        }
    }

    private void InitializeWavFile(object obj)
    {
        if (obj is string)
        {
            string path = (string)obj;
            string directoryName = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }
            fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            lock (hashLock)
            {
                hashAlgorithm = SHA256.Create();
                isHashFinalized = false;
                AudioHash = null;
            }
            byte[] array = CreateWAVHeader(1, recordingFrequency, 16, 0);
            fileStream.Write(array, 0, array.Length);
            wavDataSizePosition = fileStream.Position - 4;
        }
    }

    internal string StopRecording(bool End_microphone = false)
    {
        if (!isRecording)
        {
            return null;
        }
        isRecording = false;
        string text = null;
        if (processingThread != null && processingThread.IsAlive)
        {
            processingThread.Join(1000);
        }
        try
        {
            lock (bufferLock)
            {
                if (fileStream != null)
                {
                    text = FinalizeWavFile();
                }
            }
            Debug.Log(("[AudioRecorder] 录制停止，哈希: " + (text ?? "无")));
        }
        catch (Exception ex)
        {
            Debug.Log(("停止录制出错: " + ex.Message));
        }
        finally
        {
            Cleanup(End_microphone);
        }
        return text;
    }

    private string FinalizeWavFile()
    {
        if (fileStream == null)
        {
            return null;
        }
        long length = fileStream.Length;
        int num = (int)(length - 44);
        fileStream.Position = 4L;
        byte[] bytes = BitConverter.GetBytes((int)(length - 8));
        fileStream.Write(bytes, 0, 4);
        fileStream.Position = 40L;
        byte[] bytes2 = BitConverter.GetBytes(num);
        fileStream.Write(bytes2, 0, 4);
        fileStream.Close();
        fileStream = null;
        lock (hashLock)
        {
            if (hashAlgorithm != null && !isHashFinalized)
            {
                hashAlgorithm.TransformFinalBlock(new byte[0], 0, 0);
                byte[] hash = hashAlgorithm.Hash;
                AudioHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                isHashFinalized = true;
                Debug.Log($"[AudioRecorder] WAV文件已保存: {currentFilePath}, 大小: {num}字节, SHA256: {AudioHash}");
                return AudioHash;
            }
        }
        return "";
    }

    public static bool VerifyAudioFile(string filePath, string expectedHash)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError(("[AudioRecorder] 文件不存在: " + filePath));
            return false;
        }
        if (string.IsNullOrEmpty(expectedHash))
        {
            Debug.LogError("[AudioRecorder] 预期哈希值为空");
            return false;
        }
        try
        {
            using FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
            using SHA256 sHA = SHA256.Create();
            long length = fileStream.Length;
            long num = 44L;
            if (num > 0)
            {
                if (length <= num)
                {
                    Debug.LogError("[AudioRecorder] 文件太小，不包含音频数据");
                    return false;
                }
                fileStream.Position = num;
            }
            byte[] array = new byte[65536];
            long num2 = 0L;
            int num3;
            while ((num3 = fileStream.Read(array, 0, array.Length)) > 0)
            {
                sHA.TransformBlock(array, 0, num3, null, 0);
                num2 += num3;
            }
            sHA.TransformFinalBlock(new byte[0], 0, 0);
            string text = BitConverter.ToString(sHA.Hash).Replace("-", "").ToLowerInvariant();
            bool flag = text.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
            if (SpectreState.DebugMode)
            {
                Debug.Log(("[AudioRecorder] 文件验证: " + filePath + ", " + $"大小: {num2}字节, " + "预期: " + expectedHash + ", 实际: " + text + ", " + $"匹配: {flag}"));
            }
            return flag;
        }
        catch (Exception ex)
        {
            Debug.LogError(("[AudioRecorder] 验证文件时出错: " + ex.Message));
            return false;
        }
    }

    internal bool ValidateDevice(string deviceName)
    {
        if (string.IsNullOrEmpty(deviceName))
        {
            return false;
        }
        string[] devices = Microphone.devices;
        foreach (string text in devices)
        {
            if (text == deviceName)
            {
                return true;
            }
        }
        return false;
    }

    private void Cleanup(bool End_microphone = false)
    {
        isRecording = false;
        lock (hashLock)
        {
            if (hashAlgorithm != null)
            {
                if (!isHashFinalized)
                {
                    try
                    {
                        hashAlgorithm.TransformFinalBlock(new byte[0], 0, 0);
                    }
                    catch
                    {
                    }
                }
                hashAlgorithm.Dispose();
                hashAlgorithm = null;
            }
        }
        if (fileStream != null)
        {
            fileStream.Close();
            fileStream = null;
        }
        if (recordingClip != (UnityEngine.Object)null)
        {
            if (processingThread == null || !processingThread.IsAlive)
            {
                UnityEngine.Object.Destroy(recordingClip);
                recordingClip = null;
            }
        }
        if (End_microphone && !string.IsNullOrEmpty(microphoneDevice))
        {
            Microphone.End(microphoneDevice);
        }
        lock (bufferLock)
        {
            wavDataBuffer.Clear();
        }
        processingThread = null;
    }

    private byte[] CreateWAVHeader(int channels, int sampleRate, int bitDepth, int dataLength)
    {
        byte[] array = new byte[44];
        int value = sampleRate * channels * (bitDepth / 8);
        int num = channels * (bitDepth / 8);
        Encoding.ASCII.GetBytes("RIFF").CopyTo(array, 0);
        BitConverter.GetBytes(36 + dataLength).CopyTo(array, 4);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(array, 8);
        Encoding.ASCII.GetBytes("fmt ").CopyTo(array, 12);
        BitConverter.GetBytes(16).CopyTo(array, 16);
        BitConverter.GetBytes((short)1).CopyTo(array, 20);
        BitConverter.GetBytes((short)channels).CopyTo(array, 22);
        BitConverter.GetBytes(sampleRate).CopyTo(array, 24);
        BitConverter.GetBytes(value).CopyTo(array, 28);
        BitConverter.GetBytes((short)num).CopyTo(array, 32);
        BitConverter.GetBytes((short)bitDepth).CopyTo(array, 34);
        Encoding.ASCII.GetBytes("data").CopyTo(array, 36);
        BitConverter.GetBytes(dataLength).CopyTo(array, 40);
        return array;
    }

    internal void startMicrophone(string deviceName = null)
    {
        if (!ValidateDevice(deviceName))
        {
            Debug.Log("device name illegal");
            return;
        }
        microphoneDevice = deviceName;
        if (!Microphone.IsRecording(deviceName))
        {
            recordingClip = Microphone.Start(deviceName, true, maxRecordingLength, recordingFrequency);
            Debug.Log(("Recording " + deviceName));
        }
        if (!Microphone.IsRecording(deviceName))
        {
            Debug.Log("FAIL");
        }
    }

    internal static void StopAllMicrophones()
    {
        if (Microphone.devices.Length == 0)
        {
            return;
        }
        string[] devices = Microphone.devices;
        foreach (string text in devices)
        {
            if (Microphone.IsRecording(text))
            {
                Microphone.End(text);
                Debug.Log(("[AudioRecorder] 已停止麦克风: " + text));
            }
        }
    }

    private void OnDestroy()
    {
        if (isRecording)
        {
            StopRecording(End_microphone: true);
        }
    }
}
