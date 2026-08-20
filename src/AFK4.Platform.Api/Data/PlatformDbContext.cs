using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Data;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();

    public DbSet<OrganizationLoyaltySettingsEntity> OrganizationLoyaltySettings => Set<OrganizationLoyaltySettingsEntity>();
    public DbSet<OrganizationReferralSettingsEntity> OrganizationReferralSettings => Set<OrganizationReferralSettingsEntity>();
    public DbSet<PlayerReferralEntity> PlayerReferrals => Set<PlayerReferralEntity>();

    public DbSet<NewsItemEntity> NewsItems => Set<NewsItemEntity>();

    public DbSet<UploadedMediaEntity> UploadedMedia => Set<UploadedMediaEntity>();

    public DbSet<ClubReviewEntity> ClubReviews => Set<ClubReviewEntity>();

    public DbSet<BranchEntity> Branches => Set<BranchEntity>();

    public DbSet<EskhataMerchantConfigEntity> EskhataMerchantConfigs => Set<EskhataMerchantConfigEntity>();

    public DbSet<DcPayLinkConfigEntity> DcPayLinkConfigs => Set<DcPayLinkConfigEntity>();

    public DbSet<StaffUserEntity> StaffUsers => Set<StaffUserEntity>();

    public DbSet<StaffRoleAssignmentEntity> StaffRoleAssignments => Set<StaffRoleAssignmentEntity>();

    public DbSet<StaffAccessTokenEntity> StaffAccessTokens => Set<StaffAccessTokenEntity>();

    public DbSet<StaffRefreshTokenEntity> StaffRefreshTokens => Set<StaffRefreshTokenEntity>();

    public DbSet<AuditRecordEntity> AuditRecords => Set<AuditRecordEntity>();

    public DbSet<ZoneEntity> Zones => Set<ZoneEntity>();

    public DbSet<SeatEntity> Seats => Set<SeatEntity>();

    public DbSet<WallEntity> Walls => Set<WallEntity>();

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

    public DbSet<PlayerCredentialEntity> PlayerCredentials => Set<PlayerCredentialEntity>();

    public DbSet<PlayerAccessTokenEntity> PlayerAccessTokens => Set<PlayerAccessTokenEntity>();

    public DbSet<PlayerRefreshTokenEntity> PlayerRefreshTokens => Set<PlayerRefreshTokenEntity>();

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

    public DbSet<ProductBarcodeEntity> ProductBarcodes => Set<ProductBarcodeEntity>();

    public DbSet<ShopOrderEntity> ShopOrders => Set<ShopOrderEntity>();

    public DbSet<ShopOrderLineEntity> ShopOrderLines => Set<ShopOrderLineEntity>();

    public DbSet<PosSaleEntity> PosSales => Set<PosSaleEntity>();

    public DbSet<PosSaleLineEntity> PosSaleLines => Set<PosSaleLineEntity>();

    public DbSet<PaymentEntity> Payments => Set<PaymentEntity>();

    public DbSet<ReceiptEntity> Receipts => Set<ReceiptEntity>();

    public DbSet<UpdatePackageEntity> UpdatePackages => Set<UpdatePackageEntity>();

    public DbSet<UpdateRolloutEntity> UpdateRollouts => Set<UpdateRolloutEntity>();

    public DbSet<UpdateRolloutTargetEntity> UpdateRolloutTargets => Set<UpdateRolloutTargetEntity>();

    public DbSet<DeviceUpdateStatusEntity> DeviceUpdateStatuses => Set<DeviceUpdateStatusEntity>();

    public DbSet<ReservationEntity> Reservations => Set<ReservationEntity>();

    public DbSet<PaymentIntentEntity> PaymentIntents => Set<PaymentIntentEntity>();

    public DbSet<PlatformAdminUserEntity> PlatformAdminUsers => Set<PlatformAdminUserEntity>();

    public DbSet<PlatformAdminAccessTokenEntity> PlatformAdminAccessTokens => Set<PlatformAdminAccessTokenEntity>();

    public DbSet<PlatformAdminRefreshTokenEntity> PlatformAdminRefreshTokens => Set<PlatformAdminRefreshTokenEntity>();

    public DbSet<PlatformSupportAccessGrantEntity> PlatformSupportAccessGrants => Set<PlatformSupportAccessGrantEntity>();

    public DbSet<PlatformIncidentEntity> PlatformIncidents => Set<PlatformIncidentEntity>();

    public DbSet<PlatformJobRunEntity> PlatformJobRuns => Set<PlatformJobRunEntity>();

    public DbSet<SubscriptionDailySnapshotEntity> SubscriptionDailySnapshots => Set<SubscriptionDailySnapshotEntity>();

    public DbSet<BranchDailySnapshotEntity> BranchDailySnapshots => Set<BranchDailySnapshotEntity>();

    public DbSet<PlatformFeatureEntity> PlatformFeatures => Set<PlatformFeatureEntity>();

    public DbSet<PlatformRoleEntity> PlatformRoles => Set<PlatformRoleEntity>();

    public DbSet<PlatformRolePermissionEntity> PlatformRolePermissions => Set<PlatformRolePermissionEntity>();

    public DbSet<PlatformAnnouncementEntity> PlatformAnnouncements => Set<PlatformAnnouncementEntity>();

    public DbSet<AnnouncementReadEntity> AnnouncementReads => Set<AnnouncementReadEntity>();

    public DbSet<PlanFeatureEntity> PlanFeatures => Set<PlanFeatureEntity>();

    public DbSet<OrganizationFeatureOverrideEntity> OrganizationFeatureOverrides => Set<OrganizationFeatureOverrideEntity>();

    public DbSet<OrganizationOwnerInviteEntity> OrganizationOwnerInvites => Set<OrganizationOwnerInviteEntity>();

    public DbSet<OrganizationSupportNoteEntity> OrganizationSupportNotes => Set<OrganizationSupportNoteEntity>();

    public DbSet<PlatformIdempotencyRecordEntity> PlatformIdempotencyRecords => Set<PlatformIdempotencyRecordEntity>();

    public DbSet<SubscriptionPlanEntity> SubscriptionPlans => Set<SubscriptionPlanEntity>();

    public DbSet<OrganizationSubscriptionEntity> OrganizationSubscriptions => Set<OrganizationSubscriptionEntity>();

    public DbSet<InvoiceEntity> Invoices => Set<InvoiceEntity>();

    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    public DbSet<NotificationOutboxEntity> NotificationOutbox => Set<NotificationOutboxEntity>();

    public DbSet<NotificationOutboxAttachmentEntity> NotificationOutboxAttachments => Set<NotificationOutboxAttachmentEntity>();

    public DbSet<NotificationPreferenceEntity> NotificationPreferences => Set<NotificationPreferenceEntity>();

    public DbSet<PlayerDeviceEntity> PlayerDevices => Set<PlayerDeviceEntity>();

    public DbSet<ReportScheduleEntity> ReportSchedules => Set<ReportScheduleEntity>();

    public DbSet<StaffMoneyCapEntity> StaffMoneyCaps => Set<StaffMoneyCapEntity>();

    public DbSet<MoneyActionRequestEntity> MoneyActionRequests => Set<MoneyActionRequestEntity>();

    public DbSet<PasswordResetTokenEntity> PasswordResetTokens => Set<PasswordResetTokenEntity>();

    public DbSet<StaffInviteEntity> StaffInvites => Set<StaffInviteEntity>();

    public DbSet<StaffPhoneOtpEntity> StaffPhoneOtps => Set<StaffPhoneOtpEntity>();

    public DbSet<PlayerPhoneOtpEntity> PlayerPhoneOtps => Set<PlayerPhoneOtpEntity>();

    public DbSet<PlatformAdminInvitationEntity> PlatformAdminInvitations => Set<PlatformAdminInvitationEntity>();

    public DbSet<PlatformAdminSignInChallengeEntity> PlatformAdminSignInChallenges => Set<PlatformAdminSignInChallengeEntity>();

    public DbSet<PlatformPersonEntity> PlatformPersons => Set<PlatformPersonEntity>();

    public DbSet<PlatformPersonAccessTokenEntity> PlatformPersonAccessTokens => Set<PlatformPersonAccessTokenEntity>();

    public DbSet<PlatformPersonRefreshTokenEntity> PlatformPersonRefreshTokens => Set<PlatformPersonRefreshTokenEntity>();

    public DbSet<PlatformPhoneOtpEntity> PlatformPhoneOtps => Set<PlatformPhoneOtpEntity>();

    public DbSet<PlatformReputationSnapshotEntity> PlatformReputationSnapshots => Set<PlatformReputationSnapshotEntity>();

    public DbSet<BranchBookingSettingsEntity> BranchBookingSettings => Set<BranchBookingSettingsEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganizationEntity>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(organization => organization.OrganizationId);
            entity.Property(organization => organization.Slug).HasMaxLength(64).IsRequired();
            entity.Property(organization => organization.Name).HasMaxLength(160).IsRequired();
            entity.Property(organization => organization.ContactEmail).HasMaxLength(256);
            entity.Property(organization => organization.ContactPhone).HasMaxLength(32);
            entity.Property(organization => organization.LegalDetails).HasMaxLength(1024);
            entity.Property(organization => organization.Status).HasMaxLength(32).IsRequired();
            entity.Property(organization => organization.StatusReason).HasMaxLength(512);
            entity.Property(organization => organization.PlanCode).HasMaxLength(64).IsRequired();
            entity.Property(organization => organization.SubscriptionStatus).HasMaxLength(32).IsRequired();
            entity.Property(organization => organization.LimitsJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(organization => organization.Slug).IsUnique();
            entity.HasIndex(organization => organization.Status);
        });

        modelBuilder.Entity<OrganizationLoyaltySettingsEntity>(entity =>
        {
            entity.ToTable("organization_loyalty_settings");
            entity.HasKey(settings => settings.OrganizationId);
        });

        modelBuilder.Entity<OrganizationReferralSettingsEntity>(entity =>
        {
            entity.ToTable("organization_referral_settings");
            entity.HasKey(settings => settings.OrganizationId);
        });

        modelBuilder.Entity<PlayerReferralEntity>(entity =>
        {
            entity.ToTable("player_referrals");
            // Ключ по приглашённому: код называют один раз, и второй записи о нём быть не может.
            entity.HasKey(referral => referral.InviteePlayerAccountId);
            entity.Property(referral => referral.CurrencyCode).HasMaxLength(3);
            entity.HasIndex(referral => new { referral.OrganizationId, referral.ReferrerPlayerAccountId });
        });

        modelBuilder.Entity<NewsItemEntity>(entity =>
        {
            entity.ToTable("news_items");
            entity.HasKey(news => news.Id);
            entity.Property(news => news.Title).HasMaxLength(200).IsRequired();
            entity.Property(news => news.Body).HasMaxLength(4000).IsRequired();
            entity.Property(news => news.ImageUrl).HasMaxLength(2048);
            entity.HasIndex(news => news.OrganizationId);
        });

        modelBuilder.Entity<ClubReviewEntity>(entity =>
        {
            entity.ToTable("club_reviews");
            entity.HasKey(review => review.ReviewId);
            entity.Property(review => review.Comment).HasMaxLength(1000);
            // Один визит — один отзыв. Уникальность на сессии, а не проверка перед вставкой:
            // два быстрых нажатия «Отправить» иначе оставят два отзыва об одном вечере.
            entity.HasIndex(review => review.SessionId).IsUnique();
            entity.HasIndex(review => new { review.OrganizationId, review.CreatedAtUtc });
            entity.HasIndex(review => review.PlayerAccountId);
        });

        modelBuilder.Entity<UploadedMediaEntity>(entity =>
        {
            entity.ToTable("uploaded_media");
            entity.HasKey(media => media.MediaId);
            entity.Property(media => media.Purpose).HasMaxLength(64).IsRequired();
            entity.Property(media => media.ObjectKey).HasMaxLength(512).IsRequired();
            entity.Property(media => media.ContentType).HasMaxLength(128).IsRequired();
            entity.Property(media => media.PublicUrl).HasMaxLength(2048).IsRequired();
            entity.HasIndex(media => new { media.OrganizationId, media.BranchId, media.Purpose });
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

        modelBuilder.Entity<OrganizationSubscriptionEntity>(entity =>
        {
            entity.ToTable("tenant_subscriptions");
            entity.HasKey(subscription => subscription.OrganizationSubscriptionId);
            entity.Property(subscription => subscription.OrganizationSubscriptionId)
                .HasColumnName("TenantSubscriptionId");
            entity.Property(subscription => subscription.PlanCode).HasMaxLength(64).IsRequired();
            entity.Property(subscription => subscription.Status).HasMaxLength(32).IsRequired();
            entity.Property(subscription => subscription.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(subscription => subscription.BillingInterval).HasMaxLength(16).IsRequired();
            entity.Property(subscription => subscription.DiscountReason).HasMaxLength(512);
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
            entity.Property(branch => branch.Description).HasMaxLength(500);
            entity.Property(branch => branch.Address).HasMaxLength(300);
            entity.Property(branch => branch.Phone).HasMaxLength(40);
            entity.Property(branch => branch.Telegram).HasMaxLength(120);
            entity.Property(branch => branch.Website).HasMaxLength(300);
            entity.Property(branch => branch.Instagram).HasMaxLength(120);
            entity.Property(branch => branch.LogoUrl).HasMaxLength(600);
            entity.Property(branch => branch.CoverImageUrl).HasMaxLength(600);
            entity.Property(branch => branch.WorkingHoursJson).HasColumnType("jsonb");
            entity.Property(branch => branch.PhotosJson).HasColumnType("jsonb");
            entity.Property(branch => branch.RequireManualDeviceApproval).HasDefaultValue(false);
            entity.Property(branch => branch.PreferredLocale).HasMaxLength(8).HasDefaultValue("ru").IsRequired();
            entity.Property(branch => branch.PreferredTimeZone).HasMaxLength(64).HasDefaultValue("Asia/Dushanbe").IsRequired();
            entity.Property(branch => branch.OrganizationAdminMaintenanceWindowStart)
                .HasColumnType("time without time zone").HasDefaultValue(new TimeOnly(4, 0));
            entity.Property(branch => branch.OrganizationAdminMaintenanceWindowEnd)
                .HasColumnType("time without time zone").HasDefaultValue(new TimeOnly(5, 0));
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
            entity.Property(staffUser => staffUser.Email).HasMaxLength(320);
            entity.Property(staffUser => staffUser.PasswordHash).IsRequired();
            entity.HasIndex(staffUser => new { staffUser.OrganizationId, staffUser.NormalizedUserName }).IsUnique();
            entity.Property(staffUser => staffUser.Phone).HasMaxLength(20);
            entity.Property(staffUser => staffUser.NormalizedPhone).HasMaxLength(20);
            // Phone is a GLOBAL login id (unlike username, which is per-org): a verified, active phone
            // must map to exactly one staff. Partial unique index so unverified/old rows don't collide.
            entity.HasIndex(staffUser => staffUser.NormalizedPhone)
                .IsUnique()
                .HasFilter("\"NormalizedPhone\" IS NOT NULL AND \"PhoneVerifiedAtUtc\" IS NOT NULL AND \"IsActive\"");
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
            entity.Property(zone => zone.HardwareSummary).HasMaxLength(200);
            entity.HasKey(zone => zone.ZoneId);
            entity.Property(zone => zone.Name).HasMaxLength(120).IsRequired();
            entity.Property(zone => zone.Color).HasMaxLength(32);
            entity.Property(zone => zone.ZoneType).HasMaxLength(32);
            entity.HasIndex(zone => new { zone.OrganizationId, zone.BranchId, zone.SortOrder });
        });

        modelBuilder.Entity<SeatEntity>(entity =>
        {
            entity.ToTable("seats");
            entity.HasKey(seat => seat.SeatId);
            entity.Property(seat => seat.Name).HasMaxLength(80).IsRequired();
            entity.Property(seat => seat.SeatType).HasMaxLength(32).HasDefaultValue("pc").IsRequired();
            entity.Property(seat => seat.Rotation).HasDefaultValue(0);
            entity.HasIndex(seat => new { seat.OrganizationId, seat.BranchId, seat.ZoneId, seat.SortOrder });
        });

        modelBuilder.Entity<WallEntity>(entity =>
        {
            entity.ToTable("walls");
            entity.HasKey(wall => wall.WallId);
            entity.HasIndex(wall => new { wall.OrganizationId, wall.BranchId });
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
            entity.Property(session => session.BillingMode).HasMaxLength(32).IsRequired().HasDefaultValue(string.Empty);
            entity.Property(session => session.State).HasMaxLength(32).IsRequired();
            entity.Property(session => session.Version).IsConcurrencyToken();
            // One active-ish session per seat, enforced by the database so two concurrent starts
            // cannot both win the read-then-write occupancy check (the loser hits this and gets a 409).
            entity.HasIndex(session => session.SeatId)
                .IsUnique()
                .HasFilter("\"State\" IN ('active', 'paused', 'ending')")
                .HasDatabaseName("ix_sessions_seat_active_unique");
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
            entity.Property(player => player.Email).HasMaxLength(320);
            entity.Property(player => player.PreferredLocale).HasMaxLength(16);
            entity.Property(player => player.ReferralCode).HasMaxLength(16);
            entity.HasIndex(player => new { player.OrganizationId, player.HomeBranchId });
            // Код уникален внутри клуба, а не глобально: игрок называет его вслух, и чем короче
            // код, тем важнее не требовать уникальности через всю платформу.
            entity.HasIndex(player => new { player.OrganizationId, player.ReferralCode }).IsUnique();
            entity.HasIndex(player => player.PlatformPersonId);
            // У человека в одном клубе ровно один счёт — и это защита на уровне базы, а не на
            // уровне надежды: при гонке вторая вставка падает на индексе, а код перечитывает
            // существующую связь. Счета без личности под ограничение не попадают: гостей без
            // телефона в одном клубе бывает сколько угодно.
            entity.HasIndex(player => new { player.PlatformPersonId, player.OrganizationId })
                .IsUnique()
                .HasFilter("\"PlatformPersonId\" IS NOT NULL");
        });

        modelBuilder.Entity<PlayerCredentialEntity>(entity =>
        {
            entity.ToTable("player_credentials");
            entity.HasKey(credential => credential.PlayerCredentialId);
            entity.Property(credential => credential.PasswordHash).HasMaxLength(512);
            entity.HasIndex(credential => credential.PlayerAccountId).IsUnique();
            entity.HasIndex(credential => new { credential.OrganizationId, credential.PlayerAccountId });
        });

        modelBuilder.Entity<PlayerAccessTokenEntity>(entity =>
        {
            entity.ToTable("player_access_tokens");
            entity.HasKey(accessToken => accessToken.PlayerAccessTokenId);
            entity.Property(accessToken => accessToken.TokenHash).IsRequired();
            entity.HasIndex(accessToken => accessToken.TokenHash);
            entity.HasIndex(accessToken => new { accessToken.PlayerAccountId, accessToken.ExpiresAtUtc });
        });

        modelBuilder.Entity<PlayerRefreshTokenEntity>(entity =>
        {
            entity.ToTable("player_refresh_tokens");
            entity.HasKey(refreshToken => refreshToken.PlayerRefreshTokenId);
            entity.Property(refreshToken => refreshToken.TokenHash).IsRequired();
            entity.HasIndex(refreshToken => refreshToken.TokenHash);
            entity.HasIndex(refreshToken => new { refreshToken.PlayerAccountId, refreshToken.ExpiresAtUtc });
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
            entity.Property(tariff => tariff.AppliesOnDaysMask).HasDefaultValue(0);
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

        modelBuilder.Entity<ProductBarcodeEntity>(entity =>
        {
            entity.ToTable("product_barcodes");
            entity.HasKey(barcode => barcode.BarcodeId);
            entity.Property(barcode => barcode.Code).HasMaxLength(64).IsRequired();
            entity.HasIndex(barcode => new { barcode.OrganizationId, barcode.BranchId, barcode.Code }).IsUnique();
            entity.HasIndex(barcode => new { barcode.OrganizationId, barcode.BranchId, barcode.ProductId });
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

        modelBuilder.Entity<ShopOrderEntity>(entity =>
        {
            entity.ToTable("shop_orders");
            entity.HasKey(order => order.ShopOrderId);
            entity.Property(order => order.Status).HasMaxLength(32).IsRequired();
            entity.Property(order => order.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(order => order.CancelReason).HasMaxLength(240);
            entity.Property(order => order.Version).IsConcurrencyToken();
            entity.HasIndex(order => new { order.BranchId, order.Status });
            entity.HasIndex(order => new { order.PlayerAccountId, order.PlacedAtUtc });
            entity.HasOne<PosSaleEntity>()
                .WithOne()
                .HasForeignKey<ShopOrderEntity>(order => order.PosSaleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(order => order.PosSaleId)
                .IsUnique()
                .HasFilter("\"PosSaleId\" IS NOT NULL");
        });

        modelBuilder.Entity<ShopOrderLineEntity>(entity =>
        {
            entity.ToTable("shop_order_lines");
            entity.HasKey(line => line.ShopOrderLineId);
            entity.Property(line => line.NameSnapshot).HasMaxLength(160).IsRequired();
            entity.HasIndex(line => line.ShopOrderId);
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
            entity.HasIndex(sale => sale.SessionId);
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
            entity.HasIndex(payment => new { payment.SessionId, payment.CreatedAtUtc });
            entity.HasIndex(payment => payment.LedgerEntryId).IsUnique();
            entity.HasOne<LedgerEntryEntity>()
                .WithMany()
                .HasForeignKey(payment => payment.LedgerEntryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReceiptEntity>(entity =>
        {
            entity.ToTable("receipts");
            entity.HasKey(receipt => receipt.ReceiptId);
            entity.Property(receipt => receipt.ReceiptNumber).HasMaxLength(32).IsRequired();
            entity.Property(receipt => receipt.ReceiptType).HasMaxLength(32).IsRequired();
            entity.Property(receipt => receipt.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(receipt => receipt.Locale).HasMaxLength(8).HasDefaultValue("ru").IsRequired();
            entity.HasIndex(receipt => new
            {
                receipt.OrganizationId,
                receipt.BranchId,
                receipt.ReceiptNumber
            }).IsUnique();
            entity.HasIndex(receipt => receipt.PosSaleId);
            entity.HasIndex(receipt => receipt.SessionId);
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
                package.Component,
                package.Version,
                package.Channel
            }).IsUnique();
            entity.HasIndex(package => new { package.State, package.CreatedAtUtc });
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
            entity.HasIndex(target => new { target.UpdateRolloutId, target.OrganizationId })
                .IsUnique()
                .HasFilter("\"OrganizationId\" IS NOT NULL");
            entity.HasIndex(target => new { target.UpdateRolloutId, target.BranchId })
                .IsUnique()
                .HasFilter("\"BranchId\" IS NOT NULL");
            entity.HasIndex(target => new { target.UpdateRolloutId, target.DeviceId })
                .IsUnique()
                .HasFilter("\"DeviceId\" IS NOT NULL");
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
            entity.Property(reservation => reservation.Version).IsConcurrencyToken();
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
            entity.HasIndex(reservation => reservation.StartedSessionId)
                .IsUnique()
                .HasFilter("\"StartedSessionId\" IS NOT NULL");
            entity.HasOne<SessionEntity>()
                .WithOne()
                .HasForeignKey<ReservationEntity>(reservation => reservation.StartedSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentIntentEntity>(entity =>
        {
            entity.ToTable("payment_intents");
            entity.HasKey(intent => intent.PaymentIntentId);
            entity.Property(intent => intent.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(intent => intent.Purpose).HasMaxLength(32).IsRequired();
            entity.Property(intent => intent.State).HasMaxLength(32).IsRequired();
            entity.Property(intent => intent.Method).HasMaxLength(32).IsRequired();
            entity.Property(intent => intent.GatewayPaymentId).HasMaxLength(128);
            entity.Property(intent => intent.GatewayComment).HasMaxLength(64);
            entity.Property(intent => intent.GatewayPayUrl).HasMaxLength(1024);
            entity.HasIndex(intent => intent.PlayerAccountId);
            entity.HasIndex(intent => new { intent.BranchId, intent.State });
        });

        modelBuilder.Entity<EskhataMerchantConfigEntity>(entity =>
        {
            entity.ToTable("eskhata_merchant_configs");
            entity.HasKey(config => config.EskhataMerchantConfigId);
            entity.Property(config => config.BaseUrl).HasMaxLength(512).IsRequired();
            entity.Property(config => config.CompanyId).HasMaxLength(256).IsRequired();
            entity.Property(config => config.HashKeyEncrypted).HasMaxLength(1024).IsRequired();
            entity.Property(config => config.Status).HasMaxLength(32).IsRequired();
            entity.HasIndex(config => new { config.OrganizationId, config.BranchId }).IsUnique();
        });

        modelBuilder.Entity<DcPayLinkConfigEntity>(entity =>
        {
            entity.ToTable("dc_paylink_configs");
            entity.HasKey(e => e.DcPayLinkConfigId);
            entity.Property(e => e.ReceivingCardEncrypted).IsRequired();
            entity.Property(e => e.CardLast4).HasMaxLength(4).IsRequired();
            entity.Property(e => e.CommentTemplate).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => new { e.OrganizationId, e.BranchId }).IsUnique();
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
            entity.Property(admin => admin.RecoveryCodeHashesJson).HasDefaultValue("[]").IsRequired();
            entity.Property(admin => admin.FailedTwoFactorAttempts).HasDefaultValue(0);
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

        modelBuilder.Entity<PlatformSupportAccessGrantEntity>(entity =>
        {
            entity.ToTable("platform_support_access_grants");
            entity.HasKey(grant => grant.GrantId);
            entity.Property(grant => grant.Reason).HasMaxLength(500).IsRequired();
            entity.HasIndex(grant => new { grant.PlatformAdminUserId, grant.ExpiresAtUtc });
            entity.HasIndex(grant => new { grant.OrganizationId, grant.ExpiresAtUtc });
            entity.HasIndex(grant => grant.TicketHash).IsUnique().HasFilter("\"TicketHash\" IS NOT NULL");
            entity.HasIndex(grant => grant.SessionTokenHash).IsUnique().HasFilter("\"SessionTokenHash\" IS NOT NULL");
        });

        modelBuilder.Entity<PlatformIncidentEntity>(entity =>
        {
            entity.ToTable("platform_incidents");
            entity.HasKey(incident => incident.PlatformIncidentId);
            entity.Property(incident => incident.Kind).HasMaxLength(64).IsRequired();
            entity.Property(incident => incident.DedupKey).HasMaxLength(200).IsRequired();
            entity.Property(incident => incident.Severity).HasMaxLength(16).IsRequired();
            entity.Property(incident => incident.DetailsJson).HasMaxLength(1000).IsRequired();
            // Инвариант «один ОТКРЫТЫЙ инцидент на ключ» держит база: без частичного индекса
            // два тика сторожа, наложившись, завели бы две строки и два письма про одно и то же.
            entity.HasIndex(incident => incident.DedupKey)
                .IsUnique()
                .HasFilter("\"ResolvedAtUtc\" IS NULL");
            entity.HasIndex(incident => incident.OpenedAtUtc);
        });

        modelBuilder.Entity<SubscriptionDailySnapshotEntity>(entity =>
        {
            entity.ToTable("subscription_daily_snapshots");
            entity.HasKey(snapshot => snapshot.SubscriptionDailySnapshotId);
            entity.Property(snapshot => snapshot.Status).HasMaxLength(32).IsRequired();
            entity.Property(snapshot => snapshot.PlanCode).HasMaxLength(64).IsRequired();
            entity.Property(snapshot => snapshot.CurrencyCode).HasMaxLength(3).IsRequired();
            // Идемпотентность суточного задания держит база: повторный запуск за те же сутки
            // не заведёт вторую строку, а двойной запуск и повтор после падения — норма.
            entity.HasIndex(snapshot => new { snapshot.OrganizationId, snapshot.SnapshotDate })
                .IsUnique()
                .HasDatabaseName("IX_subscription_daily_snapshots_Organization_Date");
            entity.HasIndex(snapshot => snapshot.SnapshotDate);
        });

        modelBuilder.Entity<BranchDailySnapshotEntity>(entity =>
        {
            entity.ToTable("branch_daily_snapshots");
            entity.HasKey(snapshot => snapshot.BranchDailySnapshotId);
            entity.Property(snapshot => snapshot.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.HasIndex(snapshot => new { snapshot.BranchId, snapshot.SnapshotDate })
                .IsUnique()
                .HasDatabaseName("IX_branch_daily_snapshots_Branch_Date");
            entity.HasIndex(snapshot => new { snapshot.OrganizationId, snapshot.SnapshotDate });
        });

        modelBuilder.Entity<PlatformFeatureEntity>(entity =>
        {
            entity.ToTable("platform_features");
            entity.HasKey(feature => feature.FeatureKey);
            entity.Property(feature => feature.FeatureKey).HasMaxLength(64);
            entity.Property(feature => feature.Name).HasMaxLength(128).IsRequired();
            entity.Property(feature => feature.Description).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<PlatformRoleEntity>(entity =>
        {
            entity.ToTable("platform_roles");
            entity.HasKey(role => role.RoleName);
            entity.Property(role => role.RoleName).HasMaxLength(64);
            entity.Property(role => role.DisplayName).HasMaxLength(128).IsRequired();
            entity.Property(role => role.Description).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<PlatformRolePermissionEntity>(entity =>
        {
            entity.ToTable("platform_role_permissions");
            entity.HasKey(rolePermission => rolePermission.PlatformRolePermissionId);
            entity.Property(rolePermission => rolePermission.RoleName).HasMaxLength(64).IsRequired();
            entity.Property(rolePermission => rolePermission.PermissionName).HasMaxLength(128).IsRequired();
            entity.HasIndex(rolePermission => new { rolePermission.RoleName, rolePermission.PermissionName })
                .IsUnique()
                .HasDatabaseName("IX_platform_role_permissions_Role_Permission");
            entity.HasIndex(rolePermission => rolePermission.RoleName);
        });

        modelBuilder.Entity<PlatformAnnouncementEntity>(entity =>
        {
            entity.ToTable("platform_announcements");
            entity.HasKey(announcement => announcement.PlatformAnnouncementId);
            entity.Property(announcement => announcement.Title).HasMaxLength(200).IsRequired();
            entity.Property(announcement => announcement.Body).HasMaxLength(4000).IsRequired();
            entity.Property(announcement => announcement.Severity).HasMaxLength(16).IsRequired();
            entity.Property(announcement => announcement.AudienceKind).HasMaxLength(16).IsRequired();
            entity.Property(announcement => announcement.AudiencePlanCodesJson).IsRequired();
            entity.Property(announcement => announcement.AudienceOrganizationIdsJson).IsRequired();
            entity.Property(announcement => announcement.Status).HasMaxLength(16).IsRequired();
            // Выдача клубу всегда фильтрует по статусу и окну показа — индекс под этот путь.
            entity.HasIndex(announcement => new { announcement.Status, announcement.ShowUntilUtc });
        });

        modelBuilder.Entity<AnnouncementReadEntity>(entity =>
        {
            entity.ToTable("announcement_reads");
            entity.HasKey(read => new { read.PlatformAnnouncementId, read.StaffUserId });
            entity.HasIndex(read => read.StaffUserId);
        });

        modelBuilder.Entity<PlanFeatureEntity>(entity =>
        {
            entity.ToTable("plan_features");
            entity.HasKey(planFeature => planFeature.PlanFeatureId);
            entity.Property(planFeature => planFeature.PlanCode).HasMaxLength(64).IsRequired();
            entity.Property(planFeature => planFeature.FeatureKey).HasMaxLength(64).IsRequired();
            entity.HasIndex(planFeature => new { planFeature.PlanCode, planFeature.FeatureKey })
                .IsUnique()
                .HasDatabaseName("IX_plan_features_Plan_Feature");
        });

        modelBuilder.Entity<OrganizationFeatureOverrideEntity>(entity =>
        {
            entity.ToTable("organization_feature_overrides");
            entity.HasKey(featureOverride => featureOverride.OrganizationFeatureOverrideId);
            entity.Property(featureOverride => featureOverride.FeatureKey).HasMaxLength(64).IsRequired();
            entity.Property(featureOverride => featureOverride.Reason).HasMaxLength(500).IsRequired();
            entity.HasIndex(featureOverride => new { featureOverride.OrganizationId, featureOverride.FeatureKey })
                .IsUnique()
                .HasDatabaseName("IX_organization_feature_overrides_Organization_Feature");
        });

        modelBuilder.Entity<PlatformJobRunEntity>(entity =>
        {
            entity.ToTable("platform_job_runs");
            entity.HasKey(run => run.PlatformJobRunId);
            entity.Property(run => run.JobName).HasMaxLength(64).IsRequired();
            entity.Property(run => run.Outcome).HasMaxLength(16).IsRequired();
            entity.Property(run => run.Error).HasMaxLength(2000);
            entity.HasIndex(run => new { run.JobName, run.StartedAtUtc });
        });

        modelBuilder.Entity<OrganizationOwnerInviteEntity>(entity =>
        {
            entity.ToTable("owner_invites");
            entity.HasKey(invite => invite.OrganizationOwnerInviteId);
            entity.Property(invite => invite.OrganizationOwnerInviteId).HasColumnName("OwnerInviteId");
            entity.Property(invite => invite.Code).HasMaxLength(64).IsRequired();
            entity.Property(invite => invite.NormalizedCode).HasMaxLength(64).IsRequired();
            entity.Property(invite => invite.Status).HasMaxLength(32).IsRequired();
            entity.Property(invite => invite.OwnerUserName).HasMaxLength(256);
            entity.Property(invite => invite.OwnerDisplayName).HasMaxLength(160);
            entity.Property(invite => invite.OwnerEmail).HasMaxLength(320);
            entity.Property(invite => invite.RevokedReason).HasMaxLength(512);
            entity.HasIndex(invite => invite.NormalizedCode).IsUnique();
            entity.HasIndex(invite => new { invite.OrganizationId, invite.BranchId, invite.Status });
            entity.HasIndex(invite => invite.ExpiresAtUtc);
        });

        modelBuilder.Entity<OrganizationSupportNoteEntity>(entity =>
        {
            entity.ToTable("tenant_support_notes");
            entity.HasKey(note => note.OrganizationSupportNoteId);
            entity.Property(note => note.OrganizationSupportNoteId)
                .HasColumnName("TenantSupportNoteId");
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

        modelBuilder.Entity<OutboxMessageEntity>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(row => row.OutboxMessageId);
            entity.Property(row => row.Type).HasMaxLength(64).IsRequired();
            entity.Property(row => row.PayloadJson).IsRequired();
            entity.Property(row => row.Status).HasMaxLength(16).IsRequired();
            entity.Property(row => row.IdempotencyKey).HasMaxLength(256).IsRequired();
            entity.Property(row => row.LastError).HasMaxLength(2000);
            entity.HasIndex(row => row.IdempotencyKey).IsUnique();
            entity.HasIndex(row => new { row.Status, row.AvailableAtUtc });
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
            entity.Property(row => row.TokensJson).HasMaxLength(4000);
            entity.Property(row => row.Status).HasMaxLength(16).IsRequired();
            entity.Property(row => row.LastError).HasMaxLength(2000);
            entity.HasIndex(row => row.IdempotencyKey).IsUnique();
            entity.HasIndex(row => new { row.Status, row.NextAttemptUtc });
            entity.HasMany(row => row.Attachments)
                .WithOne()
                .HasForeignKey(attachment => attachment.NotificationOutboxId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotificationOutboxAttachmentEntity>(entity =>
        {
            entity.ToTable("notification_outbox_attachments");
            entity.HasKey(attachment => attachment.NotificationOutboxAttachmentId);
            entity.Property(attachment => attachment.FileName).HasMaxLength(256).IsRequired();
            entity.Property(attachment => attachment.ContentType).HasMaxLength(128).IsRequired();
            entity.Property(attachment => attachment.Content).IsRequired();
            entity.HasIndex(attachment => attachment.NotificationOutboxId);
        });

        modelBuilder.Entity<NotificationPreferenceEntity>(entity =>
        {
            entity.ToTable("notification_preferences");
            entity.HasKey(preference => preference.NotificationPreferenceId);
            entity.Property(preference => preference.Category).HasMaxLength(16).IsRequired();
            entity.Property(preference => preference.Channel).HasMaxLength(16).IsRequired();
            entity.HasIndex(preference => new { preference.StaffUserId, preference.Category, preference.Channel });
            entity.HasIndex(preference => new { preference.PlayerAccountId, preference.Category, preference.Channel });
        });

        modelBuilder.Entity<PlayerDeviceEntity>(entity =>
        {
            entity.ToTable("player_devices");
            entity.HasKey(device => device.PlayerDeviceId);
            entity.Property(device => device.PushToken).HasMaxLength(512).IsRequired();
            entity.Property(device => device.Platform).HasMaxLength(16).IsRequired();
            entity.Property(device => device.Locale).HasMaxLength(16);
            // Токен уникален: один телефон — одна строка, даже если на нём сменился игрок.
            entity.HasIndex(device => device.PushToken).IsUnique();
            entity.HasIndex(device => device.PlayerAccountId);
        });

        modelBuilder.Entity<ReportScheduleEntity>(entity =>
        {
            entity.ToTable("report_schedules");
            entity.HasKey(schedule => schedule.ReportScheduleId);
            entity.Property(schedule => schedule.ReportType).HasMaxLength(32).IsRequired();
            entity.Property(schedule => schedule.Frequency).HasMaxLength(16).IsRequired();
            entity.HasIndex(schedule => new { schedule.IsActive, schedule.NextRunUtc });
            entity.HasIndex(schedule => new { schedule.OrganizationId, schedule.BranchId });
        });

        modelBuilder.Entity<StaffMoneyCapEntity>(entity =>
        {
            entity.ToTable("staff_money_caps");
            entity.HasKey(cap => cap.StaffMoneyCapId);
            entity.Property(cap => cap.RoleName).HasMaxLength(64).IsRequired();
            entity.Property(cap => cap.ActionScope).HasMaxLength(32).IsRequired();
            entity.HasIndex(cap => new { cap.BranchId, cap.RoleName, cap.ActionScope }).IsUnique();
        });

        modelBuilder.Entity<MoneyActionRequestEntity>(entity =>
        {
            entity.ToTable("money_action_requests");
            entity.HasKey(request => request.MoneyActionRequestId);
            entity.Property(request => request.ActionType).HasMaxLength(32).IsRequired();
            entity.Property(request => request.State).HasMaxLength(16).IsRequired();
            entity.Property(request => request.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(request => request.Reason).HasMaxLength(512).IsRequired();
            entity.HasIndex(request => new { request.BranchId, request.State });
            entity.HasIndex(request => new { request.OrganizationId, request.BranchId, request.CreatedAtUtc });
        });

        modelBuilder.Entity<PasswordResetTokenEntity>(entity =>
        {
            entity.ToTable("password_reset_tokens");
            entity.HasKey(token => token.PasswordResetTokenId);
            entity.Property(token => token.TokenHash).IsRequired();
            entity.HasIndex(token => token.TokenHash);
            entity.HasIndex(token => new { token.StaffUserId, token.ExpiresAtUtc });
        });

        modelBuilder.Entity<StaffInviteEntity>(entity =>
        {
            entity.ToTable("staff_invites");
            entity.HasKey(invite => invite.StaffInviteId);
            entity.Property(invite => invite.UserName).HasMaxLength(256).IsRequired();
            entity.Property(invite => invite.NormalizedUserName).HasMaxLength(256).IsRequired();
            entity.Property(invite => invite.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(invite => invite.Email).HasMaxLength(320).IsRequired();
            entity.Property(invite => invite.RoleNamesCsv).HasMaxLength(512).IsRequired();
            entity.Property(invite => invite.TokenHash).IsRequired();
            entity.HasIndex(invite => invite.TokenHash);
            entity.HasIndex(invite => new { invite.OrganizationId, invite.NormalizedUserName });
        });

        modelBuilder.Entity<StaffPhoneOtpEntity>(entity =>
        {
            entity.ToTable("staff_phone_otps");
            entity.HasKey(otp => otp.StaffPhoneOtpId);
            entity.Property(otp => otp.Phone).HasMaxLength(20).IsRequired();
            entity.Property(otp => otp.CodeHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(otp => new { otp.StaffUserId, otp.Purpose, otp.CreatedAtUtc });
        });

        modelBuilder.Entity<PlayerPhoneOtpEntity>(entity =>
        {
            entity.ToTable("player_phone_otps");
            entity.HasKey(otp => otp.PlayerPhoneOtpId);
            entity.Property(otp => otp.Phone).HasMaxLength(20).IsRequired();
            entity.Property(otp => otp.CodeHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(otp => new { otp.PlayerAccountId, otp.Purpose, otp.CreatedAtUtc });
        });

        modelBuilder.Entity<PlatformAdminInvitationEntity>(entity =>
        {
            entity.ToTable("platform_admin_invitations");
            entity.HasKey(invitation => invitation.InvitationId);
            entity.Property(invitation => invitation.CodeHash).IsRequired();
            entity.Property(invitation => invitation.Role).HasMaxLength(64).IsRequired();
            entity.Property(invitation => invitation.Status).HasMaxLength(32).IsRequired();
            entity.HasIndex(invitation => invitation.CodeHash).IsUnique();
            entity.HasIndex(invitation => new { invitation.Status, invitation.ExpiresAtUtc });
        });

        modelBuilder.Entity<PlatformAdminSignInChallengeEntity>(entity =>
        {
            entity.ToTable("platform_admin_sign_in_challenges");
            entity.HasKey(challenge => challenge.ChallengeId);
            entity.Property(challenge => challenge.TokenHash).IsRequired();
            entity.HasIndex(challenge => challenge.TokenHash).IsUnique();
            entity.HasIndex(challenge => new { challenge.PlatformAdminUserId, challenge.ExpiresAtUtc });
        });

        modelBuilder.Entity<PlatformPersonEntity>(entity =>
        {
            entity.ToTable("platform_persons");
            entity.HasKey(person => person.PlatformPersonId);
            entity.Property(person => person.PhoneNumber).HasMaxLength(32).IsRequired();
            entity.Property(person => person.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(person => person.PreferredLocale).HasMaxLength(16);
            entity.Property(person => person.PinHash).HasMaxLength(512);
            entity.Property(person => person.NetworkBanReason).HasMaxLength(500);
            // Номер — это и есть личность: второй записи на тот же номер быть не может.
            entity.HasIndex(person => person.PhoneNumber).IsUnique();
        });

        modelBuilder.Entity<PlatformPersonAccessTokenEntity>(entity =>
        {
            entity.ToTable("platform_person_access_tokens");
            entity.HasKey(accessToken => accessToken.PlatformPersonAccessTokenId);
            entity.Property(accessToken => accessToken.TokenHash).IsRequired();
            entity.HasIndex(accessToken => accessToken.TokenHash);
            entity.HasIndex(accessToken => new { accessToken.PlatformPersonId, accessToken.ExpiresAtUtc });
        });

        modelBuilder.Entity<PlatformPersonRefreshTokenEntity>(entity =>
        {
            entity.ToTable("platform_person_refresh_tokens");
            entity.HasKey(refreshToken => refreshToken.PlatformPersonRefreshTokenId);
            entity.Property(refreshToken => refreshToken.TokenHash).IsRequired();
            entity.HasIndex(refreshToken => refreshToken.TokenHash);
            entity.HasIndex(refreshToken => new { refreshToken.PlatformPersonId, refreshToken.ExpiresAtUtc });
        });

        modelBuilder.Entity<PlatformPhoneOtpEntity>(entity =>
        {
            entity.ToTable("platform_phone_otps");
            entity.HasKey(otp => otp.PlatformPhoneOtpId);
            entity.Property(otp => otp.Phone).HasMaxLength(32).IsRequired();
            entity.Property(otp => otp.CodeHash).HasMaxLength(64).IsRequired();
            // Счётчик отправок ключуется номером, а не счётом: у незнакомого номера счёта ещё нет.
            entity.HasIndex(otp => new { otp.Phone, otp.Purpose, otp.CreatedAtUtc });
        });

        modelBuilder.Entity<PlatformReputationSnapshotEntity>(entity =>
        {
            entity.ToTable("platform_reputation_snapshots");
            entity.HasKey(snapshot => snapshot.PlatformPersonId);
        });

        modelBuilder.Entity<BranchBookingSettingsEntity>(entity =>
        {
            entity.ToTable("branch_booking_settings");
            entity.HasKey(settings => settings.BranchId);
            entity.Property(settings => settings.AcceptanceMode).HasMaxLength(16).IsRequired();
            entity.HasIndex(settings => settings.OrganizationId);
        });
    }
}
