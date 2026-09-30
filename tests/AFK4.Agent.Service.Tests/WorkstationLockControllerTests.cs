using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Protection;
using AFK4.Agent.Service.Tests.Protection;
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

    // Диспетчер задач Windows игроку не нужен и в сессии: закрыть «лишнее» он мог бы и оболочку, и
    // чужие процессы. Свой список — «Мои приложения» в оболочке.
    [Fact]
    public async Task OpenForSessionAsync_KeepsTaskManagerDisabled()
    {
        var policies = new RecordingPolicyStore();
        var controller = CreateController(policies);

        await controller.LockAsync(CancellationToken.None);
        var outcome = await controller.OpenForSessionAsync(CancellationToken.None);

        Assert.True(outcome.IsEnforced);
        Assert.Equal(1, policies.Values["DisableTaskMgr"]);
    }

    [Fact]
    public async Task OpenForSessionAsync_PutsThePolicyBackIfSomethingRemovedIt()
    {
        var policies = new RecordingPolicyStore();
        var controller = CreateController(policies);

        await controller.OpenForSessionAsync(CancellationToken.None);

        Assert.Equal(1, policies.Values["DisableTaskMgr"]);
    }

    // Технику в обслуживании диспетчер нужен: это путь «Вернуть в зал», а не игрока.
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

    /// <summary>
    /// Приёмка на виртуалке: после «Снять киоск» агент остался и на каждом запирании снова
    /// выключал Диспетчер задач — на всей машине, у её администратора. Без учётки игрока запирать
    /// некого: запрет снимается, и в ответе честное «ничего».
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WithoutAPlayerAccount_TheTaskManagerIsGivenBack(bool openForSession)
    {
        var policies = new RecordingPolicyStore();
        policies.Values["DisableTaskMgr"] = 1;
        var controller = CreateController(policies, new ProtectionEnforcerTests.FakeRegistry { PlayerAccount = false });

        var outcome = openForSession
            ? await controller.OpenForSessionAsync(CancellationToken.None)
            : await controller.LockAsync(CancellationToken.None);

        Assert.False(outcome.IsEnforced);
        Assert.DoesNotContain("DisableTaskMgr", policies.Values.Keys);
    }

    private static WorkstationLockController CreateController(IMachinePolicyStore policies, IMachineRegistry? registry = null) =>
        new(policies, registry ?? new ProtectionEnforcerTests.FakeRegistry(), NullLogger<WorkstationLockController>.Instance);

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
