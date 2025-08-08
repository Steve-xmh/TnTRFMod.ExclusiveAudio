using System.Runtime.InteropServices;

namespace TnTRFMod.ExclusiveAudio.Wasapi;

/// <summary>
///     Represents a Wave file format
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 2)]
public class WaveFormat
{
    /// <summary>format type</summary>
    public WaveFormatEncoding waveFormatTag;

    /// <summary>number of channels</summary>
    public short channels;

    /// <summary>sample rate</summary>
    public int sampleRate;

    /// <summary>for buffer estimation</summary>
    public int averageBytesPerSecond;

    /// <summary>block size of data</summary>
    public short blockAlign;

    /// <summary>number of bits per sample of mono data</summary>
    public short bitsPerSample;

    /// <summary>number of following bytes</summary>
    public short extraSize;

    /// <summary>
    ///     Helper function to retrieve a WaveFormat structure from a pointer
    /// </summary>
    /// <param name="pointer">WaveFormat structure</param>
    /// <returns></returns>
    public static WaveFormat MarshalFromPtr(IntPtr pointer)
    {
        return Marshal.PtrToStructure<WaveFormat>(pointer)!;
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
               $"AverageBytesPerSecond: {averageBytesPerSecond}, BlockAlign: {blockAlign}, BitsPerSample: {bitsPerSample}, " +
               $"ExtraSize: {extraSize}";
    }

    public WaveFormatEx? TryGetWaveFormatEx()
    {
        if (this is WaveFormatEx waveFormatEx) return waveFormatEx;

        if (extraSize < 22) return null;

        var waveFormatExtensible = Marshal.PtrToStructure<WaveFormatEx>(MarshalToPtr());
        return waveFormatExtensible ?? null;
    }
}