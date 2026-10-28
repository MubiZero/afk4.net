#if NETFRAMEWORK
using System.ComponentModel;
using System.Reflection;

namespace System.Security.Cryptography
{
    /// <summary>Формат подписи ECDSA; в .NET Framework 4.8 есть только P1363 (NetFrameworkCompat).</summary>
    internal enum DSASignatureFormat
    {
        IeeeP1363FixedFieldConcatenation,
        Rfc3279DerSequence
    }
}

namespace System.IO.Pipes
{
    /// <summary>Канал со списком доступа: в 4.8 это обычный конструктор, в .NET Core — отдельный класс.</summary>
    internal static class NamedPipeServerStreamAcl
    {
        public static NamedPipeServerStream Create(
            string pipeName, PipeDirection direction, int maxNumberOfServerInstances, PipeTransmissionMode transmissionMode,
            PipeOptions options, int inBufferSize, int outBufferSize, PipeSecurity? pipeSecurity) =>
            new(pipeName, direction, maxNumberOfServerInstances, transmissionMode, options, inBufferSize, outBufferSize, pipeSecurity);
    }
}

namespace System.Runtime.InteropServices
{
    /// <summary>Загрузка своих DLL (nvml.dll) — то, что в .NET Core делает NativeLibrary.</summary>
    internal static class NativeLibrary
    {
        public static bool TryLoad(string libraryName, Assembly assembly, DllImportSearchPath? searchPath, out IntPtr handle)
        {
            handle = LoadLibraryEx(libraryName, IntPtr.Zero, searchPath == DllImportSearchPath.System32 ? LoadLibrarySearchSystem32 : 0);
            return handle != IntPtr.Zero;
        }

        public static bool TryGetExport(IntPtr handle, string name, out IntPtr address)
        {
            address = GetProcAddress(handle, name);
            return address != IntPtr.Zero;
        }

        public static void Free(IntPtr handle)
        {
            if (handle != IntPtr.Zero && !FreeLibrary(handle))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        private const uint LoadLibrarySearchSystem32 = 0x00000800;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string fileName, IntPtr file, uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true, BestFitMapping = false)]
        private static extern IntPtr GetProcAddress(IntPtr module, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr module);
    }
}
#endif
