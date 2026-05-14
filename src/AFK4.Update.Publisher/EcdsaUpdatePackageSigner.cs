using System.Security.Cryptography;

namespace AFK4.Update.Publisher;

public sealed class EcdsaUpdatePackageSigner
{
    public async Task<string> SignAsync(
        string privateKeyPemPath,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(privateKeyPemPath))
        {
            throw new FileNotFoundException("Update signing private key was not found.", privateKeyPemPath);
        }

        var pem = await File.ReadAllTextAsync(privateKeyPemPath, cancellationToken);
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(pem);
        var signature = ecdsa.SignData(
            payload,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return Convert.ToBase64String(signature);
    }
}
