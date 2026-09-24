using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Realtime;

/// <summary>Просьбы оболочки к агенту: запустить игру, позвать администратора.</summary>
public interface IShellAgentRequests
{
    /// <summary>
    /// Ответ агента. Агента нет на связи — ответ с <see cref="ShellPipeErrorCodeNames.AgentUnavailable"/>,
    /// а не исключение: игроку надо сказать правду, а не уронить окно.
    /// </summary>
    Task<ShellPipeReplyDto> RequestAsync(string type, IReadOnlyDictionary<string, string> payload, CancellationToken cancellationToken);
}
