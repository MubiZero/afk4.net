using System.Reflection;
using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Shared.Contracts.Tests.Platform;

public sealed class PlatformAdminPermissionNamesTests
{
    [Fact]
    public void All_ListsEveryDeclaredPermissionConstant()
    {
        var constants = typeof(PlatformAdminPermissionNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        // Список All — то, что панель показывает как чекбоксы, а роль с полным доступом получает
        // целиком. Право, забытое в списке, нельзя ни выдать роли, ни увидеть — и обнаружится это
        // только жалобой из прода.
        Assert.Equal(
            constants.OrderBy(name => name, StringComparer.Ordinal),
            PlatformAdminPermissionNames.All.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void All_HasNoDuplicates()
    {
        Assert.Equal(PlatformAdminPermissionNames.All.Count, PlatformAdminPermissionNames.All.Distinct(StringComparer.Ordinal).Count());
    }
}
