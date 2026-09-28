using System.Security.Cryptography;

namespace AFK4.Update.Publisher;

public sealed class EcdsaUpdatePackageSigner
{
    public string SignPem(string privateKeyPem, byte[] payload)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(privateKeyPem);
        var signature = ecdsa.SignData(
            payload,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return Convert.ToBase64String(signature);
    }
}
