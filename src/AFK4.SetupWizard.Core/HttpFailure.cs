using System.Net;

namespace AFK4.SetupWizard.Core;

/// <summary>
/// Ошибка HTTP с кодом ответа — по нему мастер решает, повторять ли запрос. В .NET Framework 4.8 у
/// <see cref="HttpRequestException"/> нет StatusCode: код лежит в Data, а читает его NetFrameworkCompat.
/// </summary>
internal static class HttpFailure
{
    public static HttpRequestException For(HttpStatusCode status, string message)
    {
#if NETFRAMEWORK
        var exception = new HttpRequestException(message);
        exception.Data[NetFrameworkCompat.StatusCodeKey] = status;
        return exception;
#else
        return new HttpRequestException(message, null, status);
#endif
    }
}
