namespace AFK4.Shared.Contracts.Install;

/// <summary>
/// Отметка «агент прочитал настройку и взялся за работу». Агент кладёт файл в свой каталог
/// состояния, как только убедился, что настройка пригодна; мастер установки перед запуском службы
/// файл стирает и ждёт, пока он появится.
///
/// Запущенная служба — ещё не принятая настройка: агент с негодной настройкой (адрес без https,
/// нет ключа устройства) тихо простаивает и не выходит на связь, а <c>sc start</c> при этом
/// отвечает успехом. Без отметки мастер говорил «ПК подключён» про машину, которая молчит.
/// </summary>
public static class AgentReadyMarker
{
    public const string FileName = "agent-ready";

    public static string PathIn(string stateDirectory) => Path.Combine(stateDirectory, FileName);

    public static string DefaultPath { get; } = PathIn(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AFK4",
        "Agent"));

    public static void Write(string stateDirectory)
    {
        Directory.CreateDirectory(stateDirectory);
        File.WriteAllText(PathIn(stateDirectory), DateTimeOffset.UtcNow.ToString("O"));
    }
}
