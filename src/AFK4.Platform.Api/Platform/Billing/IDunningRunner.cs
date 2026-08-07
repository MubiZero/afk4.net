namespace AFK4.Platform.Api.Platform.Billing;

public interface IDunningRunner
{
    /// <summary>Flips due invoices to overdue, sends the pre-due reminder and the overdue ladder,
    /// and returns the number of notifications sent.</summary>
    Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
