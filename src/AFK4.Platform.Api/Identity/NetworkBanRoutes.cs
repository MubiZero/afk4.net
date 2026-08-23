namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Пометка на маршруте: этим человек не начинает новое, а возвращает начатое — отменяет свою
/// бронь, снимает свой заказ, отключает уведомления.
///
/// Сетевой запрет останавливает действия, а не зрение и не отмену: отменённая бронь освобождает
/// место клубу и размораживает деньги человеку, и запирать её значит наказывать обоих.
/// </summary>
public sealed class PlayerReleaseActionAttribute : Attribute;

public static class NetworkBanRoutes
{
    public static TBuilder WorksWhenNetworkBanned<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new PlayerReleaseActionAttribute());
        return builder;
    }

    public static bool WorksWhenNetworkBanned(this HttpContext httpContext) =>
        httpContext.GetEndpoint()?.Metadata.GetMetadata<PlayerReleaseActionAttribute>() is not null;

    /// <summary>
    /// Запрещённому оставлено чтение и отмена. Правило про метод, а не список маршрутов: маршрут,
    /// добавленный завтра, обязан оказаться закрытым сам, без того чтобы кто-то вспомнил про него.
    /// </summary>
    public static bool IsBlockedForBannedPerson(this HttpContext httpContext) =>
        !HttpMethods.IsGet(httpContext.Request.Method)
        && !HttpMethods.IsHead(httpContext.Request.Method)
        && !httpContext.WorksWhenNetworkBanned();
}
