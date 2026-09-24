using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Workstation;

/// <summary>
/// Громкость, микрофон и раскладка. Без проводника у игрока нет ни значка громкости, ни
/// переключателя языка в углу — их даёт системная строка оболочки.
/// </summary>
public interface ISystemControls
{
    /// <summary>Что сейчас на ПК; поле пусто, если Windows не ответила.</summary>
    ShellSystemStateDto Read();

    void SetVolume(int percent);

    void SetMicMuted(bool muted);

    void SetLayout(string label);
}
