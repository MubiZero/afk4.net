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

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// Shared helpers used across the minimal-API endpoint registrations in Program.cs.
/// Brought into scope there via <c>using static</c>, so endpoint bodies call them unqualified.
/// </summary>
internal static partial class EndpointHelpers
{
    public static IResult ToHttpResult<TResponse>(BillingCommandServiceResult<TResponse> result)
    {
        if (result.Conflict)
        {
            return Results.Conflict(new { Error = result.Error });
        }

        if (result.NotFound)
        {
            return Results.NotFound(new { Error = result.Error });
        }

        if (!result.Succeeded)
        {
            return Results.BadRequest(new { Error = result.Error });
        }

        return Results.Ok(result.Response);
    }

    public static IResult ToUpdateHttpResult<TResponse>(UpdateServiceResult<TResponse> result)
    {
        if (result.Conflict)
        {
            return Results.Conflict(new { Error = result.Error });
        }

        if (result.NotFound)
        {
            return Results.NotFound(new { Error = result.Error });
        }

        if (!result.Succeeded)
        {
            return Results.BadRequest(new { Error = result.Error });
        }

        return Results.Ok(result.Response);
    }

    public static IResult ToReservationHttpResult<TResponse>(ReservationServiceResult<TResponse> result)
    {
        if (result.Conflict)
        {
            return Results.Conflict(new
            {
                Error = result.Error,
                result.Code,
                result.CurrentVersion
            });
        }

        if (result.NotFound)
        {
            return Results.NotFound(new { Error = result.Error });
        }

        if (!result.Succeeded)
        {
            return Results.BadRequest(new { Error = result.Error });
        }

        return Results.Ok(result.Response);
    }

    public static IResult ToInstallHttpResult<TResponse>(InstallOperationResult<TResponse> result)
    {
        return result.Status switch
        {
            InstallOperationStatus.Succeeded => Results.Ok(result.Value),
            InstallOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
            InstallOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
            _ => Results.BadRequest(new { Error = result.Error })
        };
    }

    public static string GetSourceIp(HttpContext httpContext)
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress;
        if (ShouldTrustForwardedFor(remoteIp))
        {
            var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();
            var firstForwardedFor = forwardedFor
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstForwardedFor))
            {
                return firstForwardedFor;
            }
        }

        return remoteIp?.ToString() ?? "unknown";
    }

    public static bool ShouldTrustForwardedFor(IPAddress? remoteIp)
    {
        if (remoteIp is null || IPAddress.IsLoopback(remoteIp))
        {
            return true;
        }

        if (remoteIp.IsIPv4MappedToIPv6)
        {
            remoteIp = remoteIp.MapToIPv4();
        }

        if (remoteIp.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = remoteIp.GetAddressBytes();
            return bytes[0] == 10 ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168);
        }

        if (remoteIp.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = remoteIp.GetAddressBytes();
            return remoteIp.IsIPv6LinkLocal || (bytes[0] & 0xfe) == 0xfc;
        }

        return false;
    }
}
