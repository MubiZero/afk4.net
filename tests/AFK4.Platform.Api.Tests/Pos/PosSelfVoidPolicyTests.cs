using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Pos;

namespace AFK4.Platform.Api.Tests.Pos;

/// <summary>
/// Окно самостоятельной отмены. Правило вынесено из эндпоинта и проверяется отдельно, потому
/// что каждая его граница — это деньги: шире окно — безнадзорный возврат наличных, уже —
/// кассир снова обходит ошибку вместо того, чтобы её исправить.
/// </summary>
public sealed class PosSelfVoidPolicyTests
{
    private static readonly Guid Cashier = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OtherStaff = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid OpenShift = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ClosedShift = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-13T20:00:00Z");

    private static PosSaleEntity Sale(Guid? createdBy = null, Guid? shiftId = null, TimeSpan? age = null) => new()
    {
        PosSaleId = Guid.NewGuid(),
        CreatedByStaffUserId = createdBy ?? Cashier,
        ShiftId = shiftId ?? OpenShift,
        CreatedAtUtc = Now - (age ?? TimeSpan.FromMinutes(1)),
    };

    [Fact]
    public void OwnFreshSaleInTheOpenShift_CanBeVoided()
    {
        Assert.True(PosSelfVoidPolicy.CanSelfVoid(Sale(), Cashier, OpenShift, Now));
    }

    // Чужой чек — даже свежий — кассир не отменяет: он не знает, что там пробивали.
    [Fact]
    public void AnotherCashiersSale_StaysWithTheSupervisor()
    {
        Assert.False(PosSelfVoidPolicy.CanSelfVoid(Sale(createdBy: OtherStaff), Cashier, OpenShift, Now));
    }

    // Смена, в которой пробили, уже сведена: правка задним числом ломает сверку.
    [Fact]
    public void SaleFromAnotherShift_StaysWithTheSupervisor()
    {
        Assert.False(PosSelfVoidPolicy.CanSelfVoid(Sale(shiftId: ClosedShift), Cashier, OpenShift, Now));
    }

    [Fact]
    public void WithoutAnOpenShift_NothingIsSelfVoidable()
    {
        Assert.False(PosSelfVoidPolicy.CanSelfVoid(Sale(), Cashier, openShiftId: null, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(299)]
    [InlineData(300)]
    public void InsideTheWindow_IsAllowed(int ageSeconds)
    {
        var sale = Sale(age: TimeSpan.FromSeconds(ageSeconds));
        Assert.True(PosSelfVoidPolicy.CanSelfVoid(sale, Cashier, OpenShift, Now));
    }

    [Theory]
    [InlineData(301)]
    [InlineData(3600)]
    public void OutsideTheWindow_IsRefused(int ageSeconds)
    {
        var sale = Sale(age: TimeSpan.FromSeconds(ageSeconds));
        Assert.False(PosSelfVoidPolicy.CanSelfVoid(sale, Cashier, OpenShift, Now));
    }

    // Часы разъехались и продажа «из будущего». Открывать окно на этом нельзя: сбитое время
    // превратило бы его в бессрочное.
    [Fact]
    public void SaleInTheFuture_IsRefused()
    {
        var sale = Sale(age: TimeSpan.FromMinutes(-1));
        Assert.False(PosSelfVoidPolicy.CanSelfVoid(sale, Cashier, OpenShift, Now));
    }

    [Fact]
    public void WindowIsFiveMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), PosSelfVoidPolicy.SelfVoidWindow);
    }
}
