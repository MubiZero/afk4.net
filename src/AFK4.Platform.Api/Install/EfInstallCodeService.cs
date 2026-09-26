using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Install;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Install;

public sealed class EfInstallCodeService(PlatformDbContext dbContext, TimeProvider timeProvider) : IInstallCodeService
{
    public async Task<InstallOperationResult<InstallCodeDto>> CreateAsync(
        Guid organizationId,
        Guid branchId,
        Guid staffUserId,
        CreateInstallCodeRequest request,
        CancellationToken cancellationToken)
    {
        if (request.LifetimeHours is < InstallCodeLimits.MinLifetimeHours or > InstallCodeLimits.MaxLifetimeHours)
        {
            return InstallOperationResult<InstallCodeDto>.BadRequest(
                $"Lifetime must be between {InstallCodeLimits.MinLifetimeHours} and {InstallCodeLimits.MaxLifetimeHours} hours.",
                organizationId,
                branchId,
                staffUserId);
        }

        if (request.MaxDevices is < InstallCodeLimits.MinDevices or > InstallCodeLimits.MaxDevices)
        {
            return InstallOperationResult<InstallCodeDto>.BadRequest(
                $"Device count must be between {InstallCodeLimits.MinDevices} and {InstallCodeLimits.MaxDevices}.",
                organizationId,
                branchId,
                staffUserId);
        }

        var branchExists = await dbContext.Branches.AnyAsync(
            branch => branch.OrganizationId == organizationId && branch.BranchId == branchId,
            cancellationToken);
        if (!branchExists)
        {
            return InstallOperationResult<InstallCodeDto>.NotFound("Branch was not found.");
        }

        var now = timeProvider.GetUtcNow();
        var code = InstallCodes.Create();
        var entity = new InstallCodeEntity
        {
            InstallCodeId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            CodeHash = InstallCodes.Hash(InstallCodes.Normalize(code)!),
            CreatedAtUtc = now,
            CreatedByStaffUserId = staffUserId,
            ExpiresAtUtc = now.AddHours(request.LifetimeHours),
            MaxDevices = request.MaxDevices,
        };
        dbContext.InstallCodes.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return InstallOperationResult<InstallCodeDto>.Success(ToDto(entity, code), organizationId, branchId, staffUserId);
    }

    public async Task<IReadOnlyList<InstallCodeDto>> ListActiveAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var rows = await dbContext.InstallCodes
            .AsNoTracking()
            .Where(code => code.OrganizationId == organizationId
                && code.BranchId == branchId
                && code.ExpiresAtUtc > now
                && code.UsedDevices < code.MaxDevices)
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(code => code.CreatedAtUtc)
            .Select(code => ToDto(code, plainCode: null))
            .ToArray();
    }

    public async Task<bool> RevokeAsync(
        Guid organizationId,
        Guid branchId,
        Guid installCodeId,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.InstallCodes.SingleOrDefaultAsync(
            code => code.OrganizationId == organizationId
                && code.BranchId == branchId
                && code.InstallCodeId == installCodeId,
            cancellationToken);
        if (entity is null)
        {
            return false;
        }

        dbContext.InstallCodes.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static InstallCodeDto ToDto(InstallCodeEntity entity, string? plainCode) => new(
        entity.InstallCodeId,
        entity.BranchId,
        plainCode,
        entity.CreatedAtUtc,
        entity.ExpiresAtUtc,
        entity.MaxDevices,
        entity.UsedDevices);
}
