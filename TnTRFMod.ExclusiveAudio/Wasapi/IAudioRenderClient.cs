using System.Runtime.InteropServices;

namespace TnTRFMod.ExclusiveAudio.Wasapi;

[ComImport]
[Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioRenderClient
{
    [PreserveSig]
    int GetBuffer(uint NumFramesRequested, out IntPtr ppData);

    [PreserveSig]
    int ReleaseBuffer(uint NumFramesWritten, int dwFlags);
}