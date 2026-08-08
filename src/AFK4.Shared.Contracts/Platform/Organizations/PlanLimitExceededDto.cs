namespace AFK4.Shared.Contracts.Platform.Organizations;

/// <summary>
/// Тело отказа по лимиту тарифа. <paramref name="Current"/> и <paramref name="Limit"/> едят
/// клиенту, чтобы отказ читался как «филиалов 2 из 2», а не как «нельзя».
/// </summary>
public sealed record PlanLimitExceededDto(
    string Code,
    string LimitName,
    int Limit,
    int Current,
    string PlanCode);
