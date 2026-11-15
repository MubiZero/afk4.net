using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace AFK4.WebHost;

/// <summary>
/// ВРЕМЕННО: диагностика падения Miniblink на Windows 7 x86 (c0000005 в mb132_x32.dll, 15.11.2026). Векторный обработчик
/// пишет в файл код и адрес первого нарушения доступа и модули со смещениями, найденные в слове стека над указателем
/// стека: так видно, какой вызов привёл в движок. Включается AFK4_WEBHOST_DEBUG=trace. Убрать вместе с причиной.
/// </summary>
internal static unsafe partial class CrashTrace
{
    private static string logPath = string.Empty;
    private static int written;

    public static void Install(string path)
    {
        logPath = path;
        AddVectoredExceptionHandler(1, &Handler);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Handler(void** pointers)
    {
        try
        {
            var record = (uint*)pointers[0];
            var context = (byte*)pointers[1];
            // Только нарушение доступа и только первое: остальные исключения — штатная работа .NET.
            if (record[0] != 0xC0000005 || Interlocked.Exchange(ref written, 1) != 0)
            {
                return 0;
            }

            var text = new StringBuilder();
            var wide = IntPtr.Size == 8;
            var ip = wide ? *(nuint*)(context + 0xF8) : *(uint*)(context + 0xB8);
            var sp = wide ? *(nuint*)(context + 0x98) : *(uint*)(context + 0xC4);
            text.AppendLine($"{DateTimeOffset.Now:O} access violation, thread {Environment.CurrentManagedThreadId}");
            text.AppendLine($"ip = {Describe((nuint)ip)}");
            // Кадры: слова стека, похожие на адреса возврата (внутри модуля). Первые 400 слов хватает на цепочку вызовов.
            var found = 0;
            for (var i = 0; i < 400 && found < 40; i++)
            {
                var word = ((nuint*)sp)[i];
                if (Describe(word) is { } module && module.Contains('+', StringComparison.Ordinal))
                {
                    text.AppendLine($"  sp[{i}] {module}");
                    found++;
                }
            }

            File.AppendAllText(logPath, text.ToString());
        }
        catch
        {
            // Диагностика не должна ронять процесс сама.
        }

        return 0;
    }

    private static string? Describe(nuint address)
    {
        const uint FromAddress = 0x4, UnchangedRefCount = 0x2;
        IntPtr module;
        if (address < 0x10000 || GetModuleHandleEx(FromAddress | UnchangedRefCount, address, &module) == 0)
        {
            return address == 0 ? "null" : null;
        }

        var name = new char[260];
        fixed (char* buffer = name)
        {
            var length = GetModuleFileName(module, buffer, 260);
            var file = new string(buffer, 0, (int)length);
            return $"{Path.GetFileName(file)}+0x{(nuint)address - (nuint)module:X}";
        }
    }

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr AddVectoredExceptionHandler(uint first, delegate* unmanaged[Stdcall]<void**, int> handler);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleExW")]
    private static partial int GetModuleHandleEx(uint flags, nuint address, IntPtr* module);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleFileNameW")]
    private static partial uint GetModuleFileName(IntPtr module, char* name, uint size);
}
