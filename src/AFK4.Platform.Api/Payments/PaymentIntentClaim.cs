using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Payments;

/// <summary>
/// Перевод заявки на пополнение из «ожидает» в конечное состояние.
///
/// Раньше обе стороны — отмена и завершение на стойке — читали состояние, решали и писали своё.
/// Между чтением и записью помещалась чужая запись, и порядок «стойка зачислила → отмена
/// записала cancelled» оставлял кошелёк пополненным при заявке, помеченной отменённой. Денег это
/// не двигало (зачисление идемпотентно по идентификатору заявки), но состояние врало: в списке
/// игрока отмена, на кошельке деньги.
///
/// Здесь условие «состояние всё ещё pending» — часть самой записи, а не отдельный шаг перед ней.
/// Проигравшая сторона видит, что не изменила ни строки, и отвечает конфликтом.
///
/// Порядок в завершении поэтому обратный прежнему: сначала заявка занимается, и только потом
/// двигаются деньги. Занять и не зачислить — заявка останется в «завершено» без денег, и это
/// видно человеку за стойкой; зачислить и не занять — деньги ушли под заявку, которую в тот же
/// момент отменили, и узнать об этом неоткуда.
/// </summary>
internal static class PaymentIntentClaim
{
    public const string Pending = "pending";
    public const string Fulfilled = "fulfilled";
    public const string Cancelled = "cancelled";

    /// <summary>
    /// Переводит заявку из <see cref="Pending"/> в <paramref name="nextState"/>. Возвращает
    /// <c>false</c>, если строка уже не в «ожидает»: кто-то успел раньше.
    ///
    /// <paramref name="intent"/> — отслеживаемая сущность, из которой строится ответ: при успехе
    /// её поля обновляются здесь же, чтобы экран не показал устаревшее состояние.
    /// </summary>
    public static async Task<bool> TryMoveFromPendingAsync(
        PlatformDbContext dbContext,
        PaymentIntentEntity intent,
        string nextState,
        DateTimeOffset? fulfilledAtUtc,
        CancellationToken cancellationToken)
    {
        var moved = await TryMoveAsync(dbContext, intent, Pending, nextState, fulfilledAtUtc, cancellationToken);
        return moved;
    }

    /// <summary>
    /// Возвращает занятую заявку обратно в «ожидает», когда зачисление не состоялось. Иначе она
    /// осталась бы в «завершено» без денег, и ни повторить, ни отменить её было бы нельзя.
    /// </summary>
    public static Task<bool> TryReleaseAsync(
        PlatformDbContext dbContext,
        PaymentIntentEntity intent,
        CancellationToken cancellationToken) =>
        TryMoveAsync(dbContext, intent, Fulfilled, Pending, null, cancellationToken);

    private static async Task<bool> TryMoveAsync(
        PlatformDbContext dbContext,
        PaymentIntentEntity intent,
        string expectedState,
        string nextState,
        DateTimeOffset? fulfilledAtUtc,
        CancellationToken cancellationToken)
    {
        var intentId = intent.PaymentIntentId;

        if (!dbContext.Database.IsRelational())
        {
            // In-memory провайдер условного обновления не умеет. Гонки в нём тоже нет: тесты на
            // нём однопоточные, а настоящую одновременность проверяют Postgres-тесты.
            var tracked = await dbContext.PaymentIntents.SingleOrDefaultAsync(
                candidate => candidate.PaymentIntentId == intentId, cancellationToken);
            if (tracked is null || tracked.State != expectedState)
            {
                return false;
            }

            tracked.State = nextState;
            tracked.FulfilledAtUtc = fulfilledAtUtc;
            await dbContext.SaveChangesAsync(cancellationToken);
            Mirror(dbContext, intent, nextState, fulfilledAtUtc);
            return true;
        }

        var affected = await dbContext.PaymentIntents
            .Where(candidate => candidate.PaymentIntentId == intentId && candidate.State == expectedState)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.State, nextState)
                    .SetProperty(candidate => candidate.FulfilledAtUtc, fulfilledAtUtc),
                cancellationToken);

        if (affected == 0)
        {
            return false;
        }

        Mirror(dbContext, intent, nextState, fulfilledAtUtc);
        return true;
    }

    /// <summary>
    /// Приводит отслеживаемую копию к записанному состоянию и снимает её с отслеживания.
    ///
    /// Без этого любой следующий SaveChanges в том же запросе — хотя бы запись в журнал — снова
    /// выложил бы состояние из памяти, уже безусловно, и затёр бы чужую отмену, успевшую в этот зазор.
    /// То есть вернул бы ровно ту гонку, которую здесь закрывают.
    /// </summary>
    private static void Mirror(
        PlatformDbContext dbContext,
        PaymentIntentEntity intent,
        string nextState,
        DateTimeOffset? fulfilledAtUtc)
    {
        intent.State = nextState;
        intent.FulfilledAtUtc = fulfilledAtUtc;
        dbContext.Entry(intent).State = EntityState.Detached;
    }
}
