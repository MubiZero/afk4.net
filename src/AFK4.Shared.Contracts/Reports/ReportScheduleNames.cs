namespace AFK4.Shared.Contracts.Reports;

/// <summary>The report kinds a schedule can deliver — one per existing report export endpoint.</summary>
public static class ScheduledReportTypeNames
{
    public const string Shifts = "shifts";

    public const string Sales = "sales";

    public const string GameplayTime = "gameplay_time";

    public const string CashOperations = "cash_operations";

    public const string OperatorActions = "operator_actions";

    public static readonly IReadOnlyList<string> All =
        [Shifts, Sales, GameplayTime, CashOperations, OperatorActions];
}

/// <summary>How often a report schedule fires and the size of the window each run covers.</summary>
public static class ReportScheduleFrequencyNames
{
    public const string Daily = "daily";

    public const string Weekly = "weekly";

    public const string Monthly = "monthly";

    public static readonly IReadOnlyList<string> All = [Daily, Weekly, Monthly];
}
