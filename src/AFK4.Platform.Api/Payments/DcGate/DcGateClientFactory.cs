namespace AFK4.Platform.Api.Payments.DcGate;

public sealed class DcGateClientFactory(IHttpClientFactory httpClientFactory) : IDcGateClientFactory
{
    // Named client configured in Program.cs with the platform dcgate BaseAddress.
    public const string HttpClientName = "dcgate";

    public IDcGateClient CreateForApiKey(string apiKey) =>
        new DcGateClient(httpClientFactory.CreateClient(HttpClientName), apiKey);
}
