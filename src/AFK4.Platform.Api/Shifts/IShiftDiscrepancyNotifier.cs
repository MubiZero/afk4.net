using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Shifts;

/// <summary>
/// Alerts the organization owner when a closed shift's cash variance exceeds the configured
/// tolerance (anti-fraud, Operational). A no-op within tolerance or when the org has no owner
/// with an email on file.
/// </summary>
public interface IShiftDiscrepancyNotifier
{
    Task NotifyIfOverToleranceAsync(ShiftEntity shift, CancellationToken cancellationToken);
}
