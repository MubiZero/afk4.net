using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class SupportAccessAuditTests
{
    [Fact]
    public async Task WriteAsync_UnderSupportAccess_RecordsPlatformAdminAsActor()
    {
        await using var factory = new PlatformApiFactory();
        var organizationId = Guid.NewGuid();
        var platformAdminUserId = Guid.NewGuid();

        await using var scope = factory.Services.CreateAsyncScope();
        var staffContextAccessor = scope.ServiceProvider.GetRequiredService<IStaffContextAccessor>();
        staffContextAccessor.Current = new StaffContext(
            StaffUserId: Guid.Empty,
            OrganizationId: organizationId,
            DisplayName: "Поддержка платформы",
            BranchIds: new HashSet<Guid>(),
            Permissions: new HashSet<string>())
        {
            SupportAccess = new PlatformSupportContext(
                GrantId: Guid.NewGuid(),
                PlatformAdminUserId: platformAdminUserId,
                OrganizationId: organizationId,
                Reason: "Проверка настроек филиала по обращению клиента",
                Permission: "branch-settings.manage",
                ExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(30))
        };

        var auditRecordWriter = scope.ServiceProvider.GetRequiredService<IAuditRecordWriter>();
        await auditRecordWriter.WriteAsync(
            new AuditRecordWriteRequest(
                organizationId,
                null,
                Guid.Empty,
                "test.action",
                "TestTarget",
                null,
                AuditOutcome.Succeeded,
                "PlatformApi",
                "{}"),
            CancellationToken.None);

        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var record = await db.AuditRecords
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .FirstAsync(candidate => candidate.OrganizationId == organizationId);

        Assert.Equal(platformAdminUserId, record.ActorPlatformAdminUserId);
        Assert.Null(record.ActorStaffUserId);
    }

    [Fact]
    public async Task WriteAsync_WithoutSupportAccess_RecordsStaffUserAsActor()
    {
        await using var factory = new PlatformApiFactory();
        var organizationId = Guid.NewGuid();
        var staffUserId = Guid.NewGuid();

        await using var scope = factory.Services.CreateAsyncScope();
        var staffContextAccessor = scope.ServiceProvider.GetRequiredService<IStaffContextAccessor>();
        staffContextAccessor.Current = new StaffContext(
            StaffUserId: staffUserId,
            OrganizationId: organizationId,
            DisplayName: "Обычный сотрудник",
            BranchIds: new HashSet<Guid>(),
            Permissions: new HashSet<string>());

        var auditRecordWriter = scope.ServiceProvider.GetRequiredService<IAuditRecordWriter>();
        await auditRecordWriter.WriteAsync(
            new AuditRecordWriteRequest(
                organizationId,
                null,
                staffUserId,
                "test.action",
                "TestTarget",
                null,
                AuditOutcome.Succeeded,
                "PlatformApi",
                "{}"),
            CancellationToken.None);

        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var record = await db.AuditRecords
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .FirstAsync(candidate => candidate.OrganizationId == organizationId);

        Assert.Equal(staffUserId, record.ActorStaffUserId);
        Assert.Null(record.ActorPlatformAdminUserId);
    }
}
