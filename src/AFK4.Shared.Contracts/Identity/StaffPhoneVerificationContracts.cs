namespace AFK4.Shared.Contracts.Identity;

public sealed record StaffPhoneStartVerificationRequest(string Phone);

public sealed record StaffPhoneVerificationStartedResponse(int ExpiresInSeconds, int ResendAfterSeconds);

public sealed record StaffPhoneConfirmRequest(string Code);

public sealed record StaffPhoneConfirmedResponse(string Phone);
