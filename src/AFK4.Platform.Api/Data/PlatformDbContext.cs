using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Data;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();

    public DbSet<BranchEntity> Branches => Set<BranchEntity>();

    public DbSet<StaffUserEntity> StaffUsers => Set<StaffUserEntity>();

    public DbSet<StaffRoleAssignmentEntity> StaffRoleAssignments => Set<StaffRoleAssignmentEntity>();

    public DbSet<StaffAccessTokenEntity> StaffAccessTokens => Set<StaffAccessTokenEntity>();

    public DbSet<AuditRecordEntity> AuditRecords => Set<AuditRecordEntity>();

    public DbSet<DeviceEntity> Devices => Set<DeviceEntity>();

    public DbSet<DeviceCredentialEntity> DeviceCredentials => Set<DeviceCredentialEntity>();

    public DbSet<DeviceEnrollmentCodeEntity> DeviceEnrollmentCodes => Set<DeviceEnrollmentCodeEntity>();

    public DbSet<DeviceCommandEntity> DeviceCommands => Set<DeviceCommandEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganizationEntity>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(organization => organization.OrganizationId);
            entity.Property(organization => organization.Name).HasMaxLength(160).IsRequired();
        });

        modelBuilder.Entity<BranchEntity>(entity =>
        {
            entity.ToTable("branches");
            entity.HasKey(branch => branch.BranchId);
            entity.Property(branch => branch.Name).HasMaxLength(160).IsRequired();
            entity.HasIndex(branch => new { branch.OrganizationId, branch.BranchId }).IsUnique();
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
        });

        modelBuilder.Entity<DeviceEntity>(entity =>
        {
            entity.ToTable("devices");
            entity.HasKey(device => device.DeviceId);
            entity.Property(device => device.MachineName).HasMaxLength(128).IsRequired();
            entity.Property(device => device.AgentVersion).HasMaxLength(64).IsRequired();
            entity.Property(device => device.ShellVersion).HasMaxLength(64).IsRequired();
            entity.HasIndex(device => new { device.OrganizationId, device.BranchId });
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
    }
}
