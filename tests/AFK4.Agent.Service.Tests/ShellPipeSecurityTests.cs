using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using AFK4.Agent.Service.Shell;

namespace AFK4.Agent.Service.Tests;

/// <summary>Кто может открыть канал агента с оболочкой (спека оболочки, §4.2 и §6.1).</summary>
[SupportedOSPlatform("windows")]
public sealed class ShellPipeSecurityTests
{
    private const string PlayerSid = "S-1-5-21-1000-2000-3000-1001";

    [WindowsOnlyFact]
    public void WithAKiosk_OnlyThePlayerAccountGetsIn_BesidesSystemAndAdministrators()
    {
        var rules = Rules(ShellPipeServer.CreatePipeSecurity(PlayerSid));

        Assert.Contains(rules, rule => rule.Sid == PlayerSid && rule.Rights.HasFlag(PipeAccessRights.ReadWrite));
        Assert.DoesNotContain(rules, rule => rule.Sid == new SecurityIdentifier(WellKnownSidType.InteractiveSid, null).Value);
        Assert.Equal(3, rules.Count);
    }

    [WindowsOnlyFact]
    public void WithoutAKiosk_AnyInteractiveUserGetsIn()
    {
        var rules = Rules(ShellPipeServer.CreatePipeSecurity(null));

        Assert.Contains(rules, rule => rule.Sid == new SecurityIdentifier(WellKnownSidType.InteractiveSid, null).Value);
    }

    /// <summary>Кривой SID не роняет канал: без него оболочка не подключилась бы, и ПК стоял бы без экрана.</summary>
    [WindowsOnlyFact]
    public void ABrokenSid_FallsBackToTheInteractiveUser()
    {
        Assert.Equal(
            new SecurityIdentifier(WellKnownSidType.InteractiveSid, null),
            ShellPipeServer.PipeClient("not-a-sid"));
    }

    private static List<(string Sid, PipeAccessRights Rights)> Rules(PipeSecurity security) =>
        security.GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>()
            .Where(rule => rule.AccessControlType == AccessControlType.Allow)
            .Select(rule => (rule.IdentityReference.Value, rule.PipeAccessRights))
            .ToList();
}
