using AFK4.Agent.Service.Updates;

namespace AFK4.Agent.Service.Tests;

/// <summary>
/// Счёт попыток обновления переживает перезапуск службы.
///
/// Пока он жил в памяти процесса, сломанный пакет крутился по кругу: поставили — упало —
/// «откатились» — служба перезапустилась (штатное следствие обновления) — память обнулилась —
/// поставили снова. На всём парке сразу.
/// </summary>
public sealed class FileUpdateAttemptLedgerTests : IDisposable
{
    private readonly string stateDirectory = Path.Combine(Path.GetTempPath(), $"afk4-attempts-{Guid.NewGuid():N}");
    private readonly Guid rolloutId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    [Fact]
    public void SecondAttemptSurvivesRestart_ThirdIsRefused()
    {
        var first = new FileUpdateAttemptLedger(stateDirectory);
        Assert.True(first.ShouldAttempt(rolloutId));
        first.RecordAttempt(rolloutId);

        // Перезапуск службы: журнал читается с диска, а не начинается заново.
        var afterFirstRestart = new FileUpdateAttemptLedger(stateDirectory);
        Assert.True(afterFirstRestart.ShouldAttempt(rolloutId));
        afterFirstRestart.RecordAttempt(rolloutId);

        var afterSecondRestart = new FileUpdateAttemptLedger(stateDirectory);
        Assert.False(afterSecondRestart.ShouldAttempt(rolloutId));
    }

    // Сбой сети — не вина пакета: такая попытка не должна съедать лимит.
    [Fact]
    public void ForgottenAttemptDoesNotCountAgainstTheLimit()
    {
        var ledger = new FileUpdateAttemptLedger(stateDirectory);
        ledger.RecordAttempt(rolloutId);
        ledger.ForgetAttempt(rolloutId);
        ledger.RecordAttempt(rolloutId);

        Assert.True(new FileUpdateAttemptLedger(stateDirectory).ShouldAttempt(rolloutId));
    }

    [Fact]
    public void OtherRolloutsAreNotAffected()
    {
        var ledger = new FileUpdateAttemptLedger(stateDirectory);
        ledger.RecordAttempt(rolloutId);
        ledger.RecordAttempt(rolloutId);

        Assert.False(ledger.ShouldAttempt(rolloutId));
        Assert.True(ledger.ShouldAttempt(Guid.Parse("22222222-2222-4222-8222-222222222222")));
    }

    // Повреждённый журнал не должен запрещать обновления навсегда: считаем, что попыток не было.
    [Fact]
    public void CorruptLedgerStartsFromScratch()
    {
        Directory.CreateDirectory(stateDirectory);
        File.WriteAllText(Path.Combine(stateDirectory, FileUpdateAttemptLedger.LedgerFileName), "{ not json");

        Assert.True(new FileUpdateAttemptLedger(stateDirectory).ShouldAttempt(rolloutId));
    }

    public void Dispose()
    {
        if (Directory.Exists(stateDirectory))
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }
}
