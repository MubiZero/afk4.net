namespace AFK4.SetupWizard.Core;

/// <summary>
/// Диалог выбора файла. Живёт интерфейсом, потому что сам диалог — часть нативного окна: ядро
/// собирается и тестируется без WPF, а WebView2 своего окна выбора файла не откроет.
/// </summary>
public interface ILogoFilePicker
{
    /// <returns>Путь к выбранному файлу или <c>null</c>, если человек закрыл диалог.</returns>
    string? PickImage();
}
