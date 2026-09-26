namespace AFK4.Player.Shell.Input;

public enum InputSignal
{
    None,

    /// <summary>Мышь или клавиатура тронуты: витрина уступает место окну входа.</summary>
    Activity,

    /// <summary>Тишина дольше порога: окно входа закрывается, вошедший выходит.</summary>
    Idle
}

/// <summary>
/// Простой и активность по времени последнего ввода Windows (спека оболочки, §3). Смотрит на ввод
/// во всей сессии, а не только в странице: пока играют, страница ввода не видит, а тишину за ПК
/// считать всё равно надо.
///
/// Тики — миллисекунды GetTickCount: через 49 дней счётчик переходит через ноль, и разность
/// считается без знака.
/// </summary>
public sealed class InputActivityTracker(TimeSpan idleAfter, TimeSpan activityEvery)
{
    /// <summary>Тишина, после которой окно входа закрывается (спека, §3: «60 с тишины»).</summary>
    public static readonly TimeSpan DefaultIdleAfter = TimeSpan.FromSeconds(60);

    /// <summary>Чаще сообщать незачем: странице нужно «человек здесь», а не каждое движение мыши.</summary>
    public static readonly TimeSpan DefaultActivityEvery = TimeSpan.FromSeconds(1);

    private uint? lastSeenInput;
    private uint? lastActivitySentAt;
    private bool idleSent;

    public InputSignal Observe(uint lastInputTick, uint nowTick)
    {
        var sinceInput = Elapsed(lastInputTick, nowTick);
        if (lastSeenInput is null)
        {
            // Первый взгляд — точка отсчёта, а не событие: ни «подошли», ни «ушли» ещё не случилось.
            lastSeenInput = lastInputTick;
            idleSent = sinceInput >= idleAfter;
            return InputSignal.None;
        }

        if (lastInputTick != lastSeenInput)
        {
            lastSeenInput = lastInputTick;
            idleSent = false;
            if (lastActivitySentAt is { } sentAt && Elapsed(sentAt, nowTick) < activityEvery)
            {
                return InputSignal.None;
            }

            lastActivitySentAt = nowTick;
            return InputSignal.Activity;
        }

        if (!idleSent && sinceInput >= idleAfter)
        {
            idleSent = true;
            return InputSignal.Idle;
        }

        return InputSignal.None;
    }

    private static TimeSpan Elapsed(uint fromTick, uint toTick) => TimeSpan.FromMilliseconds(unchecked(toTick - fromTick));
}
