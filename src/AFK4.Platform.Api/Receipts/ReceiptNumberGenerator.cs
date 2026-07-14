using System.Globalization;
using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Receipts;

public sealed class ReceiptNumberGenerator(PlatformDbContext dbContext) : IReceiptNumberGenerator
{
    public async Task<string> GenerateAsync(
        Guid organizationId,
        Guid branchId,
        string receiptType,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalizedReceiptType = receiptType.Trim().ToLowerInvariant();
        var receiptPrefix = normalizedReceiptType == "refund" ? "REF" : "POS";
        var dailyPrefix = $"{receiptPrefix}-{now.UtcDateTime:yyyyMMdd}-";
        var existingNumbers = await dbContext.Receipts
            .AsNoTracking()
            .Where(receipt =>
                receipt.OrganizationId == organizationId &&
                receipt.BranchId == branchId &&
                receipt.ReceiptNumber.StartsWith(dailyPrefix))
            .Select(receipt => receipt.ReceiptNumber)
            .ToListAsync(cancellationToken);

        var maxSequence = 0;
        foreach (var receiptNumber in existingNumbers)
        {
            if (receiptNumber.Length <= dailyPrefix.Length)
            {
                continue;
            }

            var suffix = receiptNumber[dailyPrefix.Length..];
            if (int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) &&
                sequence > maxSequence)
            {
                maxSequence = sequence;
            }
        }

        return $"{dailyPrefix}{maxSequence + 1:0000}";
    }
}
