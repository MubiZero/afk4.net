namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Опознаёт игрока на его собственном крае API и выбирает клуб, в котором он сейчас находится.
///
/// Токен теперь выдаётся человеку, а не клубному счёту, поэтому «текущий клуб» приходится
/// определять на каждом запросе. Порядок ниже — это и вся совместимость: клиент, который про
/// выбор клуба ничего не знает, продолжает попадать в клуб, закреплённый за токеном при входе.
/// Токены, выданные до перехода, продолжают работать до своего срока — иначе выкат разлогинил бы
/// всех игроков разом.
/// </summary>
public sealed class PlayerAuthenticationMiddleware(RequestDelegate next)
{
    /// <summary>Клуб, выбранный клиентом на этот запрос. Знают о нём только новые клиенты.</summary>
    public const string OrganizationHeader = "X-AFK4-Organization";

    /// <summary>Край игрока целиком: всё, что начинается отсюда, требует опознать человека.</summary>
    private const string PersonScopePath = "/api/me";

    /// <summary>
    /// Маршруты, которым клуб не нужен вовсе, потому что они про самого человека: «кто я и где у
    /// меня счета» и его сетевой PIN. PIN принадлежит личности, и задают его обычно ещё до первого
    /// визита куда бы то ни было — требовать для этого выбранный клуб значило бы запереть дверь
    /// перед тем, кто зарегистрировался дома.
    /// </summary>
    private static readonly string[] PersonScopeRoutes = [PersonScopePath, PersonScopePath + "/pin"];

    public async Task InvokeAsync(
        HttpContext httpContext,
        IPlayerTokenService tokenService,
        IPlatformPersonTokenService personTokenService,
        IPlayerClubAccountResolver clubAccountResolver,
        IPlayerContextAccessor playerContextAccessor,
        IPlatformPersonContextAccessor personContextAccessor)
    {
        // Игрока опознаём только на его крае; на маршрутах сотрудников и платформы — никогда.
        if (!httpContext.Request.Path.StartsWithSegments(PersonScopePath, StringComparison.OrdinalIgnoreCase))
        {
            await next(httpContext);
            return;
        }

        var authorization = httpContext.Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";
        if (!authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await next(httpContext);
            return;
        }

        var token = authorization[bearerPrefix.Length..].Trim();
        var person = await personTokenService.ValidateAsync(token, httpContext.RequestAborted);
        if (person is null)
        {
            // Токен, выданный до перехода: он по-прежнему знает и счёт, и клуб.
            playerContextAccessor.Current = await tokenService.ValidateAsync(token, httpContext.RequestAborted);
            await next(httpContext);
            return;
        }

        personContextAccessor.Current = person;

        if (!TryReadRequestedOrganization(httpContext, out var requestedOrganizationId))
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(
                new { error = "invalid_organization" }, httpContext.RequestAborted);
            return;
        }

        var selection = await clubAccountResolver.ResolveAsync(
            person.PlatformPersonId,
            requestedOrganizationId,
            person.PinnedOrganizationId,
            httpContext.RequestAborted);

        personContextAccessor.Current = person with { SelectedOrganizationId = selection.OrganizationId };

        if (selection.Account is { } account)
        {
            playerContextAccessor.Current = new PlayerContext(
                account.PlayerAccountId,
                account.OrganizationId,
                person.PhoneVerified,
                person.PlatformPersonId);
        }
        else if (!IsPersonScopeRoute(httpContext.Request.Path)
            && !(selection.OrganizationId is not null
                && (httpContext.OpensClubAccount() || httpContext.AllowsGuestWithoutClubAccount())))
        {
            // Человек опознан, но клуба нет — это не «кто ты такой», а «в каком клубе».
            // Отвечать 401 здесь значит отправить приложение на повторный вход, из которого
            // оно вернётся ровно с тем же результатом.
            //
            // Исключений два, и оба требуют названного клуба: маршрут-действие, ради которого
            // счёт и открывается (счёт заводит сам эндпоинт), и маршрут-рассказ о клубе, который
            // человек читает до первого действия и который счёта не открывает вовсе.
            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            await httpContext.Response.WriteAsJsonAsync(
                new { error = "club_not_selected" }, httpContext.RequestAborted);
            return;
        }

        await next(httpContext);
    }

    private static bool IsPersonScopeRoute(PathString path) =>
        PersonScopeRoutes.Any(route =>
            path.Equals(route, StringComparison.OrdinalIgnoreCase)
            || path.Equals(route + "/", StringComparison.OrdinalIgnoreCase));

    private static bool TryReadRequestedOrganization(HttpContext httpContext, out Guid? organizationId)
    {
        organizationId = null;
        var raw = httpContext.Request.Headers[OrganizationHeader].ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        // Нечитаемый клуб — это отказ, а не повод молча подставить другой: подставленный клуб
        // показал бы человеку чужой кошелёк там, где он ждал свой.
        if (!Guid.TryParse(raw, out var parsed) || parsed == Guid.Empty)
        {
            return false;
        }

        organizationId = parsed;
        return true;
    }
}
