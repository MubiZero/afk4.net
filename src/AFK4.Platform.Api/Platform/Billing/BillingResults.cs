using Microsoft.AspNetCore.Http;

namespace AFK4.Platform.Api.Platform.Billing;

public static class BillingResults
{
    public static IResult From<T>(BillingOperationResult<T> result) where T : class =>
        result.Status switch
        {
            // Код рядом с фразой, а не вместо неё: фразу читают в журналах, код — панель.
            BillingOperationStatus.NotFound => Results.NotFound(new { Error = result.Error, result.Code }),
            BillingOperationStatus.Conflict => Results.Conflict(new { Error = result.Error, result.Code }),
            BillingOperationStatus.BadRequest => Results.BadRequest(new { Error = result.Error, result.Code }),
            _ => Results.BadRequest(new { Error = result.Error ?? "Unknown billing error.", result.Code })
        };
}
