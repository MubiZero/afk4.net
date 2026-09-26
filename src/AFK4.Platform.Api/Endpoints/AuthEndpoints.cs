using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Options;
using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Dashboard;
using AFK4.Platform.Api.Diagnostics;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Identity.PhoneOtp;
using AFK4.Platform.Api.Install;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Outbox;
using AFK4.Platform.Api.Payments;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Platform.Api.Platform.Idempotency;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Platform.Api.Platform.Tenancy;
using AFK4.Platform.Api.Pos;
using AFK4.Platform.Api.Receipts;
using AFK4.Platform.Api.Reports;
using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Players;
using AFK4.Platform.Api.Sessions;
using AFK4.Platform.Api.Shifts;
using AFK4.Platform.Api.Security;
using AFK4.Platform.Api.Tenancy;
using AFK4.Platform.Api.Updates;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Audit;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Diagnostics;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Layout;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Packages;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Branding;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Identity.AccountActivation;
using AFK4.Shared.Contracts.Platform.Operator;
using AFK4.Shared.Contracts.Platform.SupportNotes;
using AFK4.Shared.Contracts.Platform.Organizations;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Receipts;
using AFK4.Shared.Contracts.Reports;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using AFK4.Shared.Contracts.Tariffs;
using AFK4.Shared.Contracts.Updates;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Threading.RateLimiting;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(
        this WebApplication app,
        IEndpointRouteBuilder organizations)
    {
        // «Не подошло» и «заперто после пяти промахов» — разные новости: под первой человек
        // ищет опечатку и пробует снова, под второй ждёт четверть часа. Код уезжает клиенту,
        // который и решает, какими словами это сказать.
        static IResult SignInResult(StaffSignInOutcome outcome) => outcome switch
        {
            { LockedOut: true } => Results.Json(
                new { Error = "Too many failed password attempts.", Code = StaffAuthErrorCodeNames.TooManyPasswordAttempts },
                statusCode: StatusCodes.Status429TooManyRequests),
            { SignedIn: null } => Results.Unauthorized(),
            _ => Results.Ok(outcome.SignedIn)
        };

        organizations.MapPost("auth/staff/sign-in", async (
            Guid organizationId,
            StaffSignInRequest request,
            IStaffCredentialService credentialService,
            CancellationToken cancellationToken) =>
        {
            if (request.OrganizationId != organizationId)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            return SignInResult(await credentialService.SignInAsync(request, cancellationToken));
        });

        organizations.MapPost("auth/staff/sign-in-by-organization-key", async (
            Guid organizationId,
            StaffSignInByOrganizationKeyRequest request,
            IStaffCredentialService credentialService,
            CancellationToken cancellationToken) =>
        {
            var outcome = await credentialService.SignInByOrganizationKeyAsync(request, cancellationToken);

            return outcome.SignedIn is { OrganizationId: var responseOrganizationId }
                && responseOrganizationId != organizationId
                    ? Results.StatusCode(StatusCodes.Status403Forbidden)
                    : SignInResult(outcome);
        });

        organizations.MapPost("auth/staff/sign-in-by-login", async (
            Guid organizationId,
            StaffSignInByLoginRequest request,
            IStaffCredentialService credentialService,
            CancellationToken cancellationToken) =>
        {
            var resolution = await credentialService.SignInByLoginAsync(
                organizationId,
                request,
                cancellationToken);

            return SignInResult(new StaffSignInOutcome(resolution.SignedIn, resolution.LockedOut));
        });

        // ── Вход до того, как известна организация ───────────────────────────────────────
        // Мастер установки ставит первый ПК в клубе и организацию узнаёт ИЗ ОТВЕТА на вход;
        // требовать её в пути — требовать знать ответ до вопроса. Тот же довод давно принят для
        // сброса пароля, который живёт в корне с самого начала.
        //
        // Организационные копии выше остаются и не помечаются устаревшими: их знают уже
        // установленные в поле сборки Organization Admin, где организация записана на машине.
        //
        // Пути берутся из StaffAuthRoutes — тех же констант, что и у мастера; стык стережёт
        // WizardRouteContractTests. До 13.09.2026 путь был строкой в двух местах, и мастер
        // получал 404 на первом же экране при зелёных тестах с обеих сторон.
        app.MapPost(StaffAuthRoutes.SignIn, async (
            StaffSignInRequest request,
            IStaffCredentialService credentialService,
            CancellationToken cancellationToken) =>
        {
            return SignInResult(await credentialService.SignInAsync(request, cancellationToken));
        }).RequireRateLimiting("staff-sign-in");

        app.MapPost(StaffAuthRoutes.SignInByLogin, async (
            StaffSignInByLoginRequest request,
            IStaffCredentialService credentialService,
            CancellationToken cancellationToken) =>
        {
            var resolution = await credentialService.SignInByLoginAsync(
                organizationId: null,
                request,
                cancellationToken);

            if (resolution.SignedIn is not null || resolution.LockedOut)
            {
                return SignInResult(new StaffSignInOutcome(resolution.SignedIn, resolution.LockedOut));
            }

            // Один логин работает в нескольких клубах — человек выбирает, в какой войти, и
            // возвращается сюда через SignIn с названной организацией. Отказ и выбор нельзя
            // сливать в один ответ: «не подошло» и «подошло в двух местах» — разные исходы.
            return resolution.Clubs.Count > 0
                ? Results.Json(
                    new StaffSignInChooseClubResponse(resolution.Clubs),
                    statusCode: StatusCodes.Status409Conflict)
                : Results.Unauthorized();
        }).RequireRateLimiting("staff-sign-in");

        app.MapPost(StaffAuthRoutes.SignInByPhone, async (
            StaffSignInByPhoneRequest request,
            IStaffCredentialService credentialService,
            CancellationToken cancellationToken) =>
        {
            return SignInResult(await credentialService.SignInByPhoneAsync(request, cancellationToken));
        }).RequireRateLimiting("staff-sign-in");

        // Первый шаг входа: по номеру решается, спросить ПИН или код первого входа. Ответ говорит,
        // заведён ли номер как сотрудник где-то в сети, — но не где и не кем; по тому же лимиту,
        // что и сам вход.
        app.MapPost(StaffAuthRoutes.NextStep, async (
            StaffSignInNextStepRequest request,
            IStaffInviteService staffInviteService,
            CancellationToken cancellationToken) =>
            Results.Ok(new StaffSignInNextStepResponse(
                await staffInviteService.ResolveSignInStepAsync(request.PhoneNumber, cancellationToken))))
            .RequireRateLimiting("staff-sign-in");

        organizations.MapPost("auth/staff/refresh", async (
            Guid organizationId,
            StaffRefreshTokenRequest request,
            IStaffTokenService tokenService,
            CancellationToken cancellationToken) =>
        {
            if (request.OrganizationId != organizationId)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var response = await tokenService.RefreshAsync(request, cancellationToken);

            return response is null
                ? Results.Unauthorized()
                : Results.Ok(response);
        });

        // Выход отзывает предъявленную пару токенов. Без него «выйти» значило бы «стереть сессию
        // с этой машины»: refresh жил бы ещё месяц и пускал обратно любого, кто его унёс. Маршрут
        // намеренно не требует живого access — сотрудник, чей токен истёк за ночь, всё равно
        // должен иметь возможность закрыть свою сессию.
        organizations.MapPost("auth/staff/sign-out", async (
            Guid organizationId,
            StaffSignOutRequest request,
            HttpContext httpContext,
            IStaffTokenService tokenService,
            CancellationToken cancellationToken) =>
        {
            if (request.OrganizationId != organizationId)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var revoked = await tokenService.RevokeAsync(
                request,
                ReadBearerToken(httpContext),
                cancellationToken);

            return revoked ? Results.NoContent() : Results.Unauthorized();
        });

        // Самопосадка за игровой ПК. Маршрут остался прежним, чтобы ни одна установленная в поле
        // оболочка не заметила перемены, но проверяет он теперь сетевой PIN личности: PIN
        // принадлежит человеку и работает во всех клубах сети. Клубный хеш, который назначал
        // администратор, не читается больше никогда.
        //
        // Отказ один на все причины — «нет такого номера», «PIN не задан», «PIN неверен»,
        // «личность закрыта», «блокировка». Разные ответы превратили бы экран игрового ПК в
        // справочник «у кого в этой сети есть аккаунт».
        app.MapPost("/api/public/player/sign-in", async (
            PlayerSignInRequest request,
            IPlatformPinService pinService,
            CancellationToken cancellationToken) =>
        {
            var result = await pinService.SignInAsync(
                request.OrganizationId,
                request.PhoneNumber,
                request.Password,
                request.BranchId,
                cancellationToken);

            return result.Status == PinSignInStatus.SignedIn
                ? Results.Ok(result.Session)
                : Results.Unauthorized();
        }).RequireRateLimiting("player-public");

        app.MapPost("/api/public/player/refresh", async (
            PlayerRefreshRequest request,
            IPlatformPersonTokenService personTokenService,
            IPlayerTokenService tokenService,
            CancellationToken cancellationToken) =>
        {
            var session = await personTokenService.RefreshAsync(request.RefreshToken, cancellationToken);
            if (session is not null)
            {
                return Results.Ok(session);
            }

            var legacy = await tokenService.RefreshAsync(request, cancellationToken);
            return legacy is null ? Results.Unauthorized() : Results.Ok(legacy);
        }).RequireRateLimiting("player-public");

        // Телефон бывает общим, а планшет на стойке — тем более: выход обязан гасить токены на
        // сервере, иначе следующий в руках человек продолжит чужую сессию из сохранённого refresh.
        app.MapPost("/api/public/player/sign-out", async (
            PlayerSignOutRequest request,
            HttpContext httpContext,
            IPlatformPersonTokenService personTokenService,
            CancellationToken cancellationToken) =>
        {
            var revoked = await personTokenService.RevokeAsync(
                request.RefreshToken,
                ReadBearerToken(httpContext),
                cancellationToken);

            return revoked ? Results.NoContent() : Results.Unauthorized();
        }).RequireRateLimiting("player-public");

        // Каталог клубов для мобильного приложения: у него нет поддомена, из которого веб-сборка
        // берёт организацию, а вход без неё невозможен (игрок опознаётся парой организация+телефон).
        // Отдаём витрину и только её: статус, подписка, долги и лимиты сюда не попадают.
        app.MapGet("/api/public/organizations", async (
            string? query,
            PlatformDbContext dbContext,
            IMemoryCache cache,
            IOptions<PublicClubDirectoryOptions> directoryOptions,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
            Results.Ok(await PublicClubDirectory.GetAsync(
                dbContext, cache, directoryOptions.Value, query, timeProvider.GetUtcNow(), cancellationToken)))
            .RequireRateLimiting("player-public");

        organizations.MapPost("auth/staff/sign-in-by-phone", async (
            Guid organizationId,
            StaffSignInByPhoneRequest request,
            IStaffCredentialService credentialService,
            CancellationToken cancellationToken) =>
        {
            var outcome = await credentialService.SignInByPhoneAsync(request, cancellationToken);

            return outcome.SignedIn is { OrganizationId: var responseOrganizationId }
                && responseOrganizationId != organizationId
                    ? Results.StatusCode(StatusCodes.Status403Forbidden)
                    : SignInResult(outcome);
        });

        organizations.MapPost("account/phone/start-verification", async (
            StaffPhoneStartVerificationRequest request,
            IStaffContextAccessor staffContextAccessor,
            IStaffPhoneVerificationService verificationService,
            CancellationToken cancellationToken) =>
        {
            var staff = staffContextAccessor.Current;
            if (staff is null)
            {
                return Results.Unauthorized();
            }

            var result = await verificationService.StartAsync(
                staff.StaffUserId, staff.OrganizationId, request.Phone, cancellationToken);

            return result.Status switch
            {
                PhoneVerificationStartStatus.Sent => Results.Ok(
                    new StaffPhoneVerificationStartedResponse(result.ExpiresInSeconds, result.ResendAfterSeconds)),
                PhoneVerificationStartStatus.InvalidPhone => Results.BadRequest(new { error = "invalid_phone" }),
                PhoneVerificationStartStatus.CooldownActive => Results.Json(
                    new { error = "cooldown_active", resendAfterSeconds = result.ResendAfterSeconds },
                    statusCode: StatusCodes.Status429TooManyRequests),
                PhoneVerificationStartStatus.RateLimited => Results.Json(
                    new { error = "rate_limited" }, statusCode: StatusCodes.Status429TooManyRequests),
                PhoneVerificationStartStatus.SmsFailed => Results.Json(
                    new { error = "sms_unavailable" }, statusCode: StatusCodes.Status502BadGateway),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        });

        organizations.MapPost("account/phone/confirm", async (
            StaffPhoneConfirmRequest request,
            IStaffContextAccessor staffContextAccessor,
            IStaffPhoneVerificationService verificationService,
            CancellationToken cancellationToken) =>
        {
            var staff = staffContextAccessor.Current;
            if (staff is null)
            {
                return Results.Unauthorized();
            }

            var result = await verificationService.ConfirmAsync(staff.StaffUserId, request.Code, cancellationToken);

            return result.Status switch
            {
                PhoneConfirmStatus.Confirmed => Results.Ok(new StaffPhoneConfirmedResponse(result.VerifiedPhone!)),
                PhoneConfirmStatus.InvalidCode => Results.Json(
                    new { error = "invalid_code", remainingAttempts = result.RemainingAttempts },
                    statusCode: StatusCodes.Status400BadRequest),
                PhoneConfirmStatus.Expired => Results.Json(
                    new { error = "code_expired" }, statusCode: StatusCodes.Status410Gone),
                PhoneConfirmStatus.NoActiveCode => Results.Json(
                    new { error = "no_active_code" }, statusCode: StatusCodes.Status410Gone),
                PhoneConfirmStatus.TooManyAttempts => Results.Json(
                    new { error = "too_many_attempts" }, statusCode: StatusCodes.Status429TooManyRequests),
                PhoneConfirmStatus.PhoneAlreadyInUse => Results.Json(
                    new { error = "phone_already_in_use" }, statusCode: StatusCodes.Status409Conflict),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        });

        organizations.MapGet("account/phone", async (
            IStaffContextAccessor staffContextAccessor,
            IStaffPhoneVerificationService verificationService,
            CancellationToken cancellationToken) =>
        {
            var staff = staffContextAccessor.Current;
            if (staff is null)
            {
                return Results.Unauthorized();
            }

            var status = await verificationService.GetStatusAsync(staff.StaffUserId, cancellationToken);
            return Results.Ok(new StaffPhoneStatusResponse(status.Phone, status.PhoneVerifiedAtUtc));
        });

        app.MapPost("/api/auth/staff/forgot-password-by-phone", async (
            StaffForgotPasswordByPhoneRequest request,
            IStaffPhonePasswordResetService resetService,
            CancellationToken cancellationToken) =>
        {
            var result = await resetService.RequestResetAsync(request.PhoneNumber, cancellationToken);
            return result.Status switch
            {
                ForgotPasswordByPhoneStatus.Accepted => Results.Ok(
                    new { expiresInSeconds = result.ExpiresInSeconds, resendAfterSeconds = result.ResendAfterSeconds }),
                ForgotPasswordByPhoneStatus.InvalidPhone => Results.BadRequest(new { error = "invalid_phone" }),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        }).RequireRateLimiting("staff-reset");

        app.MapPost("/api/auth/staff/reset-password-by-phone", async (
            StaffResetPasswordByPhoneRequest request,
            IStaffPhonePasswordResetService resetService,
            CancellationToken cancellationToken) =>
        {
            var passwordValidation = ValidateStaffPin(request.NewPassword);
            if (passwordValidation is not null)
            {
                return Results.BadRequest(new { error = passwordValidation });
            }

            var result = await resetService.ResetAsync(
                request.PhoneNumber, request.Code, request.NewPassword, cancellationToken);
            return result.Status switch
            {
                ResetPasswordByPhoneStatus.Success => Results.Ok(new { message = "Password updated." }),
                ResetPasswordByPhoneStatus.InvalidCode => Results.Json(
                    new { error = "invalid_code", remainingAttempts = result.RemainingAttempts },
                    statusCode: StatusCodes.Status400BadRequest),
                ResetPasswordByPhoneStatus.Expired => Results.Json(
                    new { error = "code_expired" }, statusCode: StatusCodes.Status410Gone),
                ResetPasswordByPhoneStatus.NoActiveCode => Results.Json(
                    new { error = "code_expired" }, statusCode: StatusCodes.Status410Gone),
                ResetPasswordByPhoneStatus.TooManyAttempts => Results.Json(
                    new { error = "too_many_attempts" }, statusCode: StatusCodes.Status429TooManyRequests),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        }).RequireRateLimiting("staff-reset");

    }

    // Выход гасит именно тот access, которым подписан запрос, поэтому строку из заголовка читаем
    // здесь: контекст сотрудника его уже не несёт, а отзывать все токены владельца — значит
    // выкинуть его же со второй машины.
    private static string? ReadBearerToken(HttpContext httpContext)
    {
        const string bearerPrefix = "Bearer ";
        var authorization = httpContext.Request.Headers.Authorization.ToString();
        return authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[bearerPrefix.Length..].Trim()
            : null;
    }
}
