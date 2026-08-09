using System.Data;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Identity;

public sealed record PlatformRoleResult(PlatformRoleDto? Value, string? ErrorCode)
{
    public bool Succeeded => ErrorCode is null;

    public static PlatformRoleResult Ok(PlatformRoleDto value) => new(value, null);

    public static PlatformRoleResult Fail(string errorCode) => new(null, errorCode);
}

/// <summary>
/// Правка состава ролей платформы. Все инварианты — «нельзя выдать больше, чем есть у тебя»,
/// «ключ от платформы нельзя выбросить», «полный администратор не исчезает» — держатся здесь и
/// внутри serializable-транзакции: каждый из них читает состояние других строк перед записью.
/// </summary>
public sealed class PlatformRoleService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    IPlatformRolePermissionResolver rolePermissionResolver)
{
    private const int MaxRoleNameLength = 64;
    private const int MaxDisplayNameLength = 128;
    private const int MaxDescriptionLength = 500;

    public async Task<IReadOnlyList<PlatformRoleDto>> ListAsync(CancellationToken cancellationToken)
    {
        var roles = await dbContext.PlatformRoles
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        var permissions = await dbContext.PlatformRolePermissions
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        var admins = await dbContext.PlatformAdminUsers
            .AsNoTracking()
            .Select(admin => admin.RolesJson)
            .ToArrayAsync(cancellationToken);

        var holders = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var rolesJson in admins)
        {
            foreach (var roleName in OpaquePlatformAdminTokenService.ParseRoles(rolesJson))
            {
                holders[roleName] = holders.GetValueOrDefault(roleName) + 1;
            }
        }

        return roles
            .OrderBy(role => role.RoleName, StringComparer.Ordinal)
            .Select(role => ToDto(role, permissions, holders))
            .ToArray();
    }

    public Task<PlatformRoleResult> CreateAsync(
        Guid actorId,
        CreatePlatformRoleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteInSerializableTransactionAsync(() => CreateCoreAsync(actorId, request, cancellationToken), cancellationToken);

    public Task<PlatformRoleResult> UpdateAsync(
        Guid actorId,
        string roleName,
        UpdatePlatformRoleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteInSerializableTransactionAsync(() => UpdateCoreAsync(actorId, roleName, request, cancellationToken), cancellationToken);

    public Task<PlatformRoleResult> DeleteAsync(string roleName, CancellationToken cancellationToken) =>
        ExecuteInSerializableTransactionAsync(() => DeleteCoreAsync(roleName, cancellationToken), cancellationToken);

    private async Task<PlatformRoleResult> CreateCoreAsync(
        Guid actorId,
        CreatePlatformRoleRequest request,
        CancellationToken cancellationToken)
    {
        var roleName = NormalizeRoleName(request.RoleName ?? string.Empty);
        var displayName = (request.DisplayName ?? string.Empty).Trim();
        if (roleName.Length is 0 or > MaxRoleNameLength
            || displayName.Length is 0 or > MaxDisplayNameLength
            || (request.Description ?? string.Empty).Length > MaxDescriptionLength)
        {
            return PlatformRoleResult.Fail(PlatformRoleErrorCodes.InvalidRole);
        }

        var permissions = Normalize(request.Permissions);
        if (permissions.Count == 0)
        {
            return PlatformRoleResult.Fail(PlatformRoleErrorCodes.InvalidRole);
        }

        var unknownCheck = CheckKnownPermissions(permissions);
        if (unknownCheck is not null)
        {
            return PlatformRoleResult.Fail(unknownCheck);
        }

        var actorCheck = await CheckActorHoldsAsync(actorId, permissions, cancellationToken);
        if (actorCheck is not null)
        {
            return PlatformRoleResult.Fail(actorCheck);
        }

        if (await dbContext.PlatformRoles.AnyAsync(role => role.RoleName == roleName, cancellationToken))
        {
            return PlatformRoleResult.Fail(PlatformRoleErrorCodes.RoleNameTaken);
        }

        var now = timeProvider.GetUtcNow();
        dbContext.PlatformRoles.Add(new PlatformRoleEntity
        {
            RoleName = roleName,
            DisplayName = displayName,
            Description = (request.Description ?? string.Empty).Trim(),
            IsBuiltIn = false,
            // Полный доступ не выдаётся при создании: роль, дающая всё, включая права, которых ещё
            // не существует, должна заводиться осознанно, а не как побочный эффект новой строки.
            GrantsAllPermissions = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        foreach (var permission in permissions)
        {
            dbContext.PlatformRolePermissions.Add(new PlatformRolePermissionEntity
            {
                PlatformRolePermissionId = Guid.NewGuid(),
                RoleName = roleName,
                PermissionName = permission
            });
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (RelationalFailureClassifier.IsUniqueViolation(exception))
        {
            // Проверка «имя занято» выше — это чтение перед записью: два одновременных создания
            // одной роли оба его пройдут. Настоящий инвариант держит первичный ключ, и проигравшая
            // сторона обязана увидеть тот же понятный отказ, а не сырой 500.
            dbContext.ChangeTracker.Clear();
            return PlatformRoleResult.Fail(PlatformRoleErrorCodes.RoleNameTaken);
        }

        return PlatformRoleResult.Ok(await LoadDtoAsync(roleName, cancellationToken));
    }

    private async Task<PlatformRoleResult> UpdateCoreAsync(
        Guid actorId,
        string requestedRoleName,
        UpdatePlatformRoleRequest request,
        CancellationToken cancellationToken)
    {
        var roleName = NormalizeRoleName(requestedRoleName);
        var role = await dbContext.PlatformRoles
            .SingleOrDefaultAsync(candidate => candidate.RoleName == roleName, cancellationToken);
        if (role is null)
        {
            return PlatformRoleResult.Fail(PlatformRoleErrorCodes.InvalidRole);
        }

        var displayName = (request.DisplayName ?? string.Empty).Trim();
        if (displayName.Length is 0 or > MaxDisplayNameLength
            || (request.Description ?? string.Empty).Length > MaxDescriptionLength)
        {
            return PlatformRoleResult.Fail(PlatformRoleErrorCodes.InvalidRole);
        }

        var permissions = Normalize(request.Permissions);

        // Роль с полным доступом описывается флагом, а не списком: перечислять её права — значит
        // зафиксировать сегодняшний набор и потерять завтрашние. Её состав здесь не правится.
        if (!role.GrantsAllPermissions)
        {
            if (permissions.Count == 0)
            {
                return PlatformRoleResult.Fail(PlatformRoleErrorCodes.InvalidRole);
            }

            var unknownCheck = CheckKnownPermissions(permissions);
            if (unknownCheck is not null)
            {
                return PlatformRoleResult.Fail(unknownCheck);
            }

            var current = await dbContext.PlatformRolePermissions
                .Where(rolePermission => rolePermission.RoleName == roleName)
                .ToArrayAsync(cancellationToken);
            var currentNames = new HashSet<string>(
                current.Select(rolePermission => rolePermission.PermissionName),
                StringComparer.OrdinalIgnoreCase);

            // Проверяем только ДОБАВЛЯЕМЫЕ права: отнять у роли право, которого нет у тебя самого,
            // безопасно и иногда необходимо (например, урезать роль, которую ты не носишь).
            var added = permissions.Where(permission => !currentNames.Contains(permission)).ToArray();
            if (added.Length > 0)
            {
                var actorCheck = await CheckActorHoldsAsync(actorId, added, cancellationToken);
                if (actorCheck is not null)
                {
                    return PlatformRoleResult.Fail(actorCheck);
                }
            }

            var losesAdminsManage = currentNames.Contains(PlatformAdminPermissionNames.ManagePlatformAdmins)
                && !permissions.Contains(PlatformAdminPermissionNames.ManagePlatformAdmins);
            if (losesAdminsManage)
            {
                var lockoutCheck = await CheckAdminsManageSurvivesAsync(actorId, roleName, cancellationToken);
                if (lockoutCheck is not null)
                {
                    return PlatformRoleResult.Fail(lockoutCheck);
                }
            }

            dbContext.PlatformRolePermissions.RemoveRange(
                current.Where(rolePermission => !permissions.Contains(rolePermission.PermissionName)));

            foreach (var permission in permissions.Where(permission => !currentNames.Contains(permission)))
            {
                dbContext.PlatformRolePermissions.Add(new PlatformRolePermissionEntity
                {
                    PlatformRolePermissionId = Guid.NewGuid(),
                    RoleName = roleName,
                    PermissionName = permission
                });
            }
        }

        role.DisplayName = displayName;
        role.Description = (request.Description ?? string.Empty).Trim();
        role.UpdatedAtUtc = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);
        return PlatformRoleResult.Ok(await LoadDtoAsync(roleName, cancellationToken));
    }

    private async Task<PlatformRoleResult> DeleteCoreAsync(string requestedRoleName, CancellationToken cancellationToken)
    {
        var roleName = NormalizeRoleName(requestedRoleName);
        var role = await dbContext.PlatformRoles
            .SingleOrDefaultAsync(candidate => candidate.RoleName == roleName, cancellationToken);
        if (role is null)
        {
            return PlatformRoleResult.Fail(PlatformRoleErrorCodes.InvalidRole);
        }

        if (role.IsBuiltIn)
        {
            return PlatformRoleResult.Fail(PlatformRoleErrorCodes.BuiltInRole);
        }

        // Удалить роль, которую кто-то носит, значит молча оставить человека без прав: он ничего
        // не поймёт, а в базе останется ссылка на несуществующую роль.
        var admins = await dbContext.PlatformAdminUsers
            .AsNoTracking()
            .Select(admin => admin.RolesJson)
            .ToArrayAsync(cancellationToken);
        var inUse = admins.Any(rolesJson =>
            OpaquePlatformAdminTokenService.ParseRoles(rolesJson).Contains(roleName));
        if (inUse)
        {
            return PlatformRoleResult.Fail(PlatformRoleErrorCodes.RoleInUse);
        }

        var dto = await LoadDtoAsync(roleName, cancellationToken);
        dbContext.PlatformRolePermissions.RemoveRange(
            dbContext.PlatformRolePermissions.Where(rolePermission => rolePermission.RoleName == roleName));
        dbContext.PlatformRoles.Remove(role);
        await dbContext.SaveChangesAsync(cancellationToken);

        return PlatformRoleResult.Ok(dto);
    }

    private static string? CheckKnownPermissions(IReadOnlyCollection<string> permissions)
    {
        // Право, которого нет в коде, никто не проверяет — выдать его значит нарисовать доступ,
        // которого не существует.
        var known = new HashSet<string>(PlatformAdminPermissionNames.All, StringComparer.OrdinalIgnoreCase);
        return permissions.All(known.Contains) ? null : PlatformRoleErrorCodes.UnknownPermission;
    }

    private async Task<string?> CheckActorHoldsAsync(
        Guid actorId,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken)
    {
        var actorPermissions = await ResolveAdminPermissionsAsync(actorId, cancellationToken);
        return permissions.All(actorPermissions.Contains) ? null : PlatformRoleErrorCodes.PermissionNotHeldByActor;
    }

    /// <summary>
    /// Единственный предохранитель против потери ключа от платформы — запрет снимать
    /// <c>ManagePlatformAdmins</c> со СВОЕЙ роли. Отдельной проверки «это последняя роль, дающая
    /// ключ» здесь намеренно нет, и она была бы недостижимым кодом: чтобы дойти до этого метода,
    /// вызывающий обязан сам иметь <c>ManagePlatformAdmins</c>, а значит какая-то его роль ключ
    /// даёт — и целевая роль последней быть не может. Проверка, которая не может сработать, — это
    /// ложное чувство защиты и тест, который нечем написать честно.
    /// </summary>
    private async Task<string?> CheckAdminsManageSurvivesAsync(
        Guid actorId,
        string roleName,
        CancellationToken cancellationToken)
    {
        var actorRoles = await dbContext.PlatformAdminUsers
            .AsNoTracking()
            .Where(admin => admin.PlatformAdminUserId == actorId)
            .Select(admin => admin.RolesJson)
            .SingleOrDefaultAsync(cancellationToken);

        // Снимать ключ со своей собственной роли — самоблокировка: следующим запросом человек уже
        // не сможет вернуть его обратно.
        return actorRoles is not null && OpaquePlatformAdminTokenService.ParseRoles(actorRoles).Contains(roleName)
            ? PlatformRoleErrorCodes.SelfLockout
            : null;
    }

    private async Task<IReadOnlySet<string>> ResolveAdminPermissionsAsync(Guid adminId, CancellationToken cancellationToken)
    {
        var rolesJson = await dbContext.PlatformAdminUsers
            .AsNoTracking()
            .Where(admin => admin.PlatformAdminUserId == adminId)
            .Select(admin => admin.RolesJson)
            .SingleOrDefaultAsync(cancellationToken);

        if (rolesJson is null)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return await rolePermissionResolver.ResolveAsync(
            OpaquePlatformAdminTokenService.ParseRoles(rolesJson),
            cancellationToken);
    }

    private static readonly IReadOnlyDictionary<string, string> CanonicalPermissions =
        PlatformAdminPermissionNames.All.ToDictionary(permission => permission, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Право приводится к написанию из кода, а не сохраняется как пришло. Иначе
    /// <c>PLATFORM.ADMINS.MANAGE</c> ложится в базу рядом с каноническим ключом: проверки
    /// сравнивают без учёта регистра и право действует, а любое посимвольное сравнение считает
    /// их разными строками — при следующей правке роли право пропадает, и предохранитель
    /// самоблокировки этого не замечает. Неизвестные ключи остаются как есть, чтобы
    /// <see cref="CheckKnownPermissions"/> их отклонил, а не молча пропустил.
    /// </summary>
    private static HashSet<string> Normalize(IReadOnlyList<string>? permissions) =>
        (permissions ?? [])
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Select(permission => permission.Trim())
            .Select(permission => CanonicalPermissions.GetValueOrDefault(permission, permission))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    // Имя роли хранится в нижнем регистре (так его пишет создание), а из адреса приходит как
    // набрал вызывающий: без приведения `PUT /roles/Support` не нашёл бы роль `support`.
    private static string NormalizeRoleName(string roleName) => roleName.Trim().ToLowerInvariant();

    private async Task<PlatformRoleDto> LoadDtoAsync(string roleName, CancellationToken cancellationToken)
    {
        var roles = await ListAsync(cancellationToken);
        return roles.Single(role => string.Equals(role.RoleName, roleName, StringComparison.OrdinalIgnoreCase));
    }

    private static PlatformRoleDto ToDto(
        PlatformRoleEntity role,
        IReadOnlyList<PlatformRolePermissionEntity> permissions,
        IReadOnlyDictionary<string, int> holders) =>
        new(
            role.RoleName,
            role.DisplayName,
            role.Description,
            role.IsBuiltIn,
            role.GrantsAllPermissions,
            role.GrantsAllPermissions
                ? PlatformAdminPermissionNames.All
                : permissions
                    .Where(rolePermission => string.Equals(rolePermission.RoleName, role.RoleName, StringComparison.OrdinalIgnoreCase))
                    .Select(rolePermission => rolePermission.PermissionName)
                    .OrderBy(permission => permission, StringComparer.Ordinal)
                    .ToArray(),
            holders.GetValueOrDefault(role.RoleName));

    private async Task<PlatformRoleResult> ExecuteInSerializableTransactionAsync(
        Func<Task<PlatformRoleResult>> action,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            // InMemory-провайдер не знает транзакций и изоляции вовсе, и гонки в нём не бывает —
            // оборачивать нечего. Настоящую гонку проверяет Postgres-тест.
            return await action();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception) when (RelationalFailureClassifier.IsSerializationFailure(exception))
        {
            // Serializable может отменить и «невиновную» сторону гонки. Объяснять её отказ чужим
            // правилом — враньё: честный ответ здесь «повторите».
            dbContext.ChangeTracker.Clear();
            return PlatformRoleResult.Fail(PlatformRoleErrorCodes.Conflict);
        }
    }
}
