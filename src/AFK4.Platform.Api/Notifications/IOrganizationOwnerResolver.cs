namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Resolves the notification recipient for an organization's owner (the staff user holding the
/// owner role, with an email on file) plus the organization name. Shared by the billing and
/// operational notifiers. Returns null when there is no owner with an email — the caller no-ops.
/// </summary>
public interface IOrganizationOwnerResolver
{
    Task<OwnerRecipient?> ResolveAsync(Guid organizationId, CancellationToken cancellationToken);
}

public sealed record OwnerRecipient(Guid StaffUserId, string DisplayName, string Email, string OrganizationName);
