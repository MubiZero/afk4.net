namespace AFK4.Operator.App.Auth;

public interface IOperatorTokenStore
{
    Task SaveAsync(OperatorTokenSnapshot snapshot, CancellationToken cancellationToken);

    Task<OperatorTokenSnapshot?> LoadAsync(CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
