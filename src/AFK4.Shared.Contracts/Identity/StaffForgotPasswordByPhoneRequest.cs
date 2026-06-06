namespace AFK4.Shared.Contracts.Identity;

/// <summary>Requests an SMS password-reset code to a staff account's verified phone.</summary>
public sealed record StaffForgotPasswordByPhoneRequest(string PhoneNumber);
