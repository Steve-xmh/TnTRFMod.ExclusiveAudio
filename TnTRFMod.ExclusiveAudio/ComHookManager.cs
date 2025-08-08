using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace TnTRFMod.ExclusiveAudio;

public static class ComHookManager
{
    private const uint PAGE_EXECUTE_READWRITE = 0x40;
    private static readonly int intPtrSize = Marshal.SizeOf<IntPtr>();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect,
        out uint lpflOldProtect);

    [SuppressMessage("Interoperability", "CA1416:验证平台兼容性")]
    public static F HookFunction<T, F>(object comObject, int methodIndex, F function)
        where T : class
        where F : Delegate
    {
        var comPtr = Marshal.GetComInterfaceForObject(comObject, typeof(T));
        var vtable = Marshal.ReadIntPtr(comPtr);
        var start = Marshal.GetStartComSlot(typeof(T));
        var offset = (start + methodIndex) * intPtrSize;

        var methodPtr = Marshal.ReadIntPtr(vtable, offset);
        if (methodPtr == IntPtr.Zero) throw new Exception("<UNK>");
        Logger.Info($"Hooking {typeof(T).FullName} (Pointer: 0x{methodPtr.ToInt64():X})");
        var originalFunction = Marshal.GetDelegateForFunctionPointer<F>(methodPtr);
        var newFunctionPtr = Marshal.GetFunctionPointerForDelegate(function);

        // 修改vtable前，设置内存保护为可写
        var protectChanged =
            VirtualProtect(vtable + offset, (UIntPtr)IntPtr.Size, PAGE_EXECUTE_READWRITE, out var oldProtect);
        if (!protectChanged)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "VirtualProtect 失败");

        Marshal.WriteIntPtr(vtable, offset, newFunctionPtr);

        // 恢复原保护
        VirtualProtect(vtable + offset, (UIntPtr)IntPtr.Size, oldProtect, out _);

        Logger.Info($"Hooked {typeof(T).FullName} to 0x{newFunctionPtr.ToInt64():X}");
        return originalFunction;
    }
}