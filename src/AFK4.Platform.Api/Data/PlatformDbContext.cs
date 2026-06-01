using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Data;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();

    public DbSet<BranchEntity> Branches => Set<BranchEntity>();

    public DbSet<StaffUserEntity> StaffUsers => Set<StaffUserEntity>();

    public DbSet<StaffRoleAssignmentEntity> StaffRoleAssignments => Set<StaffRoleAssignmentEntity>();

    public DbSet<StaffAccessTokenEntity> StaffAccessTokens => Set<StaffAccessTokenEntity>();

    public DbSet<StaffRefreshTokenEntity> StaffRefreshTokens => Set<StaffRefreshTokenEntity>();

    public DbSet<AuditRecordEntity> AuditRecords => Set<AuditRecordEntity>();

    public DbSet<ZoneEntity> Zones => Set<ZoneEntity>();

    public DbSet<SeatEntity> Seats => Set<SeatEntity>();

    public DbSet<DeviceEntity> Devices => Set<DeviceEntity>();

    public DbSet<DeviceSeatAssignmentEntity> DeviceSeatAssignments => Set<DeviceSeatAssignmentEntity>();

    public DbSet<DeviceCredentialEntity> DeviceCredentials => Set<DeviceCredentialEntity>();

    public DbSet<DeviceEnrollmentCodeEntity> DeviceEnrollmentCodes => Set<DeviceEnrollmentCodeEntity>();

    public DbSet<DeviceCommandEntity> DeviceCommands => Set<DeviceCommandEntity>();

    public DbSet<DeviceInstalledAppEntity> DeviceInstalledApps => Set<DeviceInstalledAppEntity>();

    public DbSet<SessionEntity> Sessions => Set<SessionEntity>();

    public DbSet<SessionEventEntity> SessionEvents => Set<SessionEventEntity>();

    public DbSet<SessionLeaseEntity> SessionLeases => Set<SessionLeaseEntity>();

    public DbSet<SessionCommandIdempotencyEntity> SessionCommandIdempotency => Set<SessionCommandIdempotencyEntity>();

    public DbSet<PlayerAccountEntity> PlayerAccounts => Set<PlayerAccountEntity>();

    public DbSet<LedgerEntryEntity> LedgerEntries => Set<LedgerEntryEntity>();

    public DbSet<BillingCommandIdempotencyEntity> BillingCommandIdempotency => Set<BillingCommandIdempotencyEntity>();

    public DbSet<TariffEntity> Tariffs => Set<TariffEntity>();

    public DbSet<TariffVersionEntity> TariffVersions => Set<TariffVersionEntity>();

    public DbSet<PackageDefinitionEntity> PackageDefinitions => Set<PackageDefinitionEntity>();

    public DbSet<PlayerPackageEntity> PlayerPackages => Set<PlayerPackageEntity>();

    public DbSet<ShiftEntity> Shifts => Set<ShiftEntity>();

    public DbSet<CashMovementEntity> CashMovements => Set<CashMovementEntity>();

    public DbSet<PosProductCategoryEntity> PosProductCategories => Set<PosProductCategoryEntity>();

    public DbSet<PosProductEntity> PosProducts => Set<PosProductEntity>();

    public DbSet<StockMovementEntity> StockMovements => Set<StockMovementEntity>();

    public DbSet<PosSaleEntity> PosSales => Set<PosSaleEntity>();

    public DbSet<PosSaleLineEntity> PosSaleLines => Set<PosSaleLineEntity>();

    public DbSet<PaymentEntity> Payments => Set<PaymentEntity>();

    public DbSet<ReceiptEntity> Receipts => Set<ReceiptEntity>();

    public DbSet<UpdatePackageEntity> UpdatePackages => Set<UpdatePackageEntity>();

    public DbSet<UpdateRolloutEntity> UpdateRollouts => Set<UpdateRolloutEntity>();

    public DbSet<UpdateRolloutTargetEntity> UpdateRolloutTargets => Set<UpdateRolloutTargetEntity>();

    public DbSet<DeviceUpdateStatusEntity> DeviceUpdateStatuses => Set<DeviceUpdateStatusEntity>();

    public DbSet<ReservationEntity> Reservations => Set<ReservationEntity>();

    public DbSet<PlatformAdminUserEntity> PlatformAdminUsers => Set<PlatformAdminUserEntity>();

    public DbSet<PlatformAdminAccessTokenEntity> PlatformAdminAccessTokens => Set<PlatformAdminAccessTokenEntity>();

    public DbSet<PlatformAdminRefreshTokenEntity> PlatformAdminRefreshTokens => Set<PlatformAdminRefreshTokenEntity>();

    public DbSet<OwnerInviteEntity> OwnerInvites => Set<OwnerInviteEntity>();

    public DbSet<OwnerCodeEntity> OwnerCodes => Set<OwnerCodeEntity>();

    public DbSet<TenantSupportNoteEntity> TenantSupportNotes => Set<TenantSupportNoteEntity>();

    public DbSet<PlatformIdempotencyRecordEntity> PlatformIdempotencyRecords => Set<PlatformIdempotencyRecordEntity>();

    public DbSet<SubscriptionPlanEntity> SubscriptionPlans => Set<SubscriptionPlanEntity>();

    public DbSet<TenantSubscriptionEntity> TenantSubscriptions => Set<TenantSubscriptionEntity>();

    public DbSet<InvoiceEntity> Invoices => Set<InvoiceEntity>();

    public DbSet<NotificationOutboxEntity> NotificationOutbox => Set<NotificationOutboxEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganizationEntity>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(organization => organization.OrganizationId);
            entity.Property(organization => organization.Slug).HasMaxLength(64).IsRequired();
            entity.Property(organization => organization.Name).HasMaxLength(160).IsRequired();
            entity.Property(organization => organization.Status).HasMaxLength(32).IsRequired();
            entity.Property(organization => organization.StatusReason).HasMaxLength(512);
            entity.Property(organization => organization.PlanCode).HasMaxLength(64).IsRequired();
            entity.Property(organization => organization.SubscriptionStatus).HasMaxLength(32).IsRequired();
            entity.Property(organization => organization.LimitsJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(organization => organization.Slug).IsUnique();
            entity.HasIndex(organization => organization.Status);
        });

        modelBuilder.Entity<SubscriptionPlanEntity>(entity =>
        {
            entity.ToTable("subscription_plans");
            entity.HasKey(plan => plan.PlanCode);
            entity.Property(plan => plan.PlanCode).HasMaxLength(64).IsRequired();
            entity.Property(plan => plan.Name).HasMaxLength(160).IsRequired();
            entity.Property(plan => plan.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(plan => plan.BillingInterval).HasMaxLength(16).IsRequired();
            entity.HasIndex(plan => plan.SortOrder);
        });

        modelBuilder.Entity<TenantSubscriptionEntity>(entity =>
        {
            entity.ToTable("tenant_subscriptions");
            entity.HasKey(subscription => subscription.TenantSubscriptionId);
            entity.Property(subscription => subscription.PlanCode).HasMaxLength(64).IsRequired();
            entity.Property(subscription => subscription.Status).HasMaxLength(32).IsRequired();
            entity.Property(subscription => subscription.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(subscription => subscription.BillingInterval).HasMaxLength(16).IsRequired();
            entity.HasIndex(subscription => subscription.OrganizationId).IsUnique();
            entity.HasIndex(subscription => new { subscription.Status, subscription.NextInvoiceUtc });
        });

        modelBuilder.Entity<InvoiceEntity>(entity =>
        {
            entity.ToTable("invoices");
            entity.HasKey(invoice => invoice.InvoiceId);
            entity.Property(invoice => invoice.Kind).HasMaxLength(16).IsRequired();
            entity.Property(invoice => invoice.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(invoice => invoice.Status).HasMaxLength(16).IsRequired();
            entity.Property(invoice => invoice.VoidReason).HasMaxLength(512);
            entity.Property(invoice => invoice.Description).HasMaxLength(240).IsRequired();
            entity.HasIndex(invoice => invoice.Number).IsUnique();
            entity.HasIndex(invoice => new { invoice.OrganizationId, invoice.IssuedAtUtc });
            entity.HasIndex(invoice => new { invoice.Status, invoice.DueAtUtc });
        });

        modelBuilder.Entity<BranchEntity>(entity =>
        {
            entity.ToTable("branches");
            entity.HasKey(branch => branch.BranchId);
            entity.Property(branch => branch.Slug).HasMaxLength(64).IsRequired();
            entity.Property(branch => branch.Name).HasMaxLength(160).IsRequired();
            entity.Property(branch => branch.City).HasMaxLength(120).IsRequired();
            entity.Property(branch => branch.RequireManualDeviceApproval).HasDefaultValue(false);
            entity.HasIndex(branch => new { branch.OrganizationId, branch.BranchId }).IsUnique();
            entity.HasIndex(branch => new { branch.OrganizationId, branch.Slug }).IsUnique();
        });

        modelBuilder.Entity<StaffUserEntity>(entity =>
        {
            entity.ToTable("staff_users");
            entity.HasKey(staffUser => staffUser.StaffUserId);
            entity.Property(staffUser => staffUser.UserName).HasMaxLength(256).IsRequired();
            entity.Property(staffUser => staffUser.NormalizedUserName).HasMaxLength(256).IsRequired();
            entity.Property(staffUser => staffUser.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(staffUser => staffUser.PasswordHash).IsRequired();
            entity.HasIndex(staffUser => new { staffUser.OrganizationId, staffUser.NormalizedUserName }).IsUnique();
        });

        modelBuilder.Entity<StaffRoleAssignmentEntity>(entity =>
        {
            entity.ToTable("staff_role_assignments");
            entity.HasKey(roleAssignment => roleAssignment.StaffRoleAssignmentId);
            entity.Property(roleAssignment => roleAssignment.RoleName).HasMaxLength(64).IsRequired();
            entity.HasIndex(roleAssignment => new
            {
                roleAssignment.StaffUserId,
                roleAssignment.OrganizationId,
                roleAssignment.BranchId,
                roleAssignment.RoleName
            }).IsUnique();
        });

        modelBuilder.Entity<StaffAccessTokenEntity>(entity =>
        {
            entity.ToTable("staff_access_tokens");
            entity.HasKey(accessToken => accessToken.StaffAccessTokenId);
            entity.Property(accessToken => accessToken.TokenHash).IsRequired();
            entity.HasIndex(accessToken => accessToken.TokenHash);
            entity.HasIndex(accessToken => new { accessToken.StaffUserId, accessToken.ExpiresAtUtc });
        });

        modelBuilder.Entity<StaffRefreshTokenEntity>(entity =>
        {
            entity.ToTable("staff_refresh_tokens");
            entity.HasKey(refreshToken => refreshToken.StaffRefreshTokenId);
            entity.Property(refreshToken => refreshToken.TokenHash).IsRequired();
            entity.HasIndex(refreshToken => refreshToken.TokenHash);
            entity.HasIndex(refreshToken => new { refreshToken.StaffUserId, refreshToken.ExpiresAtUtc });
        });

        modelBuilder.Entity<AuditRecordEntity>(entity =>
        {
            entity.ToTable("audit_records");
            entity.HasKey(auditRecord => auditRecord.AuditRecordId);
            entity.Property(auditRecord => auditRecord.Action).HasMaxLength(128).IsRequired();
            entity.Property(auditRecord => auditRecord.TargetType).HasMaxLength(128).IsRequired();
            entity.Property(auditRecord => auditRecord.TargetId).HasMaxLength(128);
            entity.Property(auditRecord => auditRecord.Outcome).HasMaxLength(32).IsRequired();
            entity.Property(auditRecord => auditRecord.SourceApp).HasMaxLength(64).IsRequired();
            entity.Property(auditRecord => auditRecord.DetailsJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(auditRecord => new
            {
                auditRecord.OrganizationId,
                auditRecord.BranchId,
                auditRecord.CreatedAtUtc
            });
            entity.HasIndex(auditRecord => new
            {
                auditRecord.ActorPlatformAdminUserId,
                auditRecord.CreatedAtUtc
            });
        });

        modelBuilder.Entity<ZoneEntity>(entity =>
        {
            entity.ToTable("zones");
            entity.HasKey(zone => zone.ZoneId);
            entity.Property(zone => zone.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(zone => new { zone.OrganizationId, zone.BranchId, zone.SortOrder });
        });

        modelBuilder.Entity<SeatEntity>(entity =>
        {
            entity.ToTable("seats");
            entity.HasKey(seat => seat.SeatId);
            entity.Property(seat => seat.Name).HasMaxLength(80).IsRequired();
            entity.HasIndex(seat => new { seat.OrganizationId, seat.BranchId, seat.ZoneId, seat.SortOrder });
        });

        modelBuilder.Entity<DeviceEntity>(entity =>
        {
            entity.ToTable("devices");
            entity.HasKey(device => device.DeviceId);
            entity.Property(device => device.MachineName).HasMaxLength(128).IsRequired();
            entity.Property(device => device.DisplayName).HasMaxLength(80).IsRequired();
            entity.Property(device => device.DevicePublicKey).HasMaxLength(4096).IsRequired();
            entity.Property(device => device.Role).HasMaxLength(32).IsRequired();
            entity.Property(device => device.EnrollmentState).HasMaxLength(32).IsRequired();
            entity.Property(device => device.AgentVersion).HasMaxLength(64).IsRequired();
            entity.Property(device => device.ShellVersion).HasMaxLength(64).IsRequired();
            entity.HasIndex(device => new { device.OrganizationId, device.BranchId });
            entity.HasIndex(device => new { device.OrganizationId, device.BranchId, device.EnrollmentState });
            entity.HasIndex(device => device.EnrolledViaOwnerCodeId);
        });

        modelBuilder.Entity<DeviceSeatAssignmentEntity>(entity =>
        {
            entity.ToTable("device_seat_assignments");
            entity.HasKey(assignment => assignment.DeviceSeatAssignmentId);
            entity.HasIndex(assignment => new { assignment.SeatId, assignment.DetachedAtUtc });
            entity.HasIndex(assignment => new { assignment.DeviceId, assignment.DetachedAtUtc });
            entity.HasIndex(assignment => new { assignment.OrganizationId, assignment.BranchId });
        });

        modelBuilder.Entity<DeviceCredentialEntity>(entity =>
        {
            entity.ToTable("device_credentials");
            entity.HasKey(credential => credential.CredentialId);
            entity.Property(credential => credential.SecretHash).IsRequired();
            entity.HasIndex(credential => credential.DeviceId);
            entity.HasIndex(credential => new { credential.OrganizationId, credential.BranchId, credential.DeviceId });
        });

        modelBuilder.Entity<DeviceEnrollmentCodeEntity>(entity =>
        {
            entity.ToTable("device_enrollment_codes");
            entity.HasKey(code => code.Code);
            entity.Property(code => code.Code).HasMaxLength(32).IsRequired();
            entity.HasIndex(code => new { code.OrganizationId, code.BranchId });
            entity.HasIndex(code => code.ExpiresAtUtc);
        });

        modelBuilder.Entity<DeviceCommandEntity>(entity =>
        {
            entity.ToTable("device_commands");
            entity.HasKey(command => command.CommandId);
            entity.Property(command => command.Type).HasMaxLength(64).IsRequired();
            entity.Property(command => command.Status).HasMaxLength(32).IsRequired();
            entity.Property(command => command.Message).HasMaxLength(512);
            entity.Property(command => command.PayloadJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(command => new { command.DeviceId, command.CommandId }).IsUnique();
        });

        modelBuilder.Entity<DeviceInstalledAppEntity>(entity =>
        {
            entity.ToTable("device_installed_apps");
            entity.HasKey(app => app.DeviceInstalledAppId);
            entity.Property(app => app.DisplayName).HasMaxLength(240).IsRequired();
            entity.Property(app => app.Version).HasMaxLength(120);
            entity.Property(app => app.Publisher).HasMaxLength(160);
            entity.Property(app => app.InstallLocation).HasMaxLength(512);
            entity.HasIndex(app => new { app.DeviceId, app.DisplayName });
            entity.HasIndex(app => new { app.OrganizationId, app.BranchId, app.DeviceId });
            entity.HasIndex(app => app.ReportedAtUtc);
        });

        modelBuilder.Entity<SessionEntity>(entity =>
        {
            entity.ToTable("sessions");
            entity.HasKey(session => session.SessionId);
            entity.Property(session => session.PlayerKind).HasMaxLength(32).IsRequired();
            entity.Property(session => session.TariffRuleVersionId).HasMaxLength(128).IsRequired();
            entity.Property(session => session.State).HasMaxLength(32).IsRequired();
            entity.HasIndex(session => new
            {
                session.OrganizationId,
                session.BranchId,
                session.SeatId,
                session.State
            });
            entity.HasIndex(session => new
            {
                session.OrganizationId,
                session.BranchId,
                session.DeviceId,
                session.State
            });
            entity.HasIndex(session => session.CurrentLeaseId);
        });

        modelBuilder.Entity<SessionEventEntity>(entity =>
        {
            entity.ToTable("session_events");
            entity.HasKey(sessionEvent => sessionEvent.SessionEventId);
            entity.Property(sessionEvent => sessionEvent.EventType).HasMaxLength(80).IsRequired();
            entity.Property(sessionEvent => sessionEvent.DetailsJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(sessionEvent => new { sessionEvent.SessionId, sessionEvent.CreatedAtUtc });
        });

        modelBuilder.Entity<SessionLeaseEntity>(entity =>
        {
            entity.ToTable("session_leases");
            entity.HasKey(lease => lease.SessionLeaseId);
            entity.Property(lease => lease.State).HasMaxLength(32).IsRequired();
            entity.Property(lease => lease.SignatureAlgorithm).HasMaxLength(64).IsRequired();
            entity.Property(lease => lease.Signature).IsRequired();
            entity.Property(lease => lease.PayloadJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(lease => new { lease.SessionId, lease.Sequence }).IsUnique();
            entity.HasIndex(lease => new { lease.DeviceId, lease.ExpiresAtUtc });
        });

        modelBuilder.Entity<SessionCommandIdempotencyEntity>(entity =>
        {
            entity.ToTable("session_command_idempotency");
            entity.HasKey(record => record.SessionCommandIdempotencyId);
            entity.Property(record => record.IdempotencyKeyHash).HasMaxLength(128).IsRequired();
            entity.Property(record => record.Operation).HasMaxLength(64).IsRequired();
            entity.Property(record => record.RequestHash).HasMaxLength(128).IsRequired();
            entity.Property(record => record.ResponseJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(record => new
            {
                record.OrganizationId,
                record.BranchId,
                record.IdempotencyKeyHash,
                record.Operation
            }).IsUnique();
        });

        modelBuilder.Entity<PlayerAccountEntity>(entity =>
        {
            entity.ToTable("player_accounts");
            entity.HasKey(player => player.PlayerAccountId);
            entity.Property(player => player.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(player => player.PhoneNumber).HasMaxLength(64);
            entity.HasIndex(player => new { player.OrganizationId, player.HomeBranchId });
        });

        modelBuilder.Entity<LedgerEntryEntity>(entity =>
        {
            entity.ToTable("ledger_entries");
            entity.HasKey(entry => entry.LedgerEntryId);
            entity.Property(entry => entry.EntryType).HasMaxLength(64).IsRequired();
            entity.Property(entry => entry.AccountType).HasMaxLength(64).IsRequired();
            entity.Property(entry => entry.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(entry => entry.Description).HasMaxLength(240).IsRequired();
            entity.Property(entry => entry.Reason).HasMaxLength(512).IsRequired();
            entity.HasIndex(entry => new { entry.OrganizationId, entry.BranchId, entry.CreatedAtUtc });
            entity.HasIndex(entry => new { entry.PlayerAccountId, entry.CreatedAtUtc });
            entity.HasIndex(entry => new { entry.ShiftId, entry.CreatedAtUtc });
            entity.HasIndex(entry => entry.SessionId);
            entity.HasIndex(entry => entry.PlayerPackageId);
            entity.HasIndex(entry => entry.ReversesLedgerEntryId);
        });

        modelBuilder.Entity<BillingCommandIdempotencyEntity>(entity =>
        {
            entity.ToTable("billing_command_idempotency");
            entity.HasKey(record => record.BillingCommandIdempotencyId);
            entity.Property(record => record.Operation).HasMaxLength(64).IsRequired();
            entity.Property(record => record.IdempotencyKeyHash).HasMaxLength(128).IsRequired();
            entity.Property(record => record.RequestHash).HasMaxLength(128).IsRequired();
            entity.Property(record => record.ResponseJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(record => new
            {
                record.OrganizationId,
                record.BranchId,
                record.Operation,
                record.IdempotencyKeyHash
            }).IsUnique();
        });

        modelBuilder.Entity<TariffEntity>(entity =>
        {
            entity.ToTable("tariffs");
            entity.HasKey(tariff => tariff.TariffId);
            entity.Property(tariff => tariff.Name).HasMaxLength(160).IsRequired();
            entity.HasIndex(tariff => new { tariff.OrganizationId, tariff.BranchId, tariff.Name }).IsUnique();
        });

        modelBuilder.Entity<TariffVersionEntity>(entity =>
        {
            entity.ToTable("tariff_versions");
            entity.HasKey(version => version.TariffVersionId);
            entity.Property(version => version.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.HasIndex(version => new { version.TariffId, version.VersionNumber }).IsUnique();
            entity.HasIndex(version => new { version.OrganizationId, version.BranchId, version.EffectiveFromUtc });
        });

        modelBuilder.Entity<PackageDefinitionEntity>(entity =>
        {
            entity.ToTable("package_definitions");
            entity.HasKey(package => package.PackageDefinitionId);
            entity.Property(package => package.Name).HasMaxLength(160).IsRequired();
            entity.Property(package => package.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.HasIndex(package => new { package.OrganizationId, package.BranchId, package.Name }).IsUnique();
        });

        modelBuilder.Entity<PlayerPackageEntity>(entity =>
        {
            entity.ToTable("player_packages");
            entity.HasKey(package => package.PlayerPackageId);
            entity.Property(package => package.Name).HasMaxLength(160).IsRequired();
            entity.Property(package => package.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.HasIndex(package => new { package.PlayerAccountId, package.PurchasedAtUtc });
            entity.HasIndex(package => new { package.OrganizationId, package.BranchId });
        });

        modelBuilder.Entity<ShiftEntity>(entity =>
        {
            entity.ToTable("shifts");
            entity.HasKey(shift => shift.ShiftId);
            entity.Property(shift => shift.State).HasMaxLength(32).IsRequired();
            entity.Property(shift => shift.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(shift => shift.OpeningNote).HasMaxLength(512).IsRequired();
            entity.Property(shift => shift.ClosingNote).HasMaxLength(512).IsRequired();
            entity.HasIndex(shift => new
            {
                shift.OrganizationId,
                shift.BranchId,
                shift.State
            });
        });

        modelBuilder.Entity<CashMovementEntity>(entity =>
        {
            entity.ToTable("cash_movements");
            entity.HasKey(movement => movement.CashMovementId);
            entity.Property(movement => movement.MovementType).HasMaxLength(32).IsRequired();
            entity.Property(movement => movement.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(movement => movement.Reason).HasMaxLength(512).IsRequired();
            entity.HasIndex(movement => new { movement.ShiftId, movement.CreatedAtUtc });
        });

        modelBuilder.Entity<PosProductCategoryEntity>(entity =>
        {
            entity.ToTable("pos_product_categories");
            entity.HasKey(category => category.CategoryId);
            entity.Property(category => category.Name).HasMaxLength(160).IsRequired();
            entity.HasIndex(category => new
            {
                category.OrganizationId,
                category.BranchId,
                category.Name
            }).IsUnique();
        });

        modelBuilder.Entity<PosProductEntity>(entity =>
        {
            entity.ToTable("pos_products");
            entity.HasKey(product => product.ProductId);
            entity.Property(product => product.Name).HasMaxLength(160).IsRequired();
            entity.Property(product => product.Sku).HasMaxLength(80).IsRequired();
            entity.Property(product => product.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.HasIndex(product => new
            {
                product.OrganizationId,
                product.BranchId,
                product.Sku
            }).IsUnique();
            entity.HasIndex(product => new
            {
                product.OrganizationId,
                product.BranchId,
                product.CategoryId
            });
        });

        modelBuilder.Entity<StockMovementEntity>(entity =>
        {
            entity.ToTable("stock_movements");
            entity.HasKey(movement => movement.StockMovementId);
            entity.Property(movement => movement.MovementType).HasMaxLength(32).IsRequired();
            entity.Property(movement => movement.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(movement => movement.Reason).HasMaxLength(512).IsRequired();
            entity.HasIndex(movement => new { movement.ProductId, movement.CreatedAtUtc });
            entity.HasIndex(movement => new
            {
                movement.OrganizationId,
                movement.BranchId,
                movement.CreatedAtUtc
            });
        });

        modelBuilder.Entity<PosSaleEntity>(entity =>
        {
            entity.ToTable("pos_sales");
            entity.HasKey(sale => sale.PosSaleId);
            entity.Property(sale => sale.State).HasMaxLength(32).IsRequired();
            entity.Property(sale => sale.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(sale => sale.RefundReason).HasMaxLength(512).IsRequired();
            entity.Property(sale => sale.VoidReason).HasMaxLength(512).IsRequired();
            entity.HasIndex(sale => new
            {
                sale.OrganizationId,
                sale.BranchId,
                sale.ShiftId,
                sale.CreatedAtUtc
            });
            entity.HasIndex(sale => sale.PlayerAccountId);
            entity.HasIndex(sale => sale.State);
        });

        modelBuilder.Entity<PosSaleLineEntity>(entity =>
        {
            entity.ToTable("pos_sale_lines");
            entity.HasKey(line => line.PosSaleLineId);
            entity.Property(line => line.ProductName).HasMaxLength(160).IsRequired();
            entity.Property(line => line.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.HasIndex(line => line.PosSaleId);
        });

        modelBuilder.Entity<PaymentEntity>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(payment => payment.PaymentId);
            entity.Property(payment => payment.PaymentKind).HasMaxLength(32).IsRequired();
            entity.Property(payment => payment.Provider).HasMaxLength(64).IsRequired();
            entity.Property(payment => payment.PaymentMethod).HasMaxLength(64).IsRequired();
            entity.Property(payment => payment.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(payment => payment.Note).HasMaxLength(512).IsRequired();
            entity.HasIndex(payment => new { payment.PosSaleId, payment.CreatedAtUtc });
        });

        modelBuilder.Entity<ReceiptEntity>(entity =>
        {
            entity.ToTable("receipts");
            entity.HasKey(receipt => receipt.ReceiptId);
            entity.Property(receipt => receipt.ReceiptNumber).HasMaxLength(32).IsRequired();
            entity.Property(receipt => receipt.ReceiptType).HasMaxLength(32).IsRequired();
            entity.Property(receipt => receipt.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.HasIndex(receipt => new
            {
                receipt.OrganizationId,
                receipt.BranchId,
                receipt.ReceiptNumber
            }).IsUnique();
            entity.HasIndex(receipt => receipt.PosSaleId);
        });

        modelBuilder.Entity<UpdatePackageEntity>(entity =>
        {
            entity.ToTable("update_packages");
            entity.HasKey(package => package.UpdatePackageId);
            entity.Property(package => package.Component).HasMaxLength(64).IsRequired();
            entity.Property(package => package.Version).HasMaxLength(64).IsRequired();
            entity.Property(package => package.Channel).HasMaxLength(32).IsRequired();
            entity.Property(package => package.ArtifactUri).HasMaxLength(1024).IsRequired();
            entity.Property(package => package.Sha256).HasMaxLength(128).IsRequired();
            entity.Property(package => package.Signature).IsRequired();
            entity.Property(package => package.SignatureAlgorithm).HasMaxLength(64).IsRequired();
            entity.Property(package => package.State).HasMaxLength(32).IsRequired();
            entity.Property(package => package.ReleaseNotes).HasMaxLength(2000).IsRequired();
            entity.HasIndex(package => new
            {
                package.OrganizationId,
                package.BranchId,
                package.Component,
                package.Version,
                package.Channel
            }).IsUnique();
            entity.HasIndex(package => new { package.OrganizationId, package.BranchId, package.CreatedAtUtc });
        });

        modelBuilder.Entity<UpdateRolloutEntity>(entity =>
        {
            entity.ToTable("update_rollouts");
            entity.HasKey(rollout => rollout.UpdateRolloutId);
            entity.Property(rollout => rollout.Component).HasMaxLength(64).IsRequired();
            entity.Property(rollout => rollout.Version).HasMaxLength(64).IsRequired();
            entity.Property(rollout => rollout.Channel).HasMaxLength(32).IsRequired();
            entity.Property(rollout => rollout.State).HasMaxLength(32).IsRequired();
            entity.Property(rollout => rollout.TargetKind).HasMaxLength(32).IsRequired();
            entity.Property(rollout => rollout.Reason).HasMaxLength(512).IsRequired();
            entity.HasIndex(rollout => new
            {
                rollout.OrganizationId,
                rollout.BranchId,
                rollout.Channel,
                rollout.State,
                rollout.StartsAtUtc
            });
            entity.HasIndex(rollout => rollout.UpdatePackageId);
        });

        modelBuilder.Entity<UpdateRolloutTargetEntity>(entity =>
        {
            entity.ToTable("update_rollout_targets");
            entity.HasKey(target => target.UpdateRolloutTargetId);
            entity.Property(target => target.TargetKind).HasMaxLength(32).IsRequired();
            entity.HasIndex(target => new { target.UpdateRolloutId, target.DeviceId }).IsUnique();
            entity.HasIndex(target => new { target.OrganizationId, target.BranchId, target.DeviceId });
        });

        modelBuilder.Entity<DeviceUpdateStatusEntity>(entity =>
        {
            entity.ToTable("device_update_statuses");
            entity.HasKey(status => status.DeviceUpdateStatusId);
            entity.Property(status => status.Component).HasMaxLength(64).IsRequired();
            entity.Property(status => status.InstalledVersion).HasMaxLength(64).IsRequired();
            entity.Property(status => status.TargetVersion).HasMaxLength(64).IsRequired();
            entity.Property(status => status.Status).HasMaxLength(32).IsRequired();
            entity.Property(status => status.Message).HasMaxLength(512).IsRequired();
            entity.HasIndex(status => new
            {
                status.DeviceId,
                status.UpdateRolloutId,
                status.UpdatePackageId,
                status.Component
            }).IsUnique();
            entity.HasIndex(status => new
            {
                status.OrganizationId,
                status.BranchId,
                status.Status,
                status.UpdatedAtUtc
            });
        });

        modelBuilder.Entity<ReservationEntity>(entity =>
        {
            entity.ToTable("reservations");
            entity.HasKey(reservation => reservation.ReservationId);
            entity.Property(reservation => reservation.CustomerName).HasMaxLength(160).IsRequired();
            entity.Property(reservation => reservation.PhoneNumber).HasMaxLength(64);
            entity.Property(reservation => reservation.State).HasMaxLength(32).IsRequired();
            entity.Property(reservation => reservation.Source).HasMaxLength(32).IsRequired();
            entity.Property(reservation => reservation.Note).HasMaxLength(512).IsRequired();
            entity.Property(reservation => reservation.CancelReason).HasMaxLength(512).IsRequired();
            entity.HasIndex(reservation => new
            {
                reservation.OrganizationId,
                reservation.BranchId,
                reservation.StartsAtUtc
            });
            entity.HasIndex(reservation => new
            {
                reservation.OrganizationId,
                reservation.BranchId,
                reservation.SeatId,
                reservation.StartsAtUtc,
                reservation.EndsAtUtc
            });
            entity.HasIndex(reservation => new
            {
                reservation.OrganizationId,
                reservation.BranchId,
                reservation.State,
                reservation.StartsAtUtc
            });
        });

        modelBuilder.Entity<PlatformAdminUserEntity>(entity =>
        {
            entity.ToTable("platform_admin_users");
            entity.HasKey(admin => admin.PlatformAdminUserId);
            entity.Property(admin => admin.UserName).HasMaxLength(256).IsRequired();
            entity.Property(admin => admin.NormalizedUserName).HasMaxLength(256).IsRequired();
            entity.Property(admin => admin.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(admin => admin.PasswordHash).IsRequired();
            entity.Property(admin => admin.RolesJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(admin => admin.NormalizedUserName).IsUnique();
        });

        modelBuilder.Entity<PlatformAdminAccessTokenEntity>(entity =>
        {
            entity.ToTable("platform_admin_access_tokens");
            entity.HasKey(accessToken => accessToken.PlatformAdminAccessTokenId);
            entity.Property(accessToken => accessToken.TokenHash).IsRequired();
            entity.HasIndex(accessToken => accessToken.TokenHash);
            entity.HasIndex(accessToken => new { accessToken.PlatformAdminUserId, accessToken.ExpiresAtUtc });
        });

        modelBuilder.Entity<PlatformAdminRefreshTokenEntity>(entity =>
        {
            entity.ToTable("platform_admin_refresh_tokens");
            entity.HasKey(refreshToken => refreshToken.PlatformAdminRefreshTokenId);
            entity.Property(refreshToken => refreshToken.TokenHash).IsRequired();
            entity.HasIndex(refreshToken => refreshToken.TokenHash);
            entity.HasIndex(refreshToken => new { refreshToken.PlatformAdminUserId, refreshToken.ExpiresAtUtc });
        });

        modelBuilder.Entity<OwnerInviteEntity>(entity =>
        {
            entity.ToTable("owner_invites");
            entity.HasKey(invite => invite.OwnerInviteId);
            entity.Property(invite => invite.Code).HasMaxLength(64).IsRequired();
            entity.Property(invite => invite.NormalizedCode).HasMaxLength(64).IsRequired();
            entity.Property(invite => invite.Status).HasMaxLength(32).IsRequired();
            entity.Property(invite => invite.OwnerUserName).HasMaxLength(256);
            entity.Property(invite => invite.OwnerDisplayName).HasMaxLength(160);
            entity.Property(invite => invite.RevokedReason).HasMaxLength(512);
            entity.HasIndex(invite => invite.NormalizedCode).IsUnique();
            entity.HasIndex(invite => new { invite.OrganizationId, invite.BranchId, invite.Status });
            entity.HasIndex(invite => invite.ExpiresAtUtc);
        });

        modelBuilder.Entity<OwnerCodeEntity>(entity =>
        {
            entity.ToTable("owner_codes");
            entity.HasKey(code => code.OwnerCodeId);
            entity.Property(code => code.CodeHash).HasMaxLength(64).IsRequired();
            entity.Property(code => code.CodeSuffix).HasMaxLength(4).IsRequired();
            entity.Property(code => code.RevokedReason).HasMaxLength(512);
            entity.HasIndex(code => code.CodeHash)
                .IsUnique()
                .HasFilter("\"RevokedAtUtc\" IS NULL");
            entity.HasIndex(code => code.StaffUserId)
                .IsUnique()
                .HasFilter("\"RevokedAtUtc\" IS NULL");
            entity.HasIndex(code => code.ExpiresAtUtc);
        });

        modelBuilder.Entity<TenantSupportNoteEntity>(entity =>
        {
            entity.ToTable("tenant_support_notes");
            entity.HasKey(note => note.TenantSupportNoteId);
            entity.Property(note => note.Body).HasMaxLength(4000).IsRequired();
            entity.HasIndex(note => new { note.OrganizationId, note.CreatedAtUtc });
        });

        modelBuilder.Entity<PlatformIdempotencyRecordEntity>(entity =>
        {
            entity.ToTable("platform_idempotency_records");
            entity.HasKey(record => record.PlatformIdempotencyRecordId);
            entity.Property(record => record.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.Property(record => record.Scope).HasMaxLength(64).IsRequired();
            entity.Property(record => record.RequestHash).HasMaxLength(128).IsRequired();
            entity.Property(record => record.ResponseBody).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(record => new { record.Scope, record.IdempotencyKey }).IsUnique();
            entity.HasIndex(record => record.ExpiresAtUtc);
        });

        modelBuilder.Entity<NotificationOutboxEntity>(entity =>
        {
            entity.ToTable("notification_outbox");
            entity.HasKey(row => row.NotificationOutboxId);
            entity.Property(row => row.IdempotencyKey).HasMaxLength(256).IsRequired();
            entity.Property(row => row.Channel).HasMaxLength(16).IsRequired();
            entity.Property(row => row.Category).HasMaxLength(16).IsRequired();
            entity.Property(row => row.TemplateKey).HasMaxLength(128).IsRequired();
            entity.Property(row => row.Locale).HasMaxLength(16).IsRequired();
            entity.Property(row => row.RecipientAddress).HasMaxLength(320);
            entity.Property(row => row.Subject).HasMaxLength(512);
            entity.Property(row => row.Status).HasMaxLength(16).IsRequired();
            entity.Property(row => row.LastError).HasMaxLength(2000);
            entity.HasIndex(row => row.IdempotencyKey).IsUnique();
            entity.HasIndex(row => new { row.Status, row.NextAttemptUtc });
        });
    }
}
