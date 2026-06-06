namespace AFK4.Shared.Contracts.Identity;

/// <summary>Completes an SMS password reset using the code delivered to the verified phone.</summary>
public sealed record StaffResetPasswordByPhoneRequest(string PhoneNumber, string Code, string NewPassword);
