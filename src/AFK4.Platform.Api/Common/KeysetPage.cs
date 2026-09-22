using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Common;

/// <summary>
/// Страница ленты «от новых к старым» по курсору (момент, идентификатор).
///
/// Ничья по моменту решается порядком самой базы, а не сравнением идентификаторов в памяти. У
/// Postgres и у .NET порядок <see cref="Guid"/> разный: сравни мы в памяти, строки одного момента
/// на границе страницы терялись бы или повторялись. В журнале денег это не редкость, а норма —
/// снятие удержания и удержание за неявку пишутся одним моментом. Выписка, листаемая по одной
/// строке, отдавала 4 строки из 13.
/// </summary>
internal static class KeysetPage
{
    /// <param name="query">Отобранные строки, ещё не упорядоченные.</param>
    /// <param name="take">Сколько строк нужно — обычно размер страницы плюс одна, чтобы узнать,
    /// есть ли продолжение.</param>
    public static async Task<List<T>> TakeAsync<T>(
        IQueryable<T> query,
        Expression<Func<T, DateTimeOffset?>> moment,
        Expression<Func<T, Guid>> id,
        string? cursor,
        int take,
        CancellationToken cancellationToken)
        where T : class
    {
        IQueryable<T> Ordered(IQueryable<T> source) => source.OrderByDescending(moment).ThenByDescending(id);

        if (!CursorToken.TryDecode(cursor, out var afterMoment, out var afterId))
        {
            return await Ordered(query).Take(take).ToListAsync(cancellationToken);
        }

        // Строки того же момента, что и курсор, — в порядке базы; продолжаем сразу за курсором.
        // Их почти всегда одна-две, поэтому читаются целиком.
        var sameMoment = await Ordered(query.Where(Compare(moment, ExpressionType.Equal, afterMoment)))
            .ToListAsync(cancellationToken);
        var readId = id.Compile();
        var position = sameMoment.FindIndex(row => readId(row) == afterId);
        var rest = position >= 0
            ? sameMoment.Skip(position + 1).ToList()
            // Строки курсора больше нет (лента, в которой строки меняют момент). Лучшее, что
            // можно, — прежнее сравнение: оно ошибается только на границе того же момента.
            : sameMoment.Where(row => readId(row).CompareTo(afterId) < 0).ToList();

        if (rest.Count >= take)
        {
            return rest.Take(take).ToList();
        }

        var older = await Ordered(query.Where(Compare(moment, ExpressionType.LessThan, afterMoment)))
            .Take(take - rest.Count)
            .ToListAsync(cancellationToken);
        rest.AddRange(older);
        return rest;
    }

    private static Expression<Func<T, bool>> Compare<T>(
        Expression<Func<T, DateTimeOffset?>> moment,
        ExpressionType comparison,
        DateTimeOffset value)
    {
        // Значение идёт через поле объекта, а не константой: так запрос параметризуется, и план
        // у базы один на все курсоры.
        var boundary = Expression.Convert(
            Expression.Property(Expression.Constant(new Boundary(value)), nameof(Boundary.Value)),
            typeof(DateTimeOffset?));
        return Expression.Lambda<Func<T, bool>>(
            Expression.MakeBinary(comparison, moment.Body, boundary),
            moment.Parameters);
    }

    private sealed record Boundary(DateTimeOffset Value);
}
