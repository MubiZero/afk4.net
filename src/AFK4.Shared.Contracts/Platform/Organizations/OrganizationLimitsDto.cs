namespace AFK4.Shared.Contracts.Platform.Organizations;

public sealed record OrganizationLimitsDto(
    int? MaxBranches,
    int? MaxDevicesPerBranch,
    int? MaxConcurrentSessions,
    int? MaxStaffUsersPerBranch,
    // Игровых ПК на весь клуб, без деления по залам: бесплатный тариф — «до десяти ПК», сколько бы
    // залов ни было (спека тарифов клуба, §2). Консоли не считаются.
    int? MaxDevices = null);
