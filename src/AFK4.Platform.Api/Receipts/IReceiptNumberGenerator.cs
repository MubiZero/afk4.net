namespace AFK4.Platform.Api.Receipts;

public interface IReceiptNumberGenerator
{
    Task<string> GenerateAsync(
        Guid organizationId,
        Guid branchId,
        string receiptType,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
