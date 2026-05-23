namespace AFK4.Shared.Contracts.Platform.Tenants;

public sealed record UpdateTenantPlanRequest(
    string PlanCode,
    string SubscriptionStatus);
