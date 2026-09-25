namespace AFK4.Platform.Api.Data;

public sealed class SubscriptionPlanEntity
{
    public string PlanCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long PriceMinorUnits { get; set; }
    public string CurrencyCode { get; set; } = "TJS";
    public string BillingInterval { get; set; } = "monthly";
    public int? MaxBranches { get; set; }
    public int? MaxDevicesPerBranch { get; set; }
    public int? MaxConcurrentSessions { get; set; }
    public int? MaxStaffUsersPerBranch { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    /// <summary>Цена каждого подтверждённого ПК сверх <see cref="IncludedDevices"/> за период.</summary>
    public long PricePerDeviceMinorUnits { get; set; }
    public int IncludedDevices { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
