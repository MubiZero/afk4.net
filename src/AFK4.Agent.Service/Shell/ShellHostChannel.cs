using System.Threading.Channels;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Shell;

public interface IShellHostChannel
{
    /// <summary>Передать кадр подключённому хосту. false — хоста нет или он не успевает: сказать некому.</summary>
    bool TryPost(ShellPipeMessage frame);

    /// <summary>Подключён ли сейчас хост. Вход по QR без хоста выдал бы токены в пустоту.</summary>
    bool HostConnected { get; }
}

/// <summary>
/// Очередь кадров к хосту без его запроса — команды клуба и вход игрока — на одно подключение канала.
/// Хост ушёл — очередь закрывается: команда, отданная пустоте, в журнале значилась бы переданной.
/// </summary>
public sealed class ShellHostChannel : IShellHostChannel
{
    private const int Capacity = 16;
    private readonly Lock gate = new();
    private Channel<ShellPipeMessage>? current;

    public bool HostConnected
    {
        get
        {
            lock (gate)
            {
                return current is not null;
            }
        }
    }

    public bool TryPost(ShellPipeMessage frame)
    {
        lock (gate)
        {
            return current?.Writer.TryWrite(frame) ?? false;
        }
    }

    public ChannelReader<ShellPipeMessage> Attach()
    {
        lock (gate)
        {
            current?.Writer.TryComplete();
            current = Channel.CreateBounded<ShellPipeMessage>(new BoundedChannelOptions(Capacity)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            });
            return current.Reader;
        }
    }

    public void Detach(ChannelReader<ShellPipeMessage> reader)
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
