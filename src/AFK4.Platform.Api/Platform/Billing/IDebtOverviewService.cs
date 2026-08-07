using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Platform.Billing;

public interface IDebtOverviewService
{
    /// <summary>Clubs that need a money decision, oldest debt first.</summary>
    Task<IReadOnlyList<DebtRowDto>> GetAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
