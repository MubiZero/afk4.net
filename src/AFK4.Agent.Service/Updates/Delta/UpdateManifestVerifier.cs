using System.Security.Cryptography;
using System.Text.Json;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.Agent.Service.Updates.Delta;

public sealed record UpdateManifestVerification(bool IsValid, string Message, UpdateManifestDocument? Document)
{
    public static UpdateManifestVerification Invalid(string message) => new(false, message, null);

    public static UpdateManifestVerification Valid(UpdateManifestDocument document) => new(true, "Update manifest verified.", document);
}

/// <summary>
/// Проверяет манифест так же строго, как пакет: хеш и размер файла, подпись тем же ключом и только
/// потом содержимое. Содержимое сверяется с инструкцией (компонент, версия, канал) и с хостом
/// пакета: манифест от другого релиза или с чужим адресом тел файлов не принимается.
/// </summary>
public static class UpdateManifestVerifier
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static UpdateManifestVerification Verify(
        ComponentUpdateInstructionDto instruction,
        UpdateManifestDto manifest,
        byte[] manifestBytes,
        string publicKeyPem)
    {
        if (!string.Equals(manifest.SignatureAlgorithm, UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363, StringComparison.Ordinal))
        {
            return UpdateManifestVerification.Invalid("Unsupported update manifest signature algorithm.");
        }

        if (string.IsNullOrWhiteSpace(publicKeyPem))
        {
            return UpdateManifestVerification.Invalid("Update verification public key is not configured.");
        }

        if (manifest.SizeBytes != manifestBytes.LongLength)
        {
            return UpdateManifestVerification.Invalid("Downloaded update manifest size does not match its metadata.");
        }

        var hash = Convert.ToHexString(SHA256.HashData(manifestBytes)).ToLowerInvariant();
        if (!string.Equals(hash, manifest.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return UpdateManifestVerification.Invalid("Downloaded update manifest SHA-256 hash does not match its metadata.");
        }

        var payload = UpdateManifestSignaturePayload.Create(
            instruction.Component, instruction.Version, instruction.Channel, manifest.Uri, hash, manifest.SizeBytes);
        if (!VerifySignature(publicKeyPem, manifest.Signature, payload))
        {
            return UpdateManifestVerification.Invalid("Update manifest signature does not match its metadata.");
        }

        UpdateManifestDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<UpdateManifestDocument>(manifestBytes, JsonOptions);
        }
        catch (JsonException)
        {
            return UpdateManifestVerification.Invalid("Update manifest is not valid JSON.");
        }

        return document is null ? UpdateManifestVerification.Invalid("Update manifest is empty.") : CheckDocument(instruction, document);
    }

    private static UpdateManifestVerification CheckDocument(ComponentUpdateInstructionDto instruction, UpdateManifestDocument document)
    {
        if (document.Schema != UpdateManifestLayout.CurrentSchema)
        {
            return UpdateManifestVerification.Invalid($"Unsupported update manifest schema {document.Schema}.");
        }

        if (!string.Equals(document.Component, instruction.Component, StringComparison.Ordinal)
            || !string.Equals(document.Version, instruction.Version, StringComparison.Ordinal)
            || !string.Equals(document.Channel, instruction.Channel, StringComparison.Ordinal))
        {
            return UpdateManifestVerification.Invalid("Update manifest belongs to a different component, version or channel.");
        }

        if (!Uri.TryCreate(document.BlobBaseUri, UriKind.Absolute, out var blobBase)
            || blobBase.Scheme != Uri.UriSchemeHttps
            || !Uri.TryCreate(instruction.ArtifactUri, UriKind.Absolute, out var artifact)
            || !string.Equals(blobBase.Host, artifact.Host, StringComparison.OrdinalIgnoreCase))
        {
            return UpdateManifestVerification.Invalid("Update manifest blob address is not HTTPS on the package host.");
        }

        if (document.Files.Count == 0)
        {
            return UpdateManifestVerification.Invalid("Update manifest lists no files.");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in document.Files)
        {
            if (!UpdateRootNames.IsKnown(file.Root))
            {
                return UpdateManifestVerification.Invalid($"Update manifest has an unknown root '{file.Root}'.");
            }

            if (!UpdateRelativePath.TryResolve(Path.GetTempPath(), file.Path, out _))
            {
                return UpdateManifestVerification.Invalid($"Update manifest has an unsafe file path '{file.Path}'.");
            }

            if (file.Sha256.Length != 64 || !file.Sha256.All(Uri.IsHexDigit) || file.SizeBytes < 0 || file.CompressedSizeBytes < 0)
            {
                return UpdateManifestVerification.Invalid($"Update manifest has an invalid entry for '{file.Path}'.");
            }

            if (!seen.Add($"{file.Root}/{file.Path}"))
            {
                return UpdateManifestVerification.Invalid($"Update manifest lists '{file.Path}' twice.");
            }
        }

        return UpdateManifestVerification.Valid(document);
    }

    private static bool VerifySignature(string publicKeyPem, string signature, byte[] payload)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(publicKeyPem);
            return ecdsa.VerifyData(
                payload,
                Convert.FromBase64String(signature),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            return false;
        }
    }
}
