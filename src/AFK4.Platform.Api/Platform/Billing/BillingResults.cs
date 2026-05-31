using Microsoft.AspNetCore.Http;

namespace AFK4.Platform.Api.Platform.Billing;

public static class BillingResults
{
    public static IResult From<T>(BillingOperationResult<T> result) where T : class =>
        result.Status switch
        {
            BillingOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
            BillingOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
            BillingOperationStatus.BadRequest => Results.BadRequest(new { Error = result.Error }),
            _ => Results.BadRequest(new { Error = result.Error ?? "Unknown billing error." })
        };
}
