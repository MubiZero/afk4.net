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
using System.Threading.RateLimiting;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(
        this WebApplication app,
        IEndpointRouteBuilder organizations)
    {
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

            var response = await credentialService.SignInAsync(request, cancellationToken);

            return response is null
                ? Results.Unauthorized()
                : Results.Ok(response);
        });

        organizations.MapPost("auth/staff/sign-in-by-organization-key", async (
            Guid organizationId,
            StaffSignInByOrganizationKeyRequest request,
            IStaffCredentialService credentialService,
            CancellationToken cancellationToken) =>
        {
            var response = await credentialService.SignInByOrganizationKeyAsync(request, cancellationToken);

            return response switch
            {
                null => Results.Unauthorized(),
                { OrganizationId: var responseOrganizationId } when responseOrganizationId != organizationId
                    => Results.StatusCode(StatusCodes.Status403Forbidden),
                _ => Results.Ok(response)
            };
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

            return resolution.SignedIn is null
                ? Results.Unauthorized()
                : Results.Ok(resolution.SignedIn);
        });

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

        app.MapPost("/api/public/player/sign-in", async (
            PlayerSignInRequest request,
            IPlayerCredentialService credentialService,
            CancellationToken cancellationToken) =>
        {
            var response = await credentialService.SignInAsync(request, cancellationToken);
            return response is null ? Results.Unauthorized() : Results.Ok(response);
        }).RequireRateLimiting("player-public");

        // Вход по SMS-коду: телефон и так опознаёт игрока, а код доказывает владение им не хуже
        // пароля — и его нечего забывать. Ответ на просьбу прислать код одинаков для известного и
        // неизвестного номера: разница превратила бы эндпоинт в справочник «кто где играет».
        app.MapPost("/api/public/player/sign-in/code", async (
            PlayerCodeSignInStartRequest request,
            IPlayerPhoneSignInService signInService,
            CancellationToken cancellationToken) =>
        {
            var result = await signInService.StartAsync(
                request.OrganizationId, request.PhoneNumber, cancellationToken);

            return result.Status switch
            {
                PhoneVerificationStartStatus.Sent => Results.Ok(
                    new PlayerCodeSignInStartedResponse(result.ExpiresInSeconds, result.ResendAfterSeconds)),
                PhoneVerificationStartStatus.InvalidPhone => Results.BadRequest(new { error = "invalid_phone" }),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        }).RequireRateLimiting("player-public");

        app.MapPost("/api/public/player/sign-in/code/confirm", async (
            PlayerCodeSignInRequest request,
            IPlayerPhoneSignInService signInService,
            CancellationToken cancellationToken) =>
        {
            var result = await signInService.ConfirmAsync(
                request.OrganizationId, request.PhoneNumber, request.Code, cancellationToken);

            return result.Status switch
            {
                PlayerCodeSignInStatus.SignedIn => Results.Ok(result.Tokens),
                PlayerCodeSignInStatus.InvalidCode => Results.Json(
                    new { error = "invalid_code", remainingAttempts = result.RemainingAttempts },
                    statusCode: StatusCodes.Status400BadRequest),
                PlayerCodeSignInStatus.Expired => Results.Json(
                    new { error = "code_expired" }, statusCode: StatusCodes.Status410Gone),
                PlayerCodeSignInStatus.NoActiveCode => Results.Json(
                    new { error = "no_active_code" }, statusCode: StatusCodes.Status410Gone),
                PlayerCodeSignInStatus.TooManyAttempts => Results.Json(
                    new { error = "too_many_attempts" }, statusCode: StatusCodes.Status429TooManyRequests),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        }).RequireRateLimiting("player-public");

        app.MapPost("/api/public/player/refresh", async (
            PlayerRefreshRequest request,
            IPlayerTokenService tokenService,
            CancellationToken cancellationToken) =>
        {
            var response = await tokenService.RefreshAsync(request, cancellationToken);
            return response is null ? Results.Unauthorized() : Results.Ok(response);
        }).RequireRateLimiting("player-public");

        // Каталог клубов для мобильного приложения: у него нет поддомена, из которого веб-сборка
        // берёт организацию, а вход без неё невозможен (игрок опознаётся парой организация+телефон).
        // Отдаём витрину и только её: статус, подписка, долги и лимиты сюда не попадают.
        app.MapGet("/api/public/organizations", async (
            string? query,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            const int maxResults = 50;

            var organizations = dbContext.Organizations
                .AsNoTracking()
                .Where(o => o.Status == "active");

            var trimmed = query?.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                // ToLower().Contains(), а не EF.Functions.ILike: ILike — расширение Npgsql, и на
                // InMemory (где идут эти тесты) оно роняет запрос в 500. Каталог отдаёт максимум
                // 50 строк, так что отсутствие индексного поиска здесь ничего не стоит.
                var needle = trimmed.ToLowerInvariant();
                organizations = organizations.Where(o =>
                    o.Name.ToLower().Contains(needle) || o.Slug.ToLower().Contains(needle));
            }

            // Порядок по имени, а не по времени создания: список должен выглядеть одинаково при
            // каждом открытии, иначе выбранный глазом клуб уезжает под пальцем.
            var found = await organizations
                .OrderBy(o => o.Name)
                .Take(maxResults)
                .Select(o => new { o.OrganizationId, o.Slug, o.Name, o.LogoUrl, o.AccentColor })
                .ToListAsync(cancellationToken);

            var organizationIds = found.Select(o => o.OrganizationId).ToList();

            // Витрина собирается тремя отдельными запросами и склеивается в памяти, а не одним
            // выражением с группировками: клубов здесь максимум полсотни, зато запросы остаются
            // такими, какие одинаково выполняет и Postgres, и InMemory под тестами.
            var branches = await dbContext.Branches
                .AsNoTracking()
                .Where(b => organizationIds.Contains(b.OrganizationId))
                .OrderBy(b => b.City).ThenBy(b => b.Name)
                .Select(b => new
                {
                    b.OrganizationId, b.BranchId, b.Name, b.City, b.Address, b.Description,
                    b.CoverImageUrl, b.Latitude, b.Longitude, b.WorkingHoursJson
                })
                .ToListAsync(cancellationToken);

            var places = branches.Select(b => new
            {
                b.OrganizationId,
                Place = new ClubPlaceDto(
                    b.BranchId, b.Name, b.City, b.Address, b.Description,
                    b.CoverImageUrl, b.Latitude, b.Longitude,
                    AFK4.Platform.Api.Branches.BranchWorkingHours.Deserialize(b.WorkingHoursJson))
            }).ToList();

            var seatCounts = await dbContext.Seats
                .AsNoTracking()
                .Where(s => organizationIds.Contains(s.OrganizationId))
                .GroupBy(s => s.OrganizationId)
                .Select(group => new { OrganizationId = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken);

            // «Цена от» — по действующим версиям тарифов: снятые с публикации и ещё не вступившие
            // в силу обещали бы игроку цену, которую на кассе никто не назовёт.
            var now = DateTimeOffset.UtcNow;
            var prices = await dbContext.TariffVersions
                .AsNoTracking()
                .Where(v => organizationIds.Contains(v.OrganizationId)
                    && v.RetiredAtUtc == null
                    && v.EffectiveFromUtc <= now)
                .Select(v => new { v.OrganizationId, v.PricePerMinuteMinorUnits, v.CurrencyCode })
                .ToListAsync(cancellationToken);

            // Оценка клуба — то же, что цена и адрес: часть витрины, по которой выбирают. Считаем
            // её здесь же, одним запросом на всю страницу каталога.
            var ratings = await dbContext.ClubReviews
                .AsNoTracking()
                .Where(review => organizationIds.Contains(review.OrganizationId))
                .GroupBy(review => review.OrganizationId)
                .Select(group => new
                {
                    OrganizationId = group.Key,
                    Average = group.Average(review => (double)review.Rating),
                    Count = group.Count()
                })
                .ToListAsync(cancellationToken);

            var entries = found.Select(o =>
            {
                var rating = ratings.FirstOrDefault(r => r.OrganizationId == o.OrganizationId);
                var cheapest = prices
                    .Where(p => p.OrganizationId == o.OrganizationId)
                    .OrderBy(p => p.PricePerMinuteMinorUnits)
                    .FirstOrDefault();

                return new OrganizationDirectoryEntryDto(
                    o.OrganizationId,
                    o.Slug,
                    o.Name,
                    o.LogoUrl,
                    o.AccentColor,
                    places.Where(p => p.OrganizationId == o.OrganizationId)
                        .Select(p => p.Place)
                        .ToList(),
                    cheapest is null ? null : cheapest.PricePerMinuteMinorUnits * 60,
                    cheapest?.CurrencyCode,
                    seatCounts.FirstOrDefault(c => c.OrganizationId == o.OrganizationId)?.Count ?? 0,
                    rating is null ? null : Math.Round(rating.Average, 1),
                    rating?.Count ?? 0);
            }).ToList();

            return Results.Ok(entries);
        }).RequireRateLimiting("player-public");

        app.MapGet("/api/public/organization/{organizationKey}/branding", async (
            string organizationKey,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var normalizedKey = SlugValidator.Normalize(organizationKey);
            var org = await dbContext.Organizations
                .AsNoTracking()
                .Where(o => o.Slug == normalizedKey && o.Status == "active")
                .Select(o => new OrganizationBrandingDto(o.OrganizationId, o.Name, o.LogoUrl, o.AccentColor))
                .FirstOrDefaultAsync(cancellationToken);
            return org is null ? Results.NotFound() : Results.Ok(org);
        }).RequireRateLimiting("player-public");

        organizations.MapPost("auth/staff/sign-in-by-phone", async (
            Guid organizationId,
            StaffSignInByPhoneRequest request,
            IStaffCredentialService credentialService,
            CancellationToken cancellationToken) =>
        {
            var signedIn = await credentialService.SignInByPhoneAsync(request, cancellationToken);
            return signedIn switch
            {
                null => Results.Unauthorized(),
                { OrganizationId: var responseOrganizationId } when responseOrganizationId != organizationId
                    => Results.StatusCode(StatusCodes.Status403Forbidden),
                _ => Results.Ok(signedIn)
            };
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
            var passwordValidation = ValidateStaffPassword(request.NewPassword);
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
}
