using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.OrganizationAdmin.App.Offline;

/// <summary>
/// Офлайн у стойки со стороны ПК администратора: подписать аренду ключом этого ПК и передать её ПК в
/// зале по сети клуба. Подпись делает хост, а не страница: приватный ключ в браузер не попадает.
/// </summary>
public sealed class OfflineDesk(Func<OfflineDeskKey> key, HttpClient http)
{
    public (string KeyId, string PublicKeyPem) PublicKey()
    {
        var current = key();
        return (current.KeyId, current.PublicKeyPem);
    }

    public async Task<OfflineSeatResponse> SendLeaseAsync(string address, SessionLeaseDto unsigned, CancellationToken cancellationToken)
    {
        // Аренды уходят только на ПК в сети клуба: страница не должна суметь отправить подписанное куда угодно.
        if (!IPAddress.TryParse(address, out var ip) || !IsPrivateIPv4(ip))
        {
            throw new InvalidOperationException("The PC address is not a private club network address.");
        }

        var signer = key();
        var lease = unsigned with { SignatureAlgorithm = OfflineLeaseSigning.Algorithm(signer.KeyId), Signature = string.Empty };
        var payload = SessionLeasePayloadCanonicalizer.CreatePayload(lease);
        var signature = signer.Key.SignData(Encoding.UTF8.GetBytes(payload), System.Security.Cryptography.HashAlgorithmName.SHA256);
        lease = lease with { Signature = Convert.ToBase64String(signature) };

        var url = new Uri($"http://{ip}:{OfflineLeaseSigning.AgentLanPort}{OfflineLeaseSigning.AgentLeasePath}");
        using var response = await http.PostAsJsonAsync(url, lease, cancellationToken);
        return await response.Content.ReadFromJsonAsync<OfflineSeatResponse>(cancellationToken)
            ?? new OfflineSeatResponse(false, $"The PC answered {(int)response.StatusCode} without a result.", true, null, null);
    }

    internal static bool IsPrivateIPv4(IPAddress ip)
    {
        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = ip.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }
}
