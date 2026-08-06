using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;

namespace AFK4.Platform.Api.Audit;

public sealed class AuditRecordStager(
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    IStaffContextAccessor staffContextAccessor) : IAuditRecordStager
{
    public void Stage(AuditRecordWriteRequest request)
    {
        var support = staffContextAccessor.Current?.SupportAccess;
        if (support is not null)
        {
            // Под грантом действует платформенный сотрудник; записать сотрудника клуба означало бы
            // приписать клубу чужое действие.
            request = request with
            {
                ActorStaffUserId = null,
                ActorPlatformAdminUserId = support.PlatformAdminUserId
            };
        }

        dbContext.AuditRecords.Add(AuditRecordFactory.Create(request, timeProvider.GetUtcNow()));
    }
}
