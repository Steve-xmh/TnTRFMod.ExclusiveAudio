using System.Runtime.InteropServices;

namespace TnTRFMod.ExclusiveAudio.Wasapi;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 2)]
public class WaveFormatEx : WaveFormat
{
    /// <summary>
    ///     The size of the extension in bytes
    /// </summary>
    public short samples;

    public int dwChannelMask;

    public Guid subFormat;

    /// <summary>
    ///     Helper function to retrieve a WaveFormatEx structure from a pointer
    /// </summary>
    /// <param name="pointer">WaveFormatEx structure</param>
    /// <returns></returns>
    public new static WaveFormatEx MarshalFromPtr(IntPtr pointer)
    {
        var waveFormatEx = Marshal.PtrToStructure<WaveFormatEx>(pointer);
        return waveFormatEx;
    }

    /// <summary>
    ///     Helper function to retrieve a WaveFormat structure from a pointer
    /// </summary>
    /// <param name="pointer">WaveFormat structure</param>
    /// <returns></returns>
    public IntPtr MarshalToPtr()
    {
        var size = Marshal.SizeOf(this);
        var ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(this, ptr, false);

        return ptr;
    }

    public override string ToString()
    {
        return $"WaveFormatTag: {waveFormatTag}, Channels: {channels}, SampleRate: {sampleRate}, " +
               $"AverageBytesPerSecond: {averageBytesPerSecond}, BlockAlign: {blockAlign}, " +
               $"BitsPerSample: {bitsPerSample}, ExtraSize: {extraSize}, Samples: {samples}, " +
               $"ChannelMask: {dwChannelMask}, SubFormat: {subFormat}";
    }
}