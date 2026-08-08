using AFK4.Shared.Contracts.Platform.Features;

namespace AFK4.Platform.Api.Platform.Entitlements;

/// <summary>
/// Общая точка отказа для игровых эндпоинтов, стоящих за фичей. Стоит после аутентификации и
/// до любой записи в базу — иначе выключенная фича продолжала бы порождать побочные эффекты.
/// </summary>
public static class FeatureGate
{
    /// <summary>
    /// Возвращает готовый отказ, если фича выключена, и <c>null</c>, если можно.
    /// Тело несёт код и ключ фичи — фразу для человека собирает клиент.
    /// </summary>
    public static async Task<IResult?> RequireAsync(
        this IOrganizationEntitlements entitlements,
        Guid organizationId,
        string featureKey,
        CancellationToken cancellationToken)
    {
        if (await entitlements.IsEnabledAsync(organizationId, featureKey, cancellationToken))
        {
            return null;
        }

        return Results.Json(
            new { Error = "FeatureDisabled", Code = PlatformFeatureNames.DisabledCode, FeatureKey = featureKey },
            statusCode: StatusCodes.Status403Forbidden);
    }
}
