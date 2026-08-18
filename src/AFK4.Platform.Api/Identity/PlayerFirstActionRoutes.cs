namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Пометка на маршруте: здесь человек делает что-то, ради чего клубу стоит открыть ему счёт.
///
/// Помечать этим весь край игрока нельзя: тогда пролистывание чужой витрины заводило бы карточки
/// в клубах, куда человек так и не пришёл, и стойка получила бы список призраков вместо гостей.
/// Отметку носят только действия: бронь, пополнение, посадка.
/// </summary>
public sealed class PlayerFirstActionAttribute : Attribute;

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
}
