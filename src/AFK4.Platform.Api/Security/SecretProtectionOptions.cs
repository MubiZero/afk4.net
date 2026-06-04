namespace AFK4.Platform.Api.Security;

public sealed class SecretProtectionOptions
{
    public const string SectionName = "Secrets";

    // Base64 of a 32-byte (256-bit) key. Supplied via environment/secret store, never committed.
    public string EncryptionKeyBase64 { get; set; } = string.Empty;
}
