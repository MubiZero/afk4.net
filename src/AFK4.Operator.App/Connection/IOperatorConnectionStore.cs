namespace AFK4.Operator.App.Connection;

public interface IOperatorConnectionStore
{
    Task SaveAsync(OperatorConnectionSnapshot snapshot, CancellationToken cancellationToken);

    Task<OperatorConnectionSnapshot?> LoadAsync(CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
