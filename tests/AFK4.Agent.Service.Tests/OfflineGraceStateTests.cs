using AFK4.Agent.Service.Enforcement;

namespace AFK4.Agent.Service.Tests;

/// <summary>
/// Льгота переживает перезапуск службы.
///
/// Пока время последнего контакта жило только в памяти, перезапуск посреди льготного окна стирал
/// его — и агент терял способность отличить «гость оплатил, а сеть только что пропала» от «связи
/// нет давно». Обе ошибки дорогие: запереть оплаченный ПК или оставить неоплаченный открытым.
/// </summary>
public sealed class OfflineGraceStateTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-14T10:00:00Z");
    private readonly string stateDirectory = Path.Combine(Path.GetTempPath(), $"afk4-grace-{Guid.NewGuid():N}");

    [Fact]
    public void RecordSuccessfulContact_SurvivesRestart()
    {
        var first = new OfflineGraceState(stateDirectory);
        first.RecordSuccessfulContact(Now, effectiveGraceMinutes: 20);

        var afterRestart = new OfflineGraceState(stateDirectory);

        Assert.Equal(Now, afterRestart.LastSuccessfulContactUtc);
        Assert.Equal(20, afterRestart.EffectiveGraceMinutes);
    }

    [Fact]
    public void WithoutFile_HasNoContactAndDefaultWindow()
    {
        var state = new OfflineGraceState(stateDirectory);

        Assert.Null(state.LastSuccessfulContactUtc);
        Assert.Equal(15, state.EffectiveGraceMinutes);
    }

    // Повреждённый файл не должен ни ронять службу, ни притворяться свежим контактом: без
    // подтверждения льготы машина запирается, и это безопасная сторона ошибки.
    [Fact]
    public void WithCorruptFile_FallsBackToNoContact()
    {
        Directory.CreateDirectory(stateDirectory);
        File.WriteAllText(Path.Combine(stateDirectory, OfflineGraceState.StateFileName), "{ not json");

        var state = new OfflineGraceState(stateDirectory);

        Assert.Null(state.LastSuccessfulContactUtc);
    }

    public void Dispose()
    {
        if (Directory.Exists(stateDirectory))
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }
}
