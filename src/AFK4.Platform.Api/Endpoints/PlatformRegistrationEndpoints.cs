using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// Дверь, у которой нет клуба и нет администратора: человек заводит себя сам. Тот же маршрут
/// впускает и того, кто уже есть, — иначе сама пара «зарегистрироваться / войти» рассказывала бы
/// звонящему, знаком нам его номер или нет.
/// </summary>
internal static class PlatformRegistrationEndpoints
{
    public static void MapPlatformRegistrationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/public/register/start", async (
            RegistrationStartRequest request,
            IPlatformRegistrationService registrationService,
            CancellationToken cancellationToken) =>
        {
            var result = await registrationService.StartAsync(request.PhoneNumber, cancellationToken);

            return result.Status switch
            {
                PhoneVerificationStartStatus.Sent => Results.Ok(
                    new RegistrationStartedResponse(result.ExpiresInSeconds, result.ResendAfterSeconds)),
                PhoneVerificationStartStatus.InvalidPhone => Results.BadRequest(new { error = "invalid_phone" }),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        }).RequireRateLimiting("register-public");

        app.MapPost("/api/public/register/confirm", async (
            RegistrationConfirmRequest request,
            IPlatformRegistrationService registrationService,
            CancellationToken cancellationToken) =>
        {
            var result = await registrationService.ConfirmAsync(
                request.PhoneNumber, request.Code, cancellationToken);

            return result.Status switch
            {
                PlatformRegistrationConfirmStatus.SignedIn => Results.Ok(result.Session),
                PlatformRegistrationConfirmStatus.InvalidCode => Results.Json(
                    new { error = "invalid_code", remainingAttempts = result.RemainingAttempts },
                    statusCode: StatusCodes.Status400BadRequest),
                PlatformRegistrationConfirmStatus.Expired => Results.Json(
                    new { error = "code_expired" }, statusCode: StatusCodes.Status410Gone),
                PlatformRegistrationConfirmStatus.NoActiveCode => Results.Json(
                    new { error = "no_active_code" }, statusCode: StatusCodes.Status410Gone),
                PlatformRegistrationConfirmStatus.TooManyAttempts => Results.Json(
                    new { error = "too_many_attempts" }, statusCode: StatusCodes.Status429TooManyRequests),
                // Здесь ответ уже можно давать честный: код из SMS доказал, что номер этот.
                PlatformRegistrationConfirmStatus.PersonDeactivated => Results.Json(
                    new { error = "account_disabled" }, statusCode: StatusCodes.Status403Forbidden),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        }).RequireRateLimiting("register-public");
    }
}
