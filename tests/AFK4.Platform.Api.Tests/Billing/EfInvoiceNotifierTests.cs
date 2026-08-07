using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Notifications;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class EfInvoiceNotifierTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-01T00:00:00Z");

    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static async Task<Guid> SeedOrgWithOwnerAsync(PlatformDbContext db, string? ownerEmail)
    {
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = orgId, Slug = "club", Name = "Demo Club", Status = "active",
            PlanCode = "starter", SubscriptionStatus = "active", LimitsJson = "{}",
            CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = ownerId, OrganizationId = orgId, UserName = "owner@club.test",
            NormalizedUserName = "OWNER@CLUB.TEST", DisplayName = "Club Owner",
            Email = ownerEmail, PasswordHash = "x", IsActive = true, CreatedAtUtc = Now
        });
        db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(), StaffUserId = ownerId,
            OrganizationId = orgId, BranchId = branchId, RoleName = OrganizationRoleNames.OrganizationOwner
        });
        await db.SaveChangesAsync();
        return orgId;
    }

    private static InvoiceEntity NewInvoice(Guid orgId) => new()
    {
        InvoiceId = Guid.NewGuid(),
        OrganizationId = orgId,
        Number = 42,
        Kind = InvoiceKindNames.Subscription,
        PeriodStartUtc = Now,
        PeriodEndUtc = Now.AddMonths(1),
        IssuedAtUtc = Now,
        DueAtUtc = Now.AddDays(7),
        AmountMinorUnits = 290000,
        CurrencyCode = "TJS",
        Status = InvoiceStatusNames.Issued,
        Description = "Subscription",
        CreatedAtUtc = Now,
        UpdatedAtUtc = Now
    };

    [Fact]
    public async Task NotifyIssuedAsync_OrgHasOwnerEmail_EnqueuesTransactionalInvoiceIssuedToOwner()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgWithOwnerAsync(db, "owner@club.test");
        var recorder = new RecordingNotificationService();
        var notifier = new EfInvoiceNotifier(new EfOrganizationOwnerResolver(db), recorder);
        var invoice = NewInvoice(orgId);

        await notifier.NotifyIssuedAsync(invoice, CancellationToken.None);

        var request = Assert.Single(recorder.Requests);
        Assert.Equal(NotificationTemplateKeys.InvoiceIssued, request.TemplateKey);
        Assert.Equal(NotificationCategory.Transactional, request.Category);
        Assert.Equal("owner@club.test", request.Recipient.EmailAddress);
        Assert.Equal(orgId, request.OrganizationId);
        Assert.Equal($"invoice-issued:{invoice.InvoiceId:N}", request.IdempotencyKey);
        Assert.Equal("Club Owner", request.Tokens["displayName"]);
        Assert.Equal("Demo Club", request.Tokens["organizationName"]);
        Assert.Equal("42", request.Tokens["invoiceNumber"]);
        Assert.Equal("2900.00", request.Tokens["amount"]);
        Assert.Equal("TJS", request.Tokens["currency"]);
        Assert.Equal("2026-05-08", request.Tokens["dueDate"]);
    }

    [Fact]
    public async Task NotifyPaidAsync_OrgHasOwnerEmail_EnqueuesTransactionalInvoicePaidToOwner()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgWithOwnerAsync(db, "owner@club.test");
        var recorder = new RecordingNotificationService();
        var notifier = new EfInvoiceNotifier(new EfOrganizationOwnerResolver(db), recorder);
        var invoice = NewInvoice(orgId);
        invoice.Status = InvoiceStatusNames.Paid;
        invoice.PaidAtUtc = Now.AddDays(3);

        await notifier.NotifyPaidAsync(invoice, CancellationToken.None);

        var request = Assert.Single(recorder.Requests);
        Assert.Equal(NotificationTemplateKeys.InvoicePaid, request.TemplateKey);
        Assert.Equal(NotificationCategory.Transactional, request.Category);
        Assert.Equal("owner@club.test", request.Recipient.EmailAddress);
        Assert.Equal($"invoice-paid:{invoice.InvoiceId:N}", request.IdempotencyKey);
        Assert.Equal("2900.00", request.Tokens["amount"]);
        Assert.Equal("2026-05-04", request.Tokens["paidDate"]);
    }

    [Fact]
    public async Task NotifyOverdueAsync_OrgHasOwnerEmail_EnqueuesTransactionalDunningStage1ToOwner()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgWithOwnerAsync(db, "owner@club.test");
        var recorder = new RecordingNotificationService();
        var notifier = new EfInvoiceNotifier(new EfOrganizationOwnerResolver(db), recorder);
        var invoice = NewInvoice(orgId);
        invoice.Status = InvoiceStatusNames.Overdue;

        await notifier.NotifyOverdueAsync(invoice, stage: 1, Now.AddDays(9), CancellationToken.None);

        var request = Assert.Single(recorder.Requests);
        Assert.Equal(NotificationTemplateKeys.InvoiceOverdue, request.TemplateKey);
        Assert.Equal(NotificationCategory.Transactional, request.Category);
        Assert.Equal("owner@club.test", request.Recipient.EmailAddress);
        Assert.Equal($"invoice-overdue:{invoice.InvoiceId:N}:1", request.IdempotencyKey);
        Assert.Equal("2900.00", request.Tokens["amount"]);
        Assert.Equal("2026-05-08", request.Tokens["dueDate"]);
        Assert.Equal("2", request.Tokens["daysOverdue"]);
    }

    [Fact]
    public async Task NotifyOverdueAsync_StageOneOnDueDate_DaysOverdueFloorsToOne()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgWithOwnerAsync(db, "owner@club.test");
        var recorder = new RecordingNotificationService();
        var notifier = new EfInvoiceNotifier(new EfOrganizationOwnerResolver(db), recorder);
        var invoice = NewInvoice(orgId);
        invoice.Status = InvoiceStatusNames.Overdue;

        // Stage 1 fires at offset 0, i.e. exactly on the due date — daysOverdue must read "1", not
        // "0", or the reminder looks like a system bug instead of a nudge to pay.
        await notifier.NotifyOverdueAsync(invoice, stage: 1, invoice.DueAtUtc, CancellationToken.None);

        var request = Assert.Single(recorder.Requests);
        Assert.Equal("1", request.Tokens["daysOverdue"]);
    }

    [Fact]
    public async Task NotifyOverdueAsync_StageAtOffsetSeven_DaysOverdueStaysExactlySeven()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgWithOwnerAsync(db, "owner@club.test");
        var recorder = new RecordingNotificationService();
        var notifier = new EfInvoiceNotifier(new EfOrganizationOwnerResolver(db), recorder);
        var invoice = NewInvoice(orgId);
        invoice.Status = InvoiceStatusNames.Overdue;

        await notifier.NotifyOverdueAsync(invoice, stage: 3, invoice.DueAtUtc.AddDays(7), CancellationToken.None);

        var request = Assert.Single(recorder.Requests);
        Assert.Equal("7", request.Tokens["daysOverdue"]);
    }

    [Fact]
    public async Task NotifyDueSoonAsync_OrgHasOwnerEmail_EnqueuesTransactionalDueSoonToOwner()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgWithOwnerAsync(db, "owner@club.test");
        var recorder = new RecordingNotificationService();
        var notifier = new EfInvoiceNotifier(new EfOrganizationOwnerResolver(db), recorder);
        var invoice = NewInvoice(orgId);

        await notifier.NotifyDueSoonAsync(invoice, Now.AddDays(4), CancellationToken.None);

        var request = Assert.Single(recorder.Requests);
        Assert.Equal(NotificationTemplateKeys.InvoiceDueSoon, request.TemplateKey);
        Assert.Equal(NotificationCategory.Transactional, request.Category);
        Assert.Equal("owner@club.test", request.Recipient.EmailAddress);
        Assert.Equal($"invoice-due-soon:{invoice.InvoiceId:N}", request.IdempotencyKey);
        Assert.Equal("2900.00", request.Tokens["amount"]);
        Assert.Equal("2026-05-08", request.Tokens["dueDate"]);
    }

    [Fact]
    public async Task NotifyIssuedAsync_OwnerHasNoEmail_SendsNothing()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgWithOwnerAsync(db, ownerEmail: null);
        var recorder = new RecordingNotificationService();
        var notifier = new EfInvoiceNotifier(new EfOrganizationOwnerResolver(db), recorder);

        await notifier.NotifyIssuedAsync(NewInvoice(orgId), CancellationToken.None);

        Assert.Empty(recorder.Requests);
    }

    [Fact]
    public async Task NotifyIssuedAsync_NoOwnerForOrg_SendsNothing()
    {
        await using var db = NewContext();
        var recorder = new RecordingNotificationService();
        var notifier = new EfInvoiceNotifier(new EfOrganizationOwnerResolver(db), recorder);

        await notifier.NotifyIssuedAsync(NewInvoice(Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(recorder.Requests);
    }
}
