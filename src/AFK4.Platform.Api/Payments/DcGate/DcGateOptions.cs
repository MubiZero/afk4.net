namespace AFK4.Platform.Api.Payments.DcGate;

public sealed class DcGateOptions
{
    public const string SectionName = "DcGate";

    // dcgate base URL, e.g. https://dcgate.mubi.dev
    public string BaseUrl { get; set; } = string.Empty;
}
