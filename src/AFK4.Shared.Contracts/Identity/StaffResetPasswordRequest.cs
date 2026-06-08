namespace AFK4.Shared.Contracts.Identity;

/// <summary>Completes a self-service password reset using the 6-digit code emailed to the account.</summary>
public sealed record StaffResetPasswordRequest(string UserNameOrEmail, string Code, string NewPassword);
