using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Data;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public DbSet<DeviceEntity> Devices => Set<DeviceEntity>();

    public DbSet<DeviceCredentialEntity> DeviceCredentials => Set<DeviceCredentialEntity>();

    public DbSet<DeviceEnrollmentCodeEntity> DeviceEnrollmentCodes => Set<DeviceEnrollmentCodeEntity>();

    public DbSet<DeviceCommandEntity> DeviceCommands => Set<DeviceCommandEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
