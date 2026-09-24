using System.Threading.Channels;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Shell;

public interface IShellHostChannel
{
    /// <summary>Передать команду подключённому хосту. false — хоста нет или он не успевает: сказать некому.</summary>
    bool TryPost(ShellPipeCommandDto command);
}

/// <summary>
/// Очередь команд клуба к хосту на одно подключение канала. Хост ушёл — очередь закрывается: команда,
/// отданная пустоте, в журнале значилась бы переданной.
/// </summary>
public sealed class ShellHostChannel : IShellHostChannel
{
    private const int Capacity = 16;
    private readonly Lock gate = new();
    private Channel<ShellPipeCommandDto>? current;

    public bool TryPost(ShellPipeCommandDto command)
    {
        lock (gate)
        {
            return current?.Writer.TryWrite(command) ?? false;
        }
    }

    public ChannelReader<ShellPipeCommandDto> Attach()
    {
        lock (gate)
        {
            current?.Writer.TryComplete();
            current = Channel.CreateBounded<ShellPipeCommandDto>(new BoundedChannelOptions(Capacity)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            });
            return current.Reader;
        }
    }

    public void Detach(ChannelReader<ShellPipeCommandDto> reader)
    {
        lock (gate)
        {
            if (current is not null && ReferenceEquals(current.Reader, reader))
            {
                current.Writer.TryComplete();
                current = null;
            }
        }
    }
}
