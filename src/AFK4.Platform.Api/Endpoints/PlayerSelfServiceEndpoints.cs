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
using AFK4.Platform.Api.Install;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Outbox;
using AFK4.Platform.Api.Payments;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Platform.Api.Platform.Entitlements;
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
using AFK4.Shared.Contracts.Platform.Features;
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

internal static class PlayerSelfServiceEndpoints
{
    public static void MapPlayerSelfServiceEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me/profile", async (
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var account = await dbContext.PlayerAccounts.SingleOrDefaultAsync(
                p => p.PlayerAccountId == player.PlayerAccountId, cancellationToken);
            if (account is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await ToProfileDtoAsync(dbContext, account, player.PhoneVerified, cancellationToken));
        }).RequireRateLimiting("player-me");

        app.MapPatch("/api/me/profile", async (
            UpdatePlayerProfileRequest request,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var account = await dbContext.PlayerAccounts.SingleOrDefaultAsync(
                candidate => candidate.PlayerAccountId == player.PlayerAccountId, cancellationToken);
            if (account is null)
            {
                return Results.Unauthorized();
            }

            if (request.PreferredLocale is not null)
            {
                var locale = request.PreferredLocale.Trim();
                if (locale.Length is 0 or > 16)
                {
                    return Results.BadRequest(new { Error = "PreferredLocale must be 1-16 characters." });
                }

                account.PreferredLocale = locale;
            }

            if (request.MarketingOptIn is not null)
            {
                account.MarketingOptIn = request.MarketingOptIn.Value;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(await ToProfileDtoAsync(dbContext, account, player.PhoneVerified, cancellationToken));
        }).RequireRateLimiting("player-me");

        // Подтверждение своего номера по SMS. Без него онлайн-пополнение и онлайн-бронь
        // закрыты, а раньше открыть их было нечем: интерфейс отправлял игрока к администратору
        // клуба, у которого такой возможности тоже не было.
        app.MapPost("/api/me/phone/start-verification", async (
            PlayerPhoneStartVerificationRequest request,
            IPlayerContextAccessor playerContextAccessor,
            IPlayerPhoneVerificationService verificationService,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var result = await verificationService.StartAsync(
                player.PlayerAccountId, request.Phone, cancellationToken);

            return result.Status switch
            {
                PhoneVerificationStartStatus.Sent => Results.Ok(
                    new PlayerPhoneVerificationStartedResponse(result.ExpiresInSeconds, result.ResendAfterSeconds)),
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
        }).RequireRateLimiting("player-me");

        app.MapPost("/api/me/phone/confirm", async (
            PlayerPhoneConfirmRequest request,
            IPlayerContextAccessor playerContextAccessor,
            IPlayerPhoneVerificationService verificationService,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var result = await verificationService.ConfirmAsync(
                player.PlayerAccountId, request.Code, cancellationToken);

            return result.Status switch
            {
                PhoneConfirmStatus.Confirmed => Results.Ok(new PlayerPhoneConfirmedResponse(result.VerifiedPhone!)),
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
        }).RequireRateLimiting("player-me");

        app.MapGet("/api/me/phone", async (
            IPlayerContextAccessor playerContextAccessor,
            IPlayerPhoneVerificationService verificationService,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var status = await verificationService.GetStatusAsync(player.PlayerAccountId, cancellationToken);
            return Results.Ok(new PlayerPhoneStatusResponse(status.Phone, status.PhoneVerifiedAtUtc));
        }).RequireRateLimiting("player-me");

        app.MapGet("/api/me/dashboard", async (
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var dashboard = await PlayerDashboardProjector.GetDashboardAsync(
                dbContext, player.PlayerAccountId, timeProvider.GetUtcNow(), cancellationToken);
            return Results.Ok(dashboard);
        }).RequireRateLimiting("player-me");

        app.MapGet("/api/me/visits", async (
            string? cursor,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var page = await PlayerHistoryProjector.GetVisitsAsync(
                dbContext, player.PlayerAccountId, cursor, cancellationToken);
            return Results.Ok(page);
        }).RequireRateLimiting("player-me");

        app.MapGet("/api/me/visits/{sessionId:guid}/receipt", async (
            Guid sessionId,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var receipt = await PlayerHistoryProjector.GetVisitReceiptAsync(
                dbContext, player.PlayerAccountId, sessionId, cancellationToken);
            return receipt is null ? Results.NotFound() : Results.Ok(receipt);
        }).RequireRateLimiting("player-me");

        app.MapGet("/api/me/purchases", async (
            string? cursor,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var page = await PlayerHistoryProjector.GetPurchasesAsync(
                dbContext, player.PlayerAccountId, cursor, cancellationToken);
            return Results.Ok(page);
        }).RequireRateLimiting("player-me");

        app.MapPost("/api/me/wallet/top-up-intent", async (
            PlayerTopUpIntentRequest request,
            IPlayerContextAccessor playerContextAccessor,
            IPlatformPersonContextAccessor personContextAccessor,
            IPlayerClubMembershipService clubMembership,
            AFK4.Platform.Api.Payments.Eskhata.IEskhataMerchantClientFactory eskhataClientFactory,
            IOrganizationEntitlements entitlements,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            // Клуб известен до счёта. Порядок здесь важен: отказ клуба и кривая сумма не должны
            // оставлять после себя карточку гостя, который в клуб так и не пришёл.
            var selectedOrganizationId = playerContextAccessor.Current?.OrganizationId
                ?? personContextAccessor.Current?.SelectedOrganizationId;
            if (selectedOrganizationId is null)
            {
                return Results.Unauthorized();
            }

            var featureDenial = await entitlements.RequireAsync(
                selectedOrganizationId.Value, PlatformFeatureNames.OnlineTopUp, cancellationToken);
            if (featureDenial is not null)
            {
                return featureDenial;
            }

            if (request.AmountMinorUnits <= 0)
            {
                return Results.BadRequest(new { Error = "Amount must be greater than zero." });
            }

            var method = string.IsNullOrWhiteSpace(request.Method)
                ? "counter"
                : request.Method.Trim().ToLowerInvariant();
            if (method != "counter" && method != "eskhata")
            {
                return Results.BadRequest(new { Error = "Method must be 'counter' or 'eskhata'." });
            }

            var clubDenial = await OpenClubAccountIfNeededAsync(
                playerContextAccessor, personContextAccessor, clubMembership,
                request.BranchId, cancellationToken);
            if (clubDenial is not null)
            {
                return clubDenial;
            }

            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var account = await dbContext.PlayerAccounts.SingleOrDefaultAsync(
                candidate => candidate.PlayerAccountId == player.PlayerAccountId, cancellationToken);
            if (account is null)
            {
                return Results.Unauthorized();
            }

            var currencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
                ? "TJS"
                : request.CurrencyCode.Trim().ToUpperInvariant();

            var now = timeProvider.GetUtcNow();
            var intent = new PaymentIntentEntity
            {
                PaymentIntentId = Guid.NewGuid(),
                PlayerAccountId = player.PlayerAccountId,
                OrganizationId = player.OrganizationId,
                BranchId = account.HomeBranchId,
                AmountMinorUnits = request.AmountMinorUnits,
                CurrencyCode = currencyCode,
                Purpose = "wallet_topup",
                State = "pending",
                Method = method,
                FulfilledByLedgerEntryId = null,
                CreatedAtUtc = now,
                FulfilledAtUtc = null
            };

            if (method == "eskhata")
            {
                var eskhataClient = await eskhataClientFactory.CreateForOrganizationAsync(
                    intent.OrganizationId, cancellationToken);
                if (eskhataClient is null)
                {
                    return Results.Json(
                        new { Error = "online_payment_unavailable" },
                        statusCode: StatusCodes.Status409Conflict);
                }

                var merchantId = await ResolveEskhataMerchantIdAsync(
                    dbContext, intent.OrganizationId, cancellationToken);
                if (merchantId is null)
                {
                    return Results.Json(
                        new { Error = "online_payment_unavailable" },
                        statusCode: StatusCodes.Status409Conflict);
                }

                var order = await eskhataClient.CreateOrderAsync(
                    intent.PaymentIntentId.ToString("N"),
                    intent.AmountMinorUnits,
                    intent.CurrencyCode == "TJS" ? "972" : intent.CurrencyCode,
                    "AFK4 wallet top-up",
                    merchantId.Value,
                    cancellationToken);

                intent.GatewayPaymentId = order.OrderId;
                intent.GatewayPayUrl = order.InvoiceUrl;
                intent.GatewayQrPayload = order.Qr;
                intent.GatewayPosId = order.PosId;
            }

            dbContext.PaymentIntents.Add(intent);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new PlayerTopUpIntentDto(
                intent.PaymentIntentId,
                intent.AmountMinorUnits,
                intent.CurrencyCode,
                intent.State,
                intent.Purpose,
                intent.Method,
                intent.CreatedAtUtc,
                intent.FulfilledAtUtc,
                IsExpired: false,
                PayUrl: intent.GatewayPayUrl,
                Comment: intent.GatewayComment,
                GatewayExpiresAtUtc: intent.GatewayExpiresAtUtc,
                Qr: intent.GatewayQrPayload,
                DeepLink: AFK4.Platform.Api.Payments.Eskhata.EskhataDeepLink.FromInvoiceUrl(intent.GatewayPayUrl)));
        }).RequireRateLimiting("player-me").OpensClubAccount();

        app.MapGet("/api/me/wallet/top-up-intents", async (
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var now = timeProvider.GetUtcNow();
            var expiryCutoff = now.AddHours(-24);

            var intents = await dbContext.PaymentIntents
                .AsNoTracking()
                .Where(intent => intent.PlayerAccountId == player.PlayerAccountId)
                .OrderByDescending(intent => intent.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            var dtos = intents.Select(intent => new PlayerTopUpIntentDto(
                intent.PaymentIntentId,
                intent.AmountMinorUnits,
                intent.CurrencyCode,
                intent.State,
                intent.Purpose,
                intent.Method,
                intent.CreatedAtUtc,
                intent.FulfilledAtUtc,
                IsExpired: intent.State == "pending" && intent.CreatedAtUtc < expiryCutoff,
                PayUrl: intent.GatewayPayUrl,
                Comment: intent.GatewayComment,
                GatewayExpiresAtUtc: intent.GatewayExpiresAtUtc))
                .ToList();

            return Results.Ok(dtos);
        }).RequireRateLimiting("player-me");

        // Intentionally no online_topup feature gate on this route. The intent behind it was
        // already legally created while the feature was on (that creation path IS gated, at
        // POST .../top-up-intent) — the player may already have paid the bank by the time this
        // polls the gateway. Gating here would mean the club's card charge succeeds but the wallet
        // credit doesn't: the player paid into a switch flipped between their tap and their bank
        // confirmation. Disabling the feature stops new intents, not payments already in flight.
        // See FeatureGateTests.TopUp_StillCreditsInFlightPayment_WhenDisabledAfterIntentCreated —
        // that test fails on purpose if this route grows a gate later.
        app.MapPost("/api/me/wallet/top-up-intents/{intentId:guid}/eskhata-status", async (
            Guid intentId,
            IPlayerContextAccessor playerContextAccessor,
            AFK4.Platform.Api.Payments.Eskhata.IEskhataMerchantClientFactory eskhataClientFactory,
            IBillingCommandService billingCommandService,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var intent = await dbContext.PaymentIntents.SingleOrDefaultAsync(
                x => x.PaymentIntentId == intentId && x.PlayerAccountId == player.PlayerAccountId,
                cancellationToken);
            if (intent is null || intent.Method != "eskhata")
            {
                return Results.NotFound();
            }

            if (intent.State == "fulfilled")
            {
                return Results.Ok(new { payment = "paid" });
            }
            if (intent.State is "cancelled" or "expired")
            {
                return Results.Ok(new { payment = "failed" });
            }

            var eskhataClient = await eskhataClientFactory.CreateForOrganizationAsync(
                intent.OrganizationId, cancellationToken);
            if (eskhataClient is null)
            {
                return Results.Ok(new { payment = "pending" });
            }

            var status = await eskhataClient.GetOrderStatusAsync(
                intent.PaymentIntentId.ToString("N"),
                intent.GatewayPaymentId ?? "",
                intent.AmountMinorUnits,
                "972",
                intent.GatewayPosId ?? 0,
                cancellationToken);

            if (status == "COMPLETED")
            {
                var topUpRequest = new TopUpWalletRequest(
                    intent.OrganizationId,
                    new MoneyDto(intent.CurrencyCode, intent.AmountMinorUnits),
                    "eskhata_online_topup",
                    intent.PaymentIntentId.ToString("N"));

                var billingResult = await billingCommandService.CreditOnlineTopUpAsync(
                    intent.PlayerAccountId, intent.BranchId, topUpRequest, cancellationToken);
                if (billingResult.Succeeded)
                {
                    intent.State = "fulfilled";
                    intent.FulfilledAtUtc = timeProvider.GetUtcNow();
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return Results.Ok(new { payment = "paid" });
                }
            }

            if (status is "CANCELED" or "REFUNDED")
            {
                return Results.Ok(new { payment = "failed" });
            }

            return Results.Ok(new { payment = "pending" });
        }).RequireRateLimiting("player-me");

        app.MapPost("/api/me/reservations", async (
            CreatePlayerReservationRequest request,
            IPlayerContextAccessor playerContextAccessor,
            IPlatformPersonContextAccessor personContextAccessor,
            IPlayerClubMembershipService clubMembership,
            IReservationService reservationService,
            IOrganizationEntitlements entitlements,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            // Порядок: сначала всё, из-за чего бронь может не состояться, и только потом счёт.
            // Иначе каждая отклонённая попытка оставляла бы клубу карточку несуществующего гостя.
            var selectedOrganizationId = playerContextAccessor.Current?.OrganizationId
                ?? personContextAccessor.Current?.SelectedOrganizationId;
            if (selectedOrganizationId is null)
            {
                return Results.Unauthorized();
            }

            var featureDenial = await entitlements.RequireAsync(
                selectedOrganizationId.Value, PlatformFeatureNames.OnlineBooking, cancellationToken);
            if (featureDenial is not null)
            {
                return featureDenial;
            }

            // D8 gate: бронировать может только подтверждённый номер. Подтверждение принадлежит
            // человеку, а не клубной карточке, поэтому спрашиваем его до открытия счёта.
            var phoneVerified = playerContextAccessor.Current?.PhoneVerified
                ?? personContextAccessor.Current?.PhoneVerified ?? false;
            if (!phoneVerified)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var now = timeProvider.GetUtcNow();
            if (request.StartsAtUtc >= request.EndsAtUtc)
            {
                return Results.BadRequest(new { Error = "End time must be after start time." });
            }

            if (request.StartsAtUtc <= now)
            {
                return Results.BadRequest(new { Error = "Start time must be in the future." });
            }

            var clubDenial = await OpenClubAccountIfNeededAsync(
                playerContextAccessor, personContextAccessor, clubMembership,
                request.BranchId, cancellationToken);
            if (clubDenial is not null)
            {
                return clubDenial;
            }

            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var account = await dbContext.PlayerAccounts
                .AsNoTracking()
                .SingleOrDefaultAsync(a => a.PlayerAccountId == player.PlayerAccountId, cancellationToken);
            if (account is null)
            {
                return Results.Unauthorized();
            }

            var result = await reservationService.CreateOnlineAsync(
                player.PlayerAccountId,
                player.OrganizationId,
                account.HomeBranchId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                if (result.Conflict)
                {
                    return Results.Conflict(new { Error = result.Error });
                }

                if (result.NotFound)
                {
                    return Results.NotFound(new { Error = result.Error });
                }

                // Отказ по решению клуба — это состояние, а не кривой запрос: приложению нужен
                // код, по которому оно скажет «так решил клуб», а не «проверьте поля».
                return PlayerBookingRules.IsClubRuleRefusal(result.Error)
                    ? Results.Conflict(new { Error = result.Error })
                    : Results.BadRequest(new { Error = result.Error });
            }

            return Results.Ok(ToPlayerReservationDto(result.Response!));
        }).RequireRateLimiting("player-me").OpensClubAccount();

        // Бронь на компанию: несколько мест на одно время одним действием. Мест здесь количество,
        // а не список, — конкретную машину игроку в приложении не выбирают, её назначает клуб.
        app.MapPost("/api/me/reservations/group", async (
            CreatePlayerReservationGroupRequest request,
            IPlayerContextAccessor playerContextAccessor,
            IPlatformPersonContextAccessor personContextAccessor,
            IPlayerClubMembershipService clubMembership,
            IReservationService reservationService,
            IOrganizationEntitlements entitlements,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            // Порядок: сначала всё, из-за чего бронь может не состояться, и только потом счёт.
            // Иначе каждая отклонённая попытка оставляла бы клубу карточку несуществующего гостя.
            var selectedOrganizationId = playerContextAccessor.Current?.OrganizationId
                ?? personContextAccessor.Current?.SelectedOrganizationId;
            if (selectedOrganizationId is null)
            {
                return Results.Unauthorized();
            }

            var featureDenial = await entitlements.RequireAsync(
                selectedOrganizationId.Value, PlatformFeatureNames.OnlineBooking, cancellationToken);
            if (featureDenial is not null)
            {
                return featureDenial;
            }

            var phoneVerified = playerContextAccessor.Current?.PhoneVerified
                ?? personContextAccessor.Current?.PhoneVerified ?? false;
            if (!phoneVerified)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var now = timeProvider.GetUtcNow();
            if (request.StartsAtUtc >= request.EndsAtUtc)
            {
                return Results.BadRequest(new { Error = "End time must be after start time." });
            }

            if (request.StartsAtUtc <= now)
            {
                return Results.BadRequest(new { Error = "Start time must be in the future." });
            }

            var clubDenial = await OpenClubAccountIfNeededAsync(
                playerContextAccessor, personContextAccessor, clubMembership,
                request.BranchId, cancellationToken);
            if (clubDenial is not null)
            {
                return clubDenial;
            }

            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var account = await dbContext.PlayerAccounts
                .AsNoTracking()
                .SingleOrDefaultAsync(a => a.PlayerAccountId == player.PlayerAccountId, cancellationToken);
            if (account is null)
            {
                return Results.Unauthorized();
            }

            var result = await reservationService.CreateOnlineGroupAsync(
                player.PlayerAccountId,
                player.OrganizationId,
                account.HomeBranchId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                if (result.Conflict)
                {
                    return Results.Conflict(new { Error = result.Error });
                }

                if (result.NotFound)
                {
                    return Results.NotFound(new { Error = result.Error });
                }

                // Отказ по деньгам, по числу мест и по решению клуба — машинные коды, интерфейсы
                // их переводят.
                return result.Error is "insufficient_funds" or PlayerReservationGroupLimits.InvalidSeatCountCode
                    || PlayerBookingRules.IsClubRuleRefusal(result.Error)
                    ? Results.Conflict(new { Error = result.Error })
                    : Results.BadRequest(new { Error = result.Error });
            }

            var reservations = result.Response!.Select(ToPlayerReservationDto).ToList();
            var first = reservations[0];
            return Results.Ok(new PlayerReservationGroupDto(
                first.ReservationGroupId!.Value,
                reservations,
                // Сумма по всей компании — то, что действительно заморожено.
                first.EstimatedCostMinorUnits is { } perSeat ? perSeat * reservations.Count : null,
                first.CurrencyCode));
        }).RequireRateLimiting("player-me").OpensClubAccount();

        // Отмена всей компании разом: передумали идти все, и четыре отдельных отмены — это четыре
        // шанса оборваться на полпути, оставив часть денег замороженной.
        app.MapDelete("/api/me/reservations/group/{reservationGroupId:guid}", async (
            Guid reservationGroupId,
            IPlayerContextAccessor playerContextAccessor,
            IReservationService reservationService,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var result = await reservationService.CancelOnlineGroupAsync(
                reservationGroupId, player.PlayerAccountId, cancellationToken);

            if (result.NotFound)
            {
                return Results.NotFound();
            }

            if (!result.Succeeded)
            {
                return Results.BadRequest(new { Error = result.Error });
            }

            return Results.Ok(result.Response!.Select(ToPlayerReservationDto).ToList());
        }).RequireRateLimiting("player-me");

        app.MapGet("/api/me/reservations", async (
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var reservations = await dbContext.Reservations
                .AsNoTracking()
                .Where(reservation => reservation.PlayerAccountId == player.PlayerAccountId)
                .OrderByDescending(reservation => reservation.StartsAtUtc)
                .ToListAsync(cancellationToken);

            var seatIds = reservations
                .Where(r => r.SeatId is not null)
                .Select(r => r.SeatId!.Value)
                .Distinct()
                .ToList();

            var seatNames = seatIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await dbContext.Seats
                    .AsNoTracking()
                    .Where(seat => seatIds.Contains(seat.SeatId))
                    .ToDictionaryAsync(seat => seat.SeatId, seat => seat.Name, cancellationToken);

            // Имя тарифа берётся по версии, а не по действующему прайсу: версия могла быть снята
            // с публикации после брони, а показать надо то, на что игрок согласился.
            var tariffVersionIds = reservations
                .Where(r => r.TariffVersionId is not null)
                .Select(r => r.TariffVersionId!.Value)
                .Distinct()
                .ToList();

            var tariffNames = tariffVersionIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await dbContext.TariffVersions
                    .AsNoTracking()
                    .Where(version => tariffVersionIds.Contains(version.TariffVersionId))
                    .Join(
                        dbContext.Tariffs.AsNoTracking(),
                        version => version.TariffId,
                        tariff => tariff.TariffId,
                        (version, tariff) => new { version.TariffVersionId, tariff.Name })
                    .ToDictionaryAsync(row => row.TariffVersionId, row => row.Name, cancellationToken);

            var dtos = reservations.Select(r => new PlayerReservationDto(
                r.ReservationId,
                r.SeatId,
                r.SeatId is not null ? seatNames.GetValueOrDefault(r.SeatId.Value) : null,
                r.StartsAtUtc,
                r.EndsAtUtc,
                r.State,
                string.IsNullOrEmpty(r.Note) ? null : r.Note,
                r.TariffVersionId,
                r.TariffVersionId is not null ? tariffNames.GetValueOrDefault(r.TariffVersionId.Value) : null,
                r.EstimatedCostMinorUnits,
                r.CurrencyCode,
                r.ReservationGroupId,
                r.RespondByUtc,
                r.RejectReasonCode,
                r.RejectReasonNote))
                .ToList();

            return Results.Ok(dtos);
        }).RequireRateLimiting("player-me");

        app.MapDelete("/api/me/reservations/{reservationId:guid}", async (
            Guid reservationId,
            IPlayerContextAccessor playerContextAccessor,
            IReservationService reservationService,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var result = await reservationService.CancelOnlineAsync(
                reservationId,
                player.PlayerAccountId,
                cancellationToken);

            if (result.NotFound)
            {
                return Results.NotFound();
            }

            if (!result.Succeeded)
            {
                return Results.BadRequest(new { Error = result.Error });
            }

            return Results.Ok(ToPlayerReservationDto(result.Response!));
        }).RequireRateLimiting("player-me");

        app.MapPost("/api/me/sessions/start", async (
            PlayerSelfStartRequest request,
            IPlayerContextAccessor playerContextAccessor,
            IPlatformPersonContextAccessor personContextAccessor,
            IPlayerClubMembershipService clubMembership,
            PlatformDbContext dbContext,
            ISessionCommandService sessionCommandService,
            CancellationToken cancellationToken) =>
        {
            // Клуб известен ещё до счёта: человек стоит у конкретного ПК конкретного клуба.
            // Проверка принадлежности устройства этому клубу остаётся на месте — иначе по номеру
            // чужой машины можно было бы сесть в клубе, который тебя не звал.
            var organizationId = playerContextAccessor.Current?.OrganizationId
                ?? personContextAccessor.Current?.SelectedOrganizationId;
            if (organizationId is null) return Results.Unauthorized();

            var assignment = await (
                from a in dbContext.DeviceSeatAssignments.AsNoTracking()
                join d in dbContext.Devices.AsNoTracking() on a.DeviceId equals d.DeviceId
                where a.DeviceId == request.DeviceId &&
                      a.OrganizationId == organizationId &&
                      a.DetachedAtUtc == null &&
                      d.EnrollmentState == DeviceEnrollmentStateNames.Approved
                orderby a.AttachedAtUtc descending
                select a).FirstOrDefaultAsync(cancellationToken);
            if (assignment is null) return Results.NotFound(new { error = "device_not_assigned" });

            if (!Guid.TryParse(request.TariffRuleVersionId, out var tariffVersionId))
                return Results.BadRequest(new { error = "invalid_tariff" });

            var version = await dbContext.TariffVersions.AsNoTracking().SingleOrDefaultAsync(
                v => v.OrganizationId == organizationId &&
                     v.BranchId == assignment.BranchId &&
                     v.TariffVersionId == tariffVersionId, cancellationToken);
            if (version is null) return Results.BadRequest(new { error = "invalid_tariff" });

            var pricing = new TariffPricing(
                version.PricePerMinuteMinorUnits, version.MinimumBillableMinutes,
                version.RoundingIncrementMinutes, version.CurrencyCode);
            var charge = TariffBilling.ComputeForMinutes(request.DurationMinutes, pricing);
            if (charge is null) return Results.BadRequest(new { error = "invalid_duration" });

            // Счёт открывается последним шагом перед делом, и филиал берётся из привязки машины,
            // а не угадывается: человек сидит именно здесь. Отклонённая попытка не должна
            // оставлять клубу карточку гостя, который так и не сел.
            var clubDenial = await OpenClubAccountIfNeededAsync(
                playerContextAccessor, personContextAccessor, clubMembership,
                assignment.BranchId, cancellationToken);
            if (clubDenial is not null) return clubDenial;

            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();

            var wallet = await LedgerBalanceProjector.GetWalletSummaryAsync(
                dbContext, player.PlayerAccountId, cancellationToken);
            var walletBalance = wallet?.WalletBalance.MinorUnits ?? 0;
            if (walletBalance < charge.AmountMinorUnits)
                return Results.Conflict(new { error = "insufficient_balance" });

            var startRequest = new StartGuestSessionRequest(
                OrganizationId: player.OrganizationId,
                SeatId: assignment.SeatId,
                TariffRuleVersionId: request.TariffRuleVersionId,
                IdempotencyKey: request.IdempotencyKey,
                DurationMode: SessionDurationModes.Fixed,
                DurationMinutes: request.DurationMinutes,
                PlayerAccountId: player.PlayerAccountId,
                BillingMode: BillingModeNames.PrepaidWallet,
                TariffVersionId: tariffVersionId);

            var result = await sessionCommandService.StartGuestSessionAsync(
                assignment.BranchId, Guid.Empty, startRequest, cancellationToken);

            if (result.Conflict) return Results.Conflict(new { error = result.Error });
            if (result.NotFound) return Results.NotFound(new { error = result.Error });
            if (!result.Succeeded) return Results.BadRequest(new { error = result.Error });
            return Results.Ok(result.Response);
        }).RequireRateLimiting("player-me").OpensClubAccount();

        app.MapPost("/api/me/sessions/{sessionId:guid}/extend", async (
            Guid sessionId,
            PlayerSelfExtendRequest request,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            ISessionCommandService sessionCommandService,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();

            var session = await dbContext.Sessions.AsNoTracking().SingleOrDefaultAsync(
                s => s.SessionId == sessionId, cancellationToken);

            // Ownership-scoped: a session the caller does not own is indistinguishable from a missing one.
            if (session is null ||
                session.PlayerAccountId != player.PlayerAccountId ||
                session.State != SessionStateNames.Active)
            {
                return Results.NotFound();
            }

            if (!Guid.TryParse(session.TariffRuleVersionId, out var tariffVersionId))
                return Results.BadRequest(new { error = "invalid_tariff" });

            var version = await dbContext.TariffVersions.AsNoTracking().SingleOrDefaultAsync(
                v => v.OrganizationId == session.OrganizationId &&
                     v.BranchId == session.BranchId &&
                     v.TariffVersionId == tariffVersionId, cancellationToken);
            if (version is null) return Results.BadRequest(new { error = "invalid_tariff" });

            var pricing = new TariffPricing(
                version.PricePerMinuteMinorUnits, version.MinimumBillableMinutes,
                version.RoundingIncrementMinutes, version.CurrencyCode);
            var charge = TariffBilling.ComputeForMinutes(request.AdditionalMinutes, pricing);
            if (charge is null) return Results.BadRequest(new { error = "invalid_duration" });

            var wallet = await LedgerBalanceProjector.GetWalletSummaryAsync(
                dbContext, player.PlayerAccountId, cancellationToken);
            if ((wallet?.WalletBalance.MinorUnits ?? 0) < charge.AmountMinorUnits)
                return Results.Conflict(new { error = "insufficient_balance" });

            var extendRequest = new ExtendSessionRequest(
                AdditionalMinutes: request.AdditionalMinutes,
                TariffRuleVersionId: session.TariffRuleVersionId,
                IdempotencyKey: request.IdempotencyKey,
                PlayerAccountId: player.PlayerAccountId,
                BillingMode: BillingModeNames.PrepaidWallet,
                TariffVersionId: tariffVersionId);

            var result = await sessionCommandService.ExtendSessionAsync(
                sessionId, Guid.Empty, extendRequest, cancellationToken);

            if (result.Conflict) return Results.Conflict(new { error = result.Error });
            if (result.NotFound) return Results.NotFound();
            if (!result.Succeeded) return Results.BadRequest(new { error = result.Error });
            return Results.Ok(result.Response);
        }).RequireRateLimiting("player-me");

    }

    /// <summary>
    /// Профиль игрока вместе с его филиалом. Имя филиала может не найтись (аккаунт заведён до того,
    /// как филиал появился, или филиал удалён) — тогда возвращается только идентификатор: он и есть
    /// то, ради чего филиал отдаётся клиенту, а имя лишь подпись на экране.
    /// </summary>
    /// <summary>
    /// Открывает человеку счёт в выбранном клубе, если его там ещё нет, и делает этот счёт
    /// текущим для остатка запроса. Ничего не делает, когда счёт уже есть, — то есть в
    /// подавляющем большинстве запросов.
    ///
    /// Возвращает готовый отказ, если счёт открыть невозможно (клуб не найден, филиал не назван
    /// у клуба с несколькими филиалами), и null, когда путь свободен.
    /// </summary>
    private static async Task<IResult?> OpenClubAccountIfNeededAsync(
        IPlayerContextAccessor playerContextAccessor,
        IPlatformPersonContextAccessor personContextAccessor,
        IPlayerClubMembershipService clubMembership,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        if (playerContextAccessor.Current is not null)
        {
            return null;
        }

        var person = personContextAccessor.Current;
        if (person?.SelectedOrganizationId is not { } organizationId)
        {
            // Ни счёта, ни выбранного клуба — отвечать будет сам эндпоинт, как и раньше.
            return null;
        }

        var membership = await clubMembership.EnsureAsync(
            person.PlatformPersonId, organizationId, branchId, cancellationToken);
        if (!membership.Succeeded)
        {
            return Results.Conflict(new { error = membership.Error });
        }

        playerContextAccessor.Current = new PlayerContext(
            membership.Account!.PlayerAccountId,
            organizationId,
            person.PhoneVerified,
            person.PlatformPersonId);
        return null;
    }

    private static async Task<PlayerProfileDto> ToProfileDtoAsync(
        PlatformDbContext dbContext,
        PlayerAccountEntity account,
        bool phoneVerified,
        CancellationToken cancellationToken)
    {
        var homeBranchId = account.HomeBranchId == Guid.Empty ? (Guid?)null : account.HomeBranchId;
        var branchName = homeBranchId is null
            ? null
            : await dbContext.Branches.AsNoTracking()
                .Where(branch => branch.BranchId == homeBranchId.Value)
                .Select(branch => branch.Name)
                .FirstOrDefaultAsync(cancellationToken);

        return new PlayerProfileDto(
            account.PlayerAccountId,
            account.DisplayName,
            account.PhoneNumber,
            phoneVerified,
            account.PreferredLocale,
            account.MarketingOptIn,
            homeBranchId,
            branchName);
    }

    private static async Task<int?> ResolveEskhataMerchantIdAsync(
        PlatformDbContext db, Guid organizationId, CancellationToken cancellationToken)
    {
        var config = await db.EskhataMerchantConfigs.AsNoTracking()
            .SingleOrDefaultAsync(c => c.OrganizationId == organizationId && c.BranchId == null, cancellationToken);
        return config?.MerchantId;
    }
}
