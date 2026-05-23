namespace AFK4.Shared.Contracts.Platform.Auth;

public sealed record PlatformAdminSignInRequest(
    string UserName,
    string Password);
