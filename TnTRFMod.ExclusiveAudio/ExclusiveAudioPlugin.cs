using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace TnTRFMod.ExclusiveAudio;

[BepInPlugin("net.stevexmh.TnTRFMod.ExclusiveAudio", ModName, "1.1.0")]
public class ExclusiveAudioPlugin : BasePlugin
{
    public const string ModName = "TnTRFMod.ExclusiveAudio";

    public static ExclusiveAudioPlugin Instance;
    public new static ManualLogSource Log;
    public ConfigEntry<short> ConfigAudioStreamPort;
    public ConfigEntry<int> ConfigAudioStreamTransferLatency;
    public ConfigEntry<int> ConfigBitsPerSample;
    public ConfigEntry<bool> ConfigEnableCriWarePluginLogging;

    private ConfigEntry<bool> ConfigEnabled;
    public ConfigEntry<int> ConfigSampleRate;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleOutputCP(uint wCodePageID);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCP(uint wCodePageID);

    public override void Load()
    {
        SetConsoleOutputCP(65001);
        SetConsoleCP(65001);

        Instance = this;

        ConfigEnabled = Config.Bind("General",
            "Enabled",
            true,
            "Enables the mod.\n" +
            "For who is first time to use this mod, please low down your audio volume to make sure the audio won't have loud noise or any other issues!");

        ConfigSampleRate = Config.Bind("General",
            "SampleRate",
            0,
            "Sample Rate of the exclusive mode wave format.\n" +
            "This should match the format of your audio output device.\n" +
            "If set to 0, it will use the sample rate of the mix format of your audio output device." +
            "If you are unsure, try use 48000 and adjust your audio output format on the system settings.");

        ConfigBitsPerSample = Config.Bind("General",
            "BitsPerSample",
            0,
            "Bit size of the sample of exclusive mode wave format.\n" +
            "This should match the format of your audio output device and the compatibility of the CriWare Unity Plugin.\n" +
            "If set to 0, it will use the bit size of the mix format of your audio output device.\n" +
            "If you are unsure, try use 16 and adjust your audio output format on the system settings.");

        ConfigEnableCriWarePluginLogging = Config.Bind("General",
            "EnableCriWarePluginLogging",
            false,
            "Enable logging of CriWare Unity Plugin.\n" +
            "if you meet some audio issues, you can turn this on to check problems.");

        ConfigAudioStreamPort = Config.Bind("General",
            "AudioStreamPort",
            (short)0,
            "Port for the HTTP audio stream server.\n" +
            "This is used to stream audio data over HTTP for capture/record/live streaming via OBS.\n" +
            "If you have poor computer performance then this may have audio issue while playing.\n" +
            "Set it to 0 to disable.");
        ConfigAudioStreamTransferLatency = Config.Bind("General",
            "AudioStreamTransferLatency",
            10,
            "Transfer latency for the HTTP audio stream server. (In milliseconds)\n" +
            "This is used to control the transfer latency of the audio stream.\n" +
            "If the audio data that is being transferred is later than now by this value, then this data will be dropped to follow up the latency.");

        Log = base.Log;

        if (!ConfigEnabled.Value) return;

        CriWareEnableExclusiveModePatch.Apply();
        HTTPAudioServer.Start();
    }
}