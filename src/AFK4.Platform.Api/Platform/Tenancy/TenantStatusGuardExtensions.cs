namespace AFK4.Platform.Api.Platform.Tenancy;

public static class TenantStatusGuardExtensions
{
    public static async Task<IResult?> RequireActiveAsync(
        this ITenantStatusGuard guard,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var snapshot = await guard.GetAsync(organizationId, cancellationToken);
        if (snapshot is null || snapshot.IsActive)
        {
            return null;
        }

        return Results.Json(
            new
            {
                Error = "TenantSuspended",
                Status = snapshot.Status,
                Reason = snapshot.Reason
            },
            statusCode: StatusCodes.Status403Forbidden);
    }
}
