namespace AFK4.Platform.Api.Payments.Eskhata;

// Ссылка на банковское приложение из hosted invoice URL: eskhata://pay/<ref>,
// где <ref> — последний сегмент пути (…/invoices/<ref>). null, если распарсить нельзя.
public static class EskhataDeepLink
{
    public static string? FromInvoiceUrl(string? invoiceUrl)
    {
        if (string.IsNullOrWhiteSpace(invoiceUrl)
            || !Uri.TryCreate(invoiceUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var segment = uri.Segments.LastOrDefault()?.Trim('/');
        return string.IsNullOrEmpty(segment) ? null : $"eskhata://pay/{segment}";
    }
}
