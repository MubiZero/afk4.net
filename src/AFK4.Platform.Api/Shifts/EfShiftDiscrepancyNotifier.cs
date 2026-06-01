using System.Globalization;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Shifts;

public sealed class EfShiftDiscrepancyNotifier(
    IOrganizationOwnerResolver ownerResolver,
    INotificationService notifications,
    PlatformDbContext dbContext,
    IOptions<NotificationOptions> options) : IShiftDiscrepancyNotifier
{
    private readonly NotificationOptions options = options.Value;

    public async Task NotifyIfOverToleranceAsync(ShiftEntity shift, CancellationToken cancellationToken)
    {
        if (Math.Abs(shift.DifferenceMinorUnits) <= options.ShiftDiscrepancyToleranceMinorUnits)
        {
            return;
        }

        var recipient = await ownerResolver.ResolveAsync(shift.OrganizationId, cancellationToken);
        if (recipient is null)
        {
            return;
        }

        var branchName = await dbContext.Branches
            .Where(branch => branch.BranchId == shift.BranchId)
            .Select(branch => branch.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var request = new NotificationRequest(
            TemplateKey: NotificationTemplateKeys.ShiftDiscrepancy,
            Category: NotificationCategory.Operational,
            Recipient: new NotificationRecipient(
                Locale: string.Empty,
                EmailAddress: recipient.Email,
                StaffUserId: recipient.StaffUserId),
            Tokens: new Dictionary<string, string>
            {
                ["displayName"] = recipient.DisplayName,
                ["organizationName"] = recipient.OrganizationName,
                ["branchName"] = branchName,
                ["difference"] = Money(shift.DifferenceMinorUnits),
                ["countedCash"] = Money(shift.CountedCashMinorUnits),
                ["expectedCash"] = Money(shift.ExpectedCashMinorUnits),
                ["currency"] = shift.CurrencyCode,
                ["closedDate"] = (shift.ClosedAtUtc ?? shift.OpenedAtUtc).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            },
            IdempotencyKey: $"shift-discrepancy:{shift.ShiftId:N}",
            OrganizationId: shift.OrganizationId,
            BranchId: shift.BranchId);

        await notifications.SendAsync(request, cancellationToken);
    }

    private static string Money(long minorUnits) => (minorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture);
}
