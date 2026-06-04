namespace AFK4.Platform.Api.Security;

// Encrypts/decrypts small secret strings (dcgate apiKey + webhook secret) for storage at rest.
public interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}
