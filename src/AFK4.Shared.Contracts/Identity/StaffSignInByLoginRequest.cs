namespace AFK4.Shared.Contracts.Identity;

public sealed record StaffSignInByLoginRequest(
    string Login,
    string Password);
