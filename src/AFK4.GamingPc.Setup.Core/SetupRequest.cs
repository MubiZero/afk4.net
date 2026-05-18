namespace AFK4.GamingPc.Setup.Core;

public sealed record SetupRequest(
    string UserName,
    string Password,
    string MachineName,
    string LeaseSigningPublicKeyPem);
