namespace AFK4.Platform.Api.Payments.DcGate;

// Builds a dcgate client bound to a specific project apiKey, over the shared
// platform base-URL HttpClient pool.
public interface IDcGateClientFactory
{
    IDcGateClient CreateForApiKey(string apiKey);
}
