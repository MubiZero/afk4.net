using AFK4.Agent.Service.Enforcement;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFK4.Agent.Service.Tests;

public sealed class WorkstationLockControllerTests
{
    // Здесь были две строки в журнал и Task.CompletedTask: сервер получал «заблокировано» на
    // машине, где не запиралось ничего.
    [Fact]
    public async Task LockAsync_DisablesTaskManagerAndSaysSo()
    {
        var policies = new RecordingPolicyStore();
        var controller = CreateController(policies);

        var outcome = await controller.LockAsync(CancellationToken.None);

        Assert.True(outcome.IsEnforced);
        Assert.Contains("task manager", outcome.Describe(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, policies.Values["DisableTaskMgr"]);
    }

    [Fact]
    public async Task UnlockAsync_RemovesWhatTheLockPutThere()
    {
        var policies = new RecordingPolicyStore();
        var controller = CreateController(policies);

        await controller.LockAsync(CancellationToken.None);
        var outcome = await controller.UnlockAsync(CancellationToken.None);

        Assert.True(outcome.IsEnforced);
        Assert.DoesNotContain("DisableTaskMgr", policies.Values.Keys);
    }

    // Не на Windows запирать нечем, и молча отвечать «заблокировано» нельзя — ровно этим
    // прежняя реализация и занималась.
    [Fact]
    public async Task LockAsync_WithoutPolicySupport_EnforcesNothingAndAdmitsIt()
    {
        var controller = CreateController(new RecordingPolicyStore { IsSupported = false });

        var outcome = await controller.LockAsync(CancellationToken.None);

        Assert.False(outcome.IsEnforced);
        Assert.Equal("nothing", outcome.Describe());
    }

    // Политика, которую не дали поставить (нет прав, реестр только на чтение), не превращается
    // в «запёрто»: оператор прочитает в результате команды, что именно получилось.
    [Fact]
    public async Task LockAsync_WhenThePolicyCannotBeWritten_ReportsNothingEnforced()
    {
        var controller = CreateController(new RecordingPolicyStore { WritesSucceed = false });

        var outcome = await controller.LockAsync(CancellationToken.None);

        Assert.False(outcome.IsEnforced);
    }

    private static WorkstationLockController CreateController(IMachinePolicyStore policies) =>
        new(policies, NullLogger<WorkstationLockController>.Instance);

    private sealed class RecordingPolicyStore : IMachinePolicyStore
    {
        public Dictionary<string, int> Values { get; } = [];

        public bool IsSupported { get; init; } = true;

        public bool WritesSucceed { get; init; } = true;

        public bool Set(string valueName, int value)
        {
            if (!WritesSucceed)
            {
                return false;
            }

            Values[valueName] = value;
            return true;
        }

        public bool Remove(string valueName)
        {
            Values.Remove(valueName);
            return true;
        }
    }
}
