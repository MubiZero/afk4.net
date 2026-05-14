using System.Text.Json;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Shifts;

namespace AFK4.Shared.Contracts.Tests;

public sealed class ShiftContractSerializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void OpenShiftRequest_RoundTrips()
    {
        var request = new OpenShiftRequest(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            new MoneyDto("TJS", 50000),
            "front register",
            "shift-open-001");

        var copy = JsonSerializer.Deserialize<OpenShiftRequest>(
            JsonSerializer.Serialize(request, Options),
            Options);

        Assert.Equal(request, copy);
    }

    [Fact]
    public void RecordCashMovementRequest_RoundTrips()
    {
        var request = new RecordCashMovementRequest(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            CashMovementTypeNames.CashIn,
            new MoneyDto("TJS", 25000),
            "cash drawer refill",
            "cash-in-001");

        var copy = JsonSerializer.Deserialize<RecordCashMovementRequest>(
            JsonSerializer.Serialize(request, Options),
            Options);

        Assert.Equal(request, copy);
    }

    [Fact]
    public void CloseShiftRequest_RoundTrips()
    {
        var request = new CloseShiftRequest(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            new MoneyDto("TJS", 164000),
            "short by one somoni",
            "shift-close-001");

        var copy = JsonSerializer.Deserialize<CloseShiftRequest>(
            JsonSerializer.Serialize(request, Options),
            Options);

        Assert.Equal(request, copy);
    }

    [Fact]
    public void ShiftDto_RoundTrips()
    {
        var shift = new ShiftDto(
            ShiftId: Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
            OrganizationId: Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            OpenedByStaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
            ClosedByStaffUserId: null,
            State: ShiftStateNames.Open,
            StartingCash: new MoneyDto("TJS", 50000),
            CountedCash: null,
            ExpectedCash: null,
            Difference: null,
            OpeningNote: "front register",
            ClosingNote: "",
            OpenedAtUtc: DateTimeOffset.Parse("2026-05-13T08:00:00Z"),
            ClosedAtUtc: null);

        var copy = JsonSerializer.Deserialize<ShiftDto>(
            JsonSerializer.Serialize(shift, Options),
            Options);

        Assert.NotNull(copy);
        Assert.Equal(shift.ShiftId, copy.ShiftId);
        Assert.Equal(ShiftStateNames.Open, copy.State);
        Assert.Equal(50000, copy.StartingCash.MinorUnits);
    }

    [Fact]
    public void CashMovementDto_RoundTrips()
    {
        var movement = new CashMovementDto(
            CashMovementId: Guid.Parse("cccccccc-0000-0000-0000-000000000001"),
            OrganizationId: Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            ShiftId: Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
            CreatedByStaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
            MovementType: CashMovementTypeNames.CashOut,
            Amount: new MoneyDto("TJS", 10000),
            Reason: "petty cash",
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-13T09:00:00Z"));

        var copy = JsonSerializer.Deserialize<CashMovementDto>(
            JsonSerializer.Serialize(movement, Options),
            Options);

        Assert.NotNull(copy);
        Assert.Equal(CashMovementTypeNames.CashOut, copy.MovementType);
        Assert.Equal(10000, copy.Amount.MinorUnits);
    }

    [Fact]
    public void ShiftSummaryDto_RoundTrips()
    {
        var summary = new ShiftSummaryDto(
            Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
            new MoneyDto("TJS", 50000),
            new MoneyDto("TJS", 25000),
            new MoneyDto("TJS", 100000),
            new MoneyDto("TJS", 10000),
            new MoneyDto("TJS", 165000),
            new MoneyDto("TJS", 164000),
            new MoneyDto("TJS", -1000));

        var copy = JsonSerializer.Deserialize<ShiftSummaryDto>(
            JsonSerializer.Serialize(summary, Options),
            Options);

        Assert.Equal(summary, copy);
    }

    [Fact]
    public void Constants_ExposeStableShiftNames()
    {
        Assert.Equal("open", ShiftStateNames.Open);
        Assert.Equal("closed", ShiftStateNames.Closed);
        Assert.Equal("cash_in", CashMovementTypeNames.CashIn);
        Assert.Equal("cash_out", CashMovementTypeNames.CashOut);
    }

    [Fact]
    public void StaffPermissionNames_ExposeShiftPermissions()
    {
        Assert.Equal("shifts.open", StaffPermissionNames.OpenShift);
        Assert.Equal("shifts.close", StaffPermissionNames.CloseShift);
        Assert.Equal("shifts.view", StaffPermissionNames.ViewShift);
        Assert.Equal("shifts.cash.manage", StaffPermissionNames.ManageShiftCash);
        Assert.Equal("reports.view", StaffPermissionNames.ViewReports);
    }
}
