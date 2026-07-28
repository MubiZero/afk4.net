namespace AFK4.Shared.Contracts.Platform.Health;

public sealed record OrganizationHealthErrorDto(
    DateTimeOffset CreatedAtUtc,
    string Source,
    string Action,
    string Outcome,
    string? Message);
