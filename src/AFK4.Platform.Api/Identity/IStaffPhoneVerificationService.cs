namespace AFK4.Platform.Api.Identity;

public enum PhoneVerificationStartStatus
{
    Sent,
    InvalidPhone,
    CooldownActive,
    RateLimited,
    SmsFailed,
}

public sealed record PhoneVerificationStartResult(
    PhoneVerificationStartStatus Status,
    int ExpiresInSeconds,
    int ResendAfterSeconds,
    string? Error);

public enum PhoneConfirmStatus
{
    Confirmed,
    NoActiveCode,
    Expired,
    TooManyAttempts,
    InvalidCode,
    PhoneAlreadyInUse,
}

public sealed record PhoneConfirmResult(PhoneConfirmStatus Status, int RemainingAttempts, string? VerifiedPhone);

public interface IStaffPhoneVerificationService
{
    Task<PhoneVerificationStartResult> StartAsync(
        Guid staffUserId, Guid organizationId, string rawPhone, CancellationToken cancellationToken);

    Task<PhoneConfirmResult> ConfirmAsync(
        Guid staffUserId, string code, CancellationToken cancellationToken);
}
