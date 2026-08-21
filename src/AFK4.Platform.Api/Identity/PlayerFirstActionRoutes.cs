namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Пометка на маршруте: здесь человек делает что-то, ради чего клубу стоит открыть ему счёт.
///
/// Помечать этим весь край игрока нельзя: тогда пролистывание чужой витрины заводило бы карточки
/// в клубах, куда человек так и не пришёл, и стойка получила бы список призраков вместо гостей.
/// Отметку носят только действия: бронь, пополнение, посадка.
/// </summary>
public sealed class PlayerFirstActionAttribute : Attribute;

/// <summary>
/// Пометка на маршруте: клуб назван, счёта в нём ещё нет — и это нормальное состояние, а не отказ.
///
/// Такие маршруты только рассказывают о клубе тому, кто в него ещё не вошёл: правила приёма
/// гостей нужнее всего как раз до первой брони. Счёта они не открывают — иначе чтение витрины
/// заводило бы стойке карточки людей, которые в клуб так и не пришли, — и ничего чужого не
/// показывают: числа такому человеку считаются как новому гостю.
/// </summary>
public sealed class PlayerClubGuestAttribute : Attribute;

public static class PlayerFirstActionRoutes
{
    public static TBuilder OpensClubAccount<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new PlayerFirstActionAttribute());
        return builder;
    }

    public static bool OpensClubAccount(this HttpContext httpContext) =>
        httpContext.GetEndpoint()?.Metadata.GetMetadata<PlayerFirstActionAttribute>() is not null;

    public static TBuilder AllowsGuestWithoutClubAccount<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new PlayerClubGuestAttribute());
        return builder;
    }

    public static bool AllowsGuestWithoutClubAccount(this HttpContext httpContext) =>
        httpContext.GetEndpoint()?.Metadata.GetMetadata<PlayerClubGuestAttribute>() is not null;
}
