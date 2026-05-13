namespace AFK4.Agent.Service;

public sealed record SessionLeaseValidationResult(bool IsValid, string? Error)
{
    public static SessionLeaseValidationResult Valid() => new(true, null);

    public static SessionLeaseValidationResult Invalid(string error) => new(false, error);
}
