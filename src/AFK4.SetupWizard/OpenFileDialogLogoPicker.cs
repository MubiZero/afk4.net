using AFK4.SetupWizard.Core;
using Microsoft.Win32;

namespace AFK4.SetupWizard;

/// <summary>
/// Окно выбора файла средствами системы. Фильтр — только растровые картинки: логотип поедет на
/// экран игрока и в приложение, а сервер всё равно примет только настоящее изображение.
/// </summary>
public sealed class OpenFileDialogLogoPicker : ILogoFilePicker
{
    public string? PickImage()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Изображения (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp",
            CheckFileExists = true,
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
