using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using Xunit;

namespace AFK4.Platform.Api.Tests.Identity;

// Guards the Этап-0 §2 visibility contract on the backend side: the shift_supervisor
// reconciliation (approve money actions; no audit/diagnostics → no Управление).
// Mirror of the frontend operatorVisibility.test.ts fixture.
public sealed class OrganizationPermissionCatalogContractTests
{
    [Fact]
    public void ShiftSupervisor_CanApproveMoneyActions()
    {
        var permissions = OrganizationPermissionCatalog.GetPermissions([OrganizationRoleNames.ShiftSupervisor]);
        Assert.Contains(OrganizationPermissionNames.ApproveMoneyAction, permissions);
    }

    [Theory]
    [InlineData(OrganizationPermissionNames.ViewAudit)]
    [InlineData(OrganizationPermissionNames.ViewDiagnostics)]
    public void ShiftSupervisor_HasNoManagementOnlyVisibility(string permission)
    {
        var permissions = OrganizationPermissionCatalog.GetPermissions([OrganizationRoleNames.ShiftSupervisor]);
        Assert.DoesNotContain(permission, permissions);
    }

    // Граница очереди заказов бара: двигаются ли деньги.
    //
    // Принять и выдать — чистая смена статуса, заказ оплачен при оформлении; еду выдаёт кассир.
    // Отменить — идёт через денежный координатор и возвращает деньги; остаётся у тех же, у кого
    // возвраты в кассе. Пока право было одно, лента показывалась кассиру и отвечала 403 на
    // каждое действие.
    [Theory]
    [InlineData(OrganizationRoleNames.OrganizationOwner)]
    [InlineData(OrganizationRoleNames.BranchManager)]
    [InlineData(OrganizationRoleNames.ShiftSupervisor)]
    [InlineData(OrganizationRoleNames.Operator)]
    public void EveryCounterRole_CanServeShopOrders(string role)
    {
        var permissions = OrganizationPermissionCatalog.GetPermissions([role]);
        Assert.Contains(OrganizationPermissionNames.ServeShopOrders, permissions);
    }

    [Fact]
    public void Operator_CannotCancelShopOrder_BecauseCancelRefundsMoney()
    {
        var permissions = OrganizationPermissionCatalog.GetPermissions([OrganizationRoleNames.Operator]);
        Assert.DoesNotContain(OrganizationPermissionNames.ManageShopOrders, permissions);
    }

    // Старший смены уже делает возвраты в кассе и ручные корректировки леджера. Запрещать ему
    // списать разбитую бутылку не от чего: это строго безопаснее того, что он уже может.
    [Fact]
    public void ShiftSupervisor_CanWriteOffStock()
    {
        var permissions = OrganizationPermissionCatalog.GetPermissions([OrganizationRoleNames.ShiftSupervisor]);
        Assert.Contains(OrganizationPermissionNames.ManageInventoryStock, permissions);
        // Контроль, ради которого это безопасно: возвраты у него и так есть.
        Assert.Contains(OrganizationPermissionNames.RefundPosSale, permissions);
    }

    // Турнир и новость филиала заводит тот, кто этот филиал ведёт. Отмена турнира возвращает
    // взносы, но возвраты у управляющего филиалом и так есть.
    [Theory]
    [InlineData(OrganizationPermissionNames.ManageNews)]
    [InlineData(OrganizationPermissionNames.ManageTournaments)]
    public void BranchManager_RunsBranchContent(string permission)
    {
        var permissions = OrganizationPermissionCatalog.GetPermissions([OrganizationRoleNames.BranchManager]);
        Assert.Contains(permission, permissions);
    }

    [Theory]
    [InlineData(OrganizationRoleNames.Operator)]
    [InlineData(OrganizationRoleNames.Technician)]
    [InlineData(OrganizationRoleNames.Accountant)]
    public void RolesWithoutRefunds_DoNotManageStock(string role)
    {
        var permissions = OrganizationPermissionCatalog.GetPermissions([role]);
        Assert.DoesNotContain(OrganizationPermissionNames.ManageInventoryStock, permissions);
    }
}
