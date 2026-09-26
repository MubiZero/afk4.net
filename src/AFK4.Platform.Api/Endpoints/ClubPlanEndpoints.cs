using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.AspNetCore.Http;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// Тариф клуба со стороны клуба: посмотреть словами и сделать то, что клуб делает сам, — начать
/// пробный период, перейти на тариф за ПК, взять обещанный платёж (спека тарифов клуба, §6).
/// </summary>
internal static class ClubPlanEndpoints
{
    public static void MapClubPlanEndpoints(this IEndpointRouteBuilder organizations)
    {
        organizations.MapGet("plan", async (
            Guid organizationId, StaffAuthorizationService authorizationService, ClubPlans plans, CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ViewSubscription);
            if (Refusal(authorization, organizationId) is { } refused) return refused;
            var plan = await plans.DescribeAsync(organizationId, ct);
            return plan is null ? Results.NotFound() : Results.Ok(plan);
        });

        organizations.MapPost("plan/trial", (Guid organizationId, StaffAuthorizationService authorizationService, ClubPlans plans, CancellationToken ct) =>
            ActAsync(organizationId, authorizationService, plans, (actor) => plans.StartTrialAsync(organizationId, actor, ct), ct));

        organizations.MapPost("plan/per-pc", (Guid organizationId, StaffAuthorizationService authorizationService, ClubPlans plans, CancellationToken ct) =>
            ActAsync(organizationId, authorizationService, plans, (actor) => plans.SwitchToPerPcAsync(organizationId, actor, ct), ct));

        organizations.MapPost("plan/promised-payment", (Guid organizationId, StaffAuthorizationService authorizationService, ClubPlans plans, CancellationToken ct) =>
            ActAsync(organizationId, authorizationService, plans, (actor) => plans.PromisePaymentAsync(organizationId, actor, ct), ct));

        // Какие ПК работают на бесплатном тарифе, когда их больше предела (§5a).
        organizations.MapGet("plan/devices", async (
            Guid organizationId, StaffAuthorizationService authorizationService, ClubPlans plans, CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ViewSubscription);
            if (Refusal(authorization, organizationId) is { } refused) return refused;
            return Results.Ok(await plans.DevicesAsync(organizationId, ct));
        });

        organizations.MapPut("plan/devices", async (
            Guid organizationId, SetClubPlanDevicesRequest request, StaffAuthorizationService authorizationService, ClubPlans plans,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManageSubscription);
            if (Refusal(authorization, organizationId) is { } refused) return refused;
            var outcome = await plans.KeepDevicesAsync(organizationId, request.DeviceIds ?? [], authorization.StaffContext!.StaffUserId, ct);
            if (outcome is null) return Results.NotFound();
            if (outcome.Length > 0) return Results.Conflict(new { error = outcome });
            return Results.Ok(await plans.DevicesAsync(organizationId, ct));
        });
    }

    private static async Task<IResult> ActAsync(
        Guid organizationId, StaffAuthorizationService authorizationService, ClubPlans plans,
        Func<Guid, Task<string?>> action, CancellationToken ct)
    {
        var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManageSubscription);
        if (Refusal(authorization, organizationId) is { } refused) return refused;

        var outcome = await action(authorization.StaffContext!.StaffUserId);
        if (outcome is null) return Results.NotFound();
        if (outcome.Length > 0) return Results.Conflict(new { error = outcome });
        return Results.Ok(await plans.DescribeAsync(organizationId, ct));
    }

    private static IResult? Refusal(StaffAuthorizationResult authorization, Guid organizationId)
    {
        if (!authorization.IsAuthenticated) return Results.Unauthorized();
        if (!authorization.IsAllowed || organizationId != authorization.StaffContext!.OrganizationId)
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        return null;
    }
}
