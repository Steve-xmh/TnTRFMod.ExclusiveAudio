using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using TnTRFMod.ExclusiveAudio.Wasapi;
using AudioClientShareMode = TnTRFMod.ExclusiveAudio.Wasapi.AudioClientShareMode;
using AudioClientStreamFlags = TnTRFMod.ExclusiveAudio.Wasapi.AudioClientStreamFlags;

namespace TnTRFMod.ExclusiveAudio;

[SuppressMessage("Interoperability", "CA1416")]
public static class CriWareEnableExclusiveModePatch
{
    private const string CriWarePluginName = "Taiko no Tatsujin Rhythm Festival_Data/Plugins/x86_64/cri_ware_unity.dll";

    private static WaveFormat? mixFormat;
    private static WaveFormat? configuredFormat;
    private static TimeSpan bufferDuration = TimeSpan.Zero;

    private static CriWarePluginNative.IAudioClientInitializeHook? AudioClientInitializeHook_Original;
    private static CriWarePluginNative.IAudioRenderClientReleaseBufferHook? AudioRenderClientReleaseBufferHook_Original;
    private static CriWarePluginNative.IAudioRenderClientGetBufferHook? AudioRenderClientGetBufferHook_Original;

    private static bool showedUnsupportedError;

    private static TimeSpan? calibratedBufferDuration;
    private static readonly Guid PCM_SUBFORMAT = new("00000001-0000-0010-8000-00AA00389B71");

    private static IntPtr lastAudioRenderClientBuffer = IntPtr.Zero;

    public static event Action<OnAudioDataArgs>? OnAudioData;

    public static void Apply()
    {
        Logger.Info("Starting CriWareEnableExclusiveModePatch");
        CriWarePluginNative.CriAtomWASAPI.SetAudioClientShareMode(AudioClientShareMode.Shared); // Load dll

        if (!CheckWaveFormat()) return;
        if (ExclusiveAudioPlugin.Instance.ConfigEnableCriWarePluginLogging.Value) EnableCriWareLogging();

        var targetFormat = GetWaveFormat();
        Logger.Info("Configured exclusive audio format:");
        PrintWaveFormatInfo(ref targetFormat);
        Logger.Info($"Buffer duration: {bufferDuration.TotalMilliseconds}ms");

        CriWarePluginNative.CriAtomWASAPI.SetAudioClientShareMode(AudioClientShareMode.Exclusive);
        CriWarePluginNative.CriAtomWASAPI.SetAudioClientBufferDuration(bufferDuration);
        CriWarePluginNative.CriAtomWASAPI.SetAudioClientFormat(targetFormat.MarshalToPtr());

        Logger.Message("Exclusive audio client feature is ready, waiting for initialization");
    }

    private static void EnableCriWareLogging()
    {
        var handle = GetModuleHandle("cri_ware_unity.dll");
        var logCallbackPtr = new IntPtr((long)handle + 0x1802273F0L - 0x180000000L);

        WriteMemory(logCallbackPtr, Marshal.GetFunctionPointerForDelegate(
            new CriWarePluginNative.OnCriAtomUnityLog((buffer, _, info, _) =>
                {
                    var data1 = info.data1.ToString("X8");
                    var data2 = info.data2.ToString("X8");
                    var data3 = info.data3.ToString("X8");

                    Logger.Info(
                        $"[CriWareUnity] {buffer} ({data1}, {data2}, {data3})");
                }
            )));
    }

    private static bool CheckWaveFormat()
    {
        // Ensure that we can enable this mode.

        var format = GetWaveFormat();

        IAudioClient3? audioClient = null;
        try
        {
            var IID_IAudioClient = typeof(IAudioClient3).GUID;

            var realEnumerator = MMDeviceEnumeratorFactory.CreateInstance();
            realEnumerator.GetDefaultAudioEndpoint(0, 0, out var device);

            if (device == null)
            {
                Logger.Error("Failed to get default audio endpoint, exclusive audio feature is disabled!");
                return false;
            }

            device.Activate(ref IID_IAudioClient, ClsCtx.ALL, IntPtr.Zero,
                out var audioClient3);
            audioClient = audioClient3 as IAudioClient3;
            if (audioClient == null)
            {
                Logger.Error("Failed to activate IAudioClient3, exclusive audio feature is disabled!");
                return false;
            }

            audioClient.GetMixFormat(out var mixFormatPtr);
            mixFormat = WaveFormat.MarshalFromPtr(mixFormatPtr);
            Logger.Info("Shared mode mix format:");
            PrintWaveFormatInfo(ref mixFormat);

            audioClient.GetDevicePeriod(out _, out var period);
            bufferDuration = new TimeSpan(period);

            AudioClientInitializeHook_Original =
                ComHookManager.HookFunction<IAudioClient3, CriWarePluginNative.IAudioClientInitializeHook>(
                    audioClient, 0,
                    AudioClientInitializeHook);
        }
        catch (COMException e)
        {
            // 0x88890001
            Logger.Error("Failed to initialize exclusive audio client for testing:");
            Logger.Error(e);
            Logger.Error(
                "The wave format of the exclusive audio is invalid and can't be used to initialize exclusive audio, exclusive audio feature is disabled!");
            Logger.Error("\tConfigured wave format:");
            PrintWaveFormatError(ref format);

            return false;
        }
        finally
        {
            if (audioClient != null)
            {
                Marshal.ReleaseComObject(audioClient);
                Thread.Sleep(200); // 等待音频客户端完全释放，否则有概率在后面的初始化中报设备被占用的错误
            }
        }

        Logger.Info($"Exclusive mode buffer duration: {bufferDuration.TotalMilliseconds}ms");

        return true;
    }

    private static void PrintWaveFormatInfo(ref WaveFormat format)
    {
        Logger.Info("\t\t- Wave Format:       " + format.waveFormatTag);
        Logger.Info("\t\t- Channels:          " + format.channels);
        Logger.Info("\t\t- Sample Rate:       " + format.sampleRate);
        Logger.Info("\t\t- Avg Bytes Per Sec: " + format.averageBytesPerSecond);
        Logger.Info("\t\t- Block Align:       " + format.blockAlign);
        Logger.Info("\t\t- Bits Per Sample:   " + format.bitsPerSample);
        Logger.Info("\t\t- CbSize:            " + format.extraSize);
        var formatEx = format.TryGetWaveFormatEx();
        if (formatEx == null) return;
        Logger.Info("\t\t- Samples:           " + formatEx.samples);
        Logger.Info("\t\t- Channel Mask:      " + "0b" + Convert.ToString(formatEx.dwChannelMask, 2));
        Logger.Info("\t\t- SubFormat:         " + formatEx.subFormat);
    }

    private static void PrintWaveFormatError(ref WaveFormat format)
    {
        Logger.Error("\t\t- Wave Format:       " + format.waveFormatTag);
        Logger.Error("\t\t- Channels:          " + format.channels);
        Logger.Error("\t\t- Sample Rate:       " + format.sampleRate);
        Logger.Error("\t\t- Avg Bytes Per Sec: " + format.averageBytesPerSecond);
        Logger.Error("\t\t- Block Align:       " + format.blockAlign);
        Logger.Error("\t\t- Bits Per Sample:   " + format.bitsPerSample);
        Logger.Error("\t\t- CbSize:            " + format.extraSize);
        var formatEx = format.TryGetWaveFormatEx();
        if (formatEx == null) return;
        Logger.Error("\t\t- Samples:           " + formatEx.samples);
        Logger.Error("\t\t- Channel Mask:      " + "0b" + Convert.ToString(formatEx.dwChannelMask, 2));
        Logger.Error("\t\t- SubFormat:         " + formatEx.subFormat);
    }

    private static void SetupIAudioRenderClientReleaseBufferHook(IntPtr pAudioClient)
    {
        if (Marshal.GetObjectForIUnknown(pAudioClient) is not IAudioClient3 audioClient)
        {
            Logger.Error("Failed to get IAudioClient3 from pAudioClient!");
            return;
        }

        audioClient.GetService(typeof(IAudioRenderClient).GUID, out var renderClient);

        AudioRenderClientGetBufferHook_Original =
            ComHookManager.HookFunction<IAudioRenderClient, CriWarePluginNative.IAudioRenderClientGetBufferHook>(
                renderClient, 0,
                IAudioRenderClientGetBufferHook);
        AudioRenderClientReleaseBufferHook_Original =
            ComHookManager
                .HookFunction<IAudioRenderClient, CriWarePluginNative.IAudioRenderClientReleaseBufferHook>(
                    renderClient, 1,
                    IAudioRenderClientReleaseBufferHook);

        Marshal.ReleaseComObject(audioClient);

        Logger.Info("Setup up IAudioRenderClientReleaseBufferHook");
    }

    private static int IAudioRenderClientGetBufferHook(IAudioRenderClient audioRenderClient, uint NumFramesRequested,
        out IntPtr ppData)
    {
        var result = AudioRenderClientGetBufferHook_Original!(audioRenderClient, NumFramesRequested, out ppData);

        if (result == 0 && ppData != IntPtr.Zero) lastAudioRenderClientBuffer = ppData;
        // Logger.Info($"Got audio render client buffer {ppData}, frame len: {NumFramesRequested}");
        return result;
    }

    private static int IAudioRenderClientReleaseBufferHook(IAudioRenderClient audioRenderClient, uint NumFramesWritten,
        int dwFlags)
    {
        if (configuredFormat != null && lastAudioRenderClientBuffer != IntPtr.Zero && OnAudioData != null)
            try
            {
                var waveBuffer =
                    new byte[NumFramesWritten * configuredFormat.channels * configuredFormat.bitsPerSample / 8];
                Marshal.Copy(lastAudioRenderClientBuffer, waveBuffer, 0, waveBuffer.Length);
                // Logger.Info($"Got audio buffer {waveBuffer.Length}");
                OnAudioData.Invoke(new OnAudioDataArgs
                {
                    Data = waveBuffer,
                    Timestamp = TimeSpan.FromTicks(DateTime.Now.Ticks),
                    Format = configuredFormat
                });
            }
            catch (Exception)
            {
                // Logger.Error($"Failed to get audio buffer in IAudioRenderClientReleaseBufferHook: {e}");
            }

        var result = AudioRenderClientReleaseBufferHook_Original!(audioRenderClient, NumFramesWritten, dwFlags);
        lastAudioRenderClientBuffer = IntPtr.Zero;

        return result;
    }

    private static uint AudioClientInitializeHook(IntPtr pAudioClient, AudioClientShareMode shareMode,
        AudioClientStreamFlags streamFlags, TimeSpan hnsBufferDuration, TimeSpan hnsPeriodicity, IntPtr pFormat,
        IntPtr audioSessionGuid)
    {
        if (showedUnsupportedError)
            return AudioClientInitializeHook_Original!(pAudioClient, AudioClientShareMode.Shared,
                streamFlags, TimeSpan.Zero, TimeSpan.Zero, pFormat,
                audioSessionGuid);

        var format = WaveFormat.MarshalFromPtr(pFormat);
        var duration = calibratedBufferDuration ?? bufferDuration;
        var formatEx = format.TryGetWaveFormatEx();
        if (formatEx != null)
        {
            formatEx.samples = Math.Min(formatEx.bitsPerSample, (short)24); // 强制将采样数设置为位深度
            formatEx.dwChannelMask = 0b11; // 强制将音频格式设置为立体声
            formatEx.subFormat = PCM_SUBFORMAT; // 强制将音频格式设置为 PCM
        }

        var finalFormat = formatEx ?? format;
        var pFinalFormatPtr = finalFormat.MarshalToPtr();

        Logger.Message("Initializing exclusive audio client...");
        var result = AudioClientInitializeHook_Original!(pAudioClient, shareMode,
            streamFlags, duration, duration, pFinalFormatPtr,
            audioSessionGuid);
        Logger.Message($"Initilize result: 0x{result:X}...");

        switch (result)
        {
            case 0:
            {
                configuredFormat = finalFormat;
                SetupIAudioRenderClientReleaseBufferHook(pAudioClient);
                Logger.Message("Exclusive audio client initialized successfully!");
                Logger.Info("    If the game crashed immediately after this, please restart the game and try again.");
                Logger.Info(
                    "    If the error still persists, please report the log file to developer or using a debugger to debug.");
                return result;
            }
            case 0x8889000a:
                Logger.Warn("The audio device is already in use for other software, waiting for it to be released...");
                return result;
            case 0x88890019 when !calibratedBufferDuration.HasValue:
            {
                Logger.Warn("Inappropriate buffer size, recalculating buffer size...");

                if (Marshal.GetObjectForIUnknown(pAudioClient) is not IAudioClient3 audioClient)
                {
                    Logger.Error("Failed to get IAudioClient3 from pAudioClient!");
                    return result;
                }

                audioClient.GetBufferSize(out var frameSize);
                var newBufferSize = 10000.0 * 1000 / finalFormat.sampleRate * frameSize + 0.5;
                calibratedBufferDuration = TimeSpan.FromTicks((long)newBufferSize);

                Logger.Warn($"New buffer duration: {calibratedBufferDuration}");

                Marshal.ReleaseComObject(audioClient);

                return result;
            }
        }

        showedUnsupportedError = true;
        Logger.Error(
            $"The wave format of the exclusive audio is invalid and can't be used to initialize exclusive audio (HRESULT: {result:x8}), audio will be disabled!");
        Logger.Error("\tConfigured wave format:");
        PrintWaveFormatError(ref finalFormat);
        switch (result)
        {
            case 0x88890019:
                Logger.Warn("Error meaning: AUDCLNT_E_BUFFER_SIZE_NOT_ALIGNED (The audio buffer is not aligned)");
                break;
            default:
                var hrEx = new Win32Exception((int)result);
                Logger.Warn($"Error meaning: {hrEx.Message}");
                break;
        }

        CriWarePluginNative.CriAtomWASAPI.SetAudioClientShareMode(AudioClientShareMode.Shared);
        CriWarePluginNative.CriAtomWASAPI.SetAudioClientBufferDuration(TimeSpan.Zero);
        CriWarePluginNative.CriAtomWASAPI.SetAudioClientFormat(IntPtr.Zero);

        return AudioClientInitializeHook_Original(pAudioClient, AudioClientShareMode.Shared,
            streamFlags, TimeSpan.Zero, TimeSpan.Zero, pFinalFormatPtr,
            audioSessionGuid);
    }

    private static void WriteMemory<T>(IntPtr location, [DisallowNull] T value)
    {
        var size = Marshal.SizeOf<T>();
        var buffer = Marshal.AllocHGlobal(size);
        VirtualProtect(location, size, 0x40, out var oldProect);
        Marshal.StructureToPtr(value, buffer, false);
        var bytes = new byte[size];
        Marshal.Copy(buffer, bytes, 0, size);
        Marshal.FreeHGlobal(buffer);
        Marshal.Copy(bytes, 0, location, size);
        VirtualProtect(location, size, oldProect, out _);
    }

    private static WaveFormat GetWaveFormat()
    {
        var configSampleRate = ExclusiveAudioPlugin.Instance.ConfigSampleRate.Value;
        var configBitsPerSample = (short)ExclusiveAudioPlugin.Instance.ConfigBitsPerSample.Value;
        var format = new WaveFormatEx
        {
            waveFormatTag = WaveFormatEncoding.Extensible,
            channels = 2,
            sampleRate = configSampleRate,
            bitsPerSample = configBitsPerSample,
            extraSize = 22,
            samples = Math.Min(configBitsPerSample, (short)24), // Not used in this case
            dwChannelMask = 0b11, // Front left and right
            subFormat = PCM_SUBFORMAT // PCM
        };
        if (mixFormat != null)
        {
            format.sampleRate = format.sampleRate == 0 ? mixFormat.sampleRate : format.sampleRate;
            format.bitsPerSample = format.bitsPerSample == 0 ? mixFormat.bitsPerSample : format.bitsPerSample;
            format.samples = Math.Min(format.bitsPerSample, (short)24);
        }

        format.blockAlign = (short)(format.bitsPerSample * format.channels / 8);
        format.averageBytesPerSecond = format.sampleRate * format.blockAlign;
        return format;
    }


    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32", PreserveSig = true)]
    private static extern bool VirtualProtect(IntPtr lpAddress, int dwSize, uint flNewProtect, out uint lpflOldProtect);

    public struct OnAudioDataArgs
    {
        public byte[] Data;
        public TimeSpan Timestamp;
        public WaveFormat Format;
    }

    internal static class CriWarePluginNative
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void OnCriAtomUnityLog(string msgBuffer, int level, LoggingData info, IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate uint IAudioClientInitializeHook(
            IntPtr audioClient,
            [In] AudioClientShareMode shareMode,
            [In] AudioClientStreamFlags streamFlags,
            [In] TimeSpan hnsBufferDuration, // REFERENCE_TIME
            [In] TimeSpan hnsPeriodicity, // REFERENCE_TIME
            IntPtr pFormat,
            IntPtr audioSessionGuid
        );

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate int IAudioRenderClientGetBufferHook(
            [MarshalAs(UnmanagedType.Interface)] IAudioRenderClient audioRenderClient, uint NumFramesRequested,
            out IntPtr ppData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate int IAudioRenderClientReleaseBufferHook(
            [MarshalAs(UnmanagedType.Interface)] IAudioRenderClient audioRenderClient, uint NumFramesWritten,
            int dwFlags);

        public static class CriAtomWASAPI
        {
            // criAtom_SetAudioClientShareMode_WASAPI
            [DllImport(CriWarePluginName, EntryPoint = "criAtom_SetAudioClientShareMode_WASAPI",
                CallingConvention = CallingConvention.StdCall)]
            public static extern void SetAudioClientShareMode(AudioClientShareMode mode);

            [DllImport(CriWarePluginName, EntryPoint = "criAtom_SetAudioClientFormat_WASAPI",
                CallingConvention = CallingConvention.StdCall)]
            public static extern void SetAudioClientFormat(IntPtr mode);

            [DllImport(CriWarePluginName, EntryPoint = "criAtom_SetAudioClientBufferDuration_WASAPI",
                CallingConvention = CallingConvention.StdCall)]
            public static extern void SetAudioClientBufferDuration(TimeSpan duration);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 2)]
        public struct LoggingData
        {
            public long data1;
            public long data2;
            public long data3;
        }
    }
}