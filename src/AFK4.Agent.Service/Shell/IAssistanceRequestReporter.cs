namespace AFK4.Agent.Service.Shell;

/// <summary>Сообщает серверу, что с этой машины позвали оператора.</summary>
public interface IAssistanceRequestReporter
{
    Task ReportAsync(DateTimeOffset requestedAtUtc, CancellationToken cancellationToken);
}
