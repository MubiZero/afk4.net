namespace AFK4.Operator.App.Configuration;

public sealed class OperatorAppOptions
{
    public Uri PlatformBaseUrl { get; init; } = new("http://localhost:5074");

    public Guid? OrganizationId { get; init; }

    public Guid? BranchId { get; init; }
}
