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

namespace AFK4.Platform.Api.Endpoints;

internal static partial class EndpointHelpers
{
    public static string? ValidateCreateReportScheduleRequest(CreateReportScheduleRequest request)
    {
        if (request.OrganizationId == Guid.Empty)
        {
            return "OrganizationId is required.";
        }

        if (!ScheduledReportTypeNames.All.Contains(request.ReportType))
        {
            return $"ReportType must be one of: {string.Join(", ", ScheduledReportTypeNames.All)}.";
        }

        if (!ReportScheduleFrequencyNames.All.Contains(request.Frequency))
        {
            return $"Frequency must be one of: {string.Join(", ", ReportScheduleFrequencyNames.All)}.";
        }

        return null;
    }

    public static string? ValidateCreateStaffInviteRequest(CreateStaffInviteRequest request)
    {
        if (request.OrganizationId == Guid.Empty)
        {
            return "OrganizationId is required.";
        }

        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            return "UserName is required.";
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return "DisplayName is required.";
        }

        // Телефон обязателен, почта — нет: приглашение едет SMS, а почты у администратора зала
        // может не быть вовсе. Названная почта обязана быть похожей на почту.
        if (PhoneNumberNormalizer.Normalize(request.PhoneNumber) is null)
        {
            return "A valid PhoneNumber is required to send the invite.";
        }

        if (request.Email is not null
            && (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@', StringComparison.Ordinal)))
        {
            return "Email must be a valid address when provided.";
        }

        return ValidateOrganizationRoleNames(request.RoleNames);
    }

    public static string? ValidateUpdateStaffUserProfileRequest(UpdateStaffUserProfileRequest request)
    {
        if (request.OrganizationId == Guid.Empty)
        {
            return "OrganizationId is required.";
        }

        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            return "UserName is required.";
        }

        if (request.UserName.Trim().Length > 256)
        {
            return "UserName must contain 256 characters or fewer.";
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return "DisplayName is required.";
        }

        return request.DisplayName.Trim().Length <= 160
            ? null
            : "DisplayName must contain 160 characters or fewer.";
    }

    public static string? ValidateStaffPassword(string password)
    {
        return string.IsNullOrWhiteSpace(password) || password.Length < 8
            ? "Password must contain at least 8 characters."
            : null;
    }

    public static string? ValidateOrganizationRoleNames(IReadOnlyList<string> roleNames)
    {
        if (roleNames.Count == 0)
        {
            return "At least one role is required.";
        }

        return roleNames.All(IsAssignableBranchStaffRole)
            ? null
            : "Unsupported branch staff role name.";
    }

    public static bool IsAssignableBranchStaffRole(string roleName)
    {
        return roleName.Trim() is
            OrganizationRoleNames.BranchManager or
            OrganizationRoleNames.ShiftSupervisor or
            OrganizationRoleNames.Operator or
            OrganizationRoleNames.Technician or
            OrganizationRoleNames.Accountant;
    }

    public static string? ValidateUpdateBranchProfileRequest(UpdateBranchProfileRequest request)
    {
        if (request.OrganizationId == Guid.Empty)
        {
            return "OrganizationId is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Name is required.";
        }

        if (request.Name.Trim().Length > 160)
        {
            return "Name must contain 160 characters or fewer.";
        }

        if (string.IsNullOrWhiteSpace(request.City))
        {
            return "City is required.";
        }

        if (request.City.Trim().Length > 120)
        {
            return "City must contain 120 characters or fewer.";
        }

        if ((request.Description?.Length ?? 0) > 500) return "Description must contain 500 characters or fewer.";
        if ((request.Address?.Length ?? 0) > 300) return "Address must contain 300 characters or fewer.";
        if ((request.Phone?.Length ?? 0) > 40) return "Phone must contain 40 characters or fewer.";
        if ((request.Telegram?.Length ?? 0) > 120) return "Telegram must contain 120 characters or fewer.";
        if ((request.Website?.Length ?? 0) > 300) return "Website must contain 300 characters or fewer.";
        if ((request.CoverImageUrl?.Length ?? 0) > 600) return "CoverImageUrl must contain 600 characters or fewer.";

        var photos = AFK4.Platform.Api.Branches.BranchPhotos.Validate(request.Photos);
        if (photos is not null) return photos;

        // Координаты приходят из карты, но прийти могут и из формы: точка за пределами глобуса
        // поставит клуб в никуда, и на карте это будет выглядеть поломкой карты, а не опечаткой.
        if (request.Latitude is < -90 or > 90) return "Latitude must be between -90 and 90.";
        if (request.Longitude is < -180 or > 180) return "Longitude must be between -180 and 180.";
        if (request.Latitude is null != (request.Longitude is null))
        {
            return "Latitude and Longitude must be provided together.";
        }

        if (string.IsNullOrWhiteSpace(request.TimeZone) || request.TimeZone.Length > 64)
        {
            return "TimeZone is required and must contain 64 characters or fewer.";
        }

        if (string.IsNullOrWhiteSpace(request.Locale) || request.Locale.Length > 8)
        {
            return "Locale is required and must contain 8 characters or fewer.";
        }

        return AFK4.Platform.Api.Branches.BranchWorkingHours.Validate(request.WorkingHours);
    }
}
