using System.Runtime.InteropServices;
using AFK4.SetupWizard.Core;

namespace AFK4.SetupWizard;

/// <summary>Системные окна Windows без WPF: выбор картинки и сообщение об ошибке.</summary>
internal static unsafe partial class NativeDialogs
{
    private const uint MbIconError = 0x10;
    private const uint OfnFileMustExist = 0x1000;
    private const uint OfnPathMustExist = 0x0800;
    private const uint OfnNoChangeDir = 0x0008;
    private const uint OfnExplorer = 0x80000;

    public static void ShowError(string caption, string text) => MessageBox(IntPtr.Zero, text, caption, MbIconError);

    /// <summary>
    /// Окно выбора файла средствами системы. Фильтр — только растровые картинки: логотип поедет на экран игрока и в
    /// приложение, а сервер всё равно примет только настоящее изображение.
    /// </summary>
    public static string? PickImage()
    {
        const int capacity = 1024;
        var file = stackalloc char[capacity];
        file[0] = '\0';
        // Пары «название\0маска\0», в конце ещё один \0.
        fixed (char* filter = "Изображения (*.png;*.jpg;*.jpeg;*.webp)\0*.png;*.jpg;*.jpeg;*.webp\0\0")
        {
            var dialog = new OpenFileName
            {
                StructSize = sizeof(OpenFileName),
                Filter = filter,
                File = file,
                MaxFile = capacity,
                Flags = OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir | OfnExplorer
            };
            return GetOpenFileName(&dialog) != 0 ? new string(file) : null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OpenFileName
    {
        public int StructSize;
        public IntPtr Owner, Instance;
        public char* Filter, CustomFilter;
        public int MaxCustomFilter, FilterIndex;
        public char* File;
        public int MaxFile;
        public char* FileTitle;
        public int MaxFileTitle;
        public char* InitialDir, Title;
        public uint Flags;
        public short FileOffset, FileExtension;
        public char* DefaultExtension;
        public IntPtr CustomData, Hook;
        public char* TemplateName;
        public IntPtr Reserved;
        public uint ReservedFlags, FlagsEx;
    }

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(IntPtr owner, string text, string caption, uint type);

    [LibraryImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW")]
    private static partial int GetOpenFileName(OpenFileName* dialog);
}

internal sealed class NativeLogoPicker : ILogoFilePicker
{
    public string? PickImage() => NativeDialogs.PickImage();
}
