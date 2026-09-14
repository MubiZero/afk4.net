namespace AFK4.Shared.Contracts.Platform.Support;

public sealed record CreatePlatformSupportAccessGrantRequest(
    Guid OrganizationId,
    string Reason,
    int LifetimeMinutes);

public sealed record PlatformSupportAccessGrantDto(
    Guid GrantId,
    Guid OrganizationId,
    string Reason,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc);

// Живой доступ в клуб, каким его видит тот, кто решает — оставить или оборвать. Отдельно от
// PlatformSupportAccessGrantDto: чтобы понять, кого обрывать, нужно имя выдавшего (Guid ничего не
// говорит), а чтобы понять, стоит ли, — вошёл ли он вообще. Невостребованный билет означает, что
// внутрь никто не заходил.
public sealed record PlatformSupportAccessGrantListItem(
    Guid GrantId,
    Guid OrganizationId,
    string Reason,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    Guid PlatformAdminUserId,
    string PlatformAdminDisplayName,
    DateTimeOffset? EnteredAtUtc);

public sealed record PlatformSupportAccessGrantIssue(
    PlatformSupportAccessGrantDto Grant,
    string Ticket,
    string AdminUrl);

public sealed record PlatformSupportSessionDto(
    string SessionToken,
    Guid OrganizationId,
    string OrganizationName,
    string Reason,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<string> WritableAreas,
    IReadOnlyList<PlatformSupportSessionBranchDto> Branches);

// The organization-admin shell is built around branches (it cannot render without at least one) —
// support needs to see the same list a club's own staff would, not just the organization name.
public sealed record PlatformSupportSessionBranchDto(Guid BranchId, string Name);

public sealed record RedeemSupportAccessTicketRequest(string Ticket);
