namespace AFK4.Platform.Api.Data;

public sealed class SessionLeaseEntity
{
    public Guid SessionLeaseId { get; set; }

    public Guid SessionId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid SeatId { get; set; }

    public Guid DeviceId { get; set; }

    public string State { get; set; } = string.Empty;

    public int Sequence { get; set; }

    public DateTimeOffset IssuedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public string SignatureAlgorithm { get; set; } = "ECDSA-P256-SHA256";

    public string Signature { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public DateTimeOffset? RevokedAtUtc { get; set; }
}
