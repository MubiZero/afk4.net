namespace AFK4.Shared.Contracts.Identity;

/// <summary>Self-service password-reset request. Resolved by username or contact email.</summary>
public sealed record StaffForgotPasswordRequest(string UserNameOrEmail);
