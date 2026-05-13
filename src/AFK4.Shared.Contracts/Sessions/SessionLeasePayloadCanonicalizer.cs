namespace AFK4.Shared.Contracts.Sessions;

public static class SessionLeasePayloadCanonicalizer
{
    public static string CreatePayload(SessionLeaseDto leaseWithoutSignature)
    {
        return string.Join(
            "|",
            leaseWithoutSignature.SessionId.ToString("D"),
            leaseWithoutSignature.OrganizationId.ToString("D"),
            leaseWithoutSignature.BranchId.ToString("D"),
            leaseWithoutSignature.SeatId.ToString("D"),
            leaseWithoutSignature.DeviceId.ToString("D"),
            leaseWithoutSignature.State,
            leaseWithoutSignature.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            leaseWithoutSignature.IssuedAtUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            leaseWithoutSignature.ExpiresAtUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            leaseWithoutSignature.SignatureAlgorithm);
    }
}
