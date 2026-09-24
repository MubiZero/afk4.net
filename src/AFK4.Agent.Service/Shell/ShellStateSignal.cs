namespace AFK4.Agent.Service.Shell;

/// <summary>
/// «Состояние ПК изменилось — отдай его оболочке сейчас». Будит канал, чтобы разблокировка дошла
/// до экрана сразу после команды, а не на следующем круге проверки.
/// </summary>
public interface IShellStateSignal
{
    void Notify();

    /// <summary>Дождаться сигнала или истечения <paramref name="timeout"/> — что наступит раньше.</summary>
    Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class ShellStateSignal : IShellStateSignal
{
    // Один слот: сколько бы раз ни дёрнули, канал проснётся один раз и соберёт свежее состояние.
    private readonly SemaphoreSlim pending = new(0, 1);

    public void Notify()
    {
        try
        {
            pending.Release();
        }
        catch (SemaphoreFullException)
        {
            // Уже разбудили, и канал ещё не проснулся — второй раз будить незачем.
        }
    }

    public async Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        await pending.WaitAsync(timeout, cancellationToken);
    }
}
