using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Options;
using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Payments.DcGate;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Dashboard;
using AFK4.Platform.Api.Diagnostics;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Identity.OwnerCodes;
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
using AFK4.Shared.Contracts.Platform.Invites;
using AFK4.Shared.Contracts.Platform.Operator;
using AFK4.Shared.Contracts.Platform.SupportNotes;
using AFK4.Shared.Contracts.Platform.Tenants;
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
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/staff/sign-in", async (
            StaffSignInRequest request,
            IStaffCredentialService credentialService,
            CancellationToken cancellationToken) =>
        {
            var response = await credentialService.SignInAsync(request, cancellationToken);

            return response is null
                ? Results.Unauthorized()
                : Results.Ok(response);
        });

        app.MapPost("/api/auth/staff/sign-in-by-tenant-key", async (
            StaffSignInByTenantKeyRequest request,
            IStaffCredentialService credentialService,
            CancellationToken cancellationToken) =>
        {
            var response = await credentialService.SignInByTenantKeyAsync(request, cancellationToken);

            return response is null
                ? Results.Unauthorized()
                : Results.Ok(response);
        });

        app.MapPost("/api/auth/staff/sign-in-by-login", async (
            StaffSignInByLoginRequest request,
            IStaffCredentialService credentialService,
            CancellationToken cancellationToken) =>
        {
            var resolution = await credentialService.SignInByLoginAsync(request, cancellationToken);

            if (resolution.SignedIn is not null)
            {
                return Results.Ok(resolution.SignedIn);
            }

            return resolution.Clubs.Count > 0
                ? Results.Json(
                    new StaffSignInChooseClubResponse(resolution.Clubs),
                    statusCode: StatusCodes.Status409Conflict)
                : Results.Unauthorized();
        });

        app.MapPost("/api/auth/staff/refresh", async (
            StaffRefreshTokenRequest request,
            IStaffTokenService tokenService,
            CancellationToken cancellationToken) =>
        {
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

        app.MapPost("/api/public/player/refresh", async (
            PlayerRefreshRequest request,
            IPlayerTokenService tokenService,
            CancellationToken cancellationToken) =>
        {
            var response = await tokenService.RefreshAsync(request, cancellationToken);
            return response is null ? Results.Unauthorized() : Results.Ok(response);
        }).RequireRateLimiting("player-public");

        app.MapGet("/api/public/tenant/{tenantKey}/branding", async (
            string tenantKey,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var normalizedKey = SlugValidator.Normalize(tenantKey);
            var org = await dbContext.Organizations
                .AsNoTracking()
                .Where(o => o.Slug == normalizedKey && o.Status == "active")
                .Select(o => new TenantBrandingDto(o.OrganizationId, o.Name, o.LogoUrl, o.AccentColor))
                .FirstOrDefaultAsync(cancellationToken);
            return org is null ? Results.NotFound() : Results.Ok(org);
        }).RequireRateLimiting("player-public");

    }
}
