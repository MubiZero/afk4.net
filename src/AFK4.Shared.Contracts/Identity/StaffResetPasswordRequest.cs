namespace AFK4.Shared.Contracts.Identity;

/// <summary>Completes a self-service password reset using the emailed token.</summary>
public sealed record StaffResetPasswordRequest(string Token, string NewPassword);
