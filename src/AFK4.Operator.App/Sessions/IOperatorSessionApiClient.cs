using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Operator.App.Sessions;

public interface IOperatorSessionApiClient
{
    Task<SessionCommandResponse> StartGuestSessionAsync(
        Guid branchId,
        StartGuestSessionRequest request,
        CancellationToken cancellationToken);

    Task<SessionCommandResponse> ExtendSessionAsync(
        Guid sessionId,
        ExtendSessionRequest request,
        CancellationToken cancellationToken);

    Task<SessionCommandResponse> TransferSessionAsync(
        Guid sessionId,
        TransferSessionRequest request,
        CancellationToken cancellationToken);

    Task<SessionCommandResponse> EndSessionAsync(
        Guid sessionId,
        EndSessionRequest request,
        CancellationToken cancellationToken);

    Task<SessionCheckoutQuoteResponse> GetCheckoutQuoteAsync(
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<SessionCheckoutResponse> CheckoutSessionAsync(
        Guid sessionId,
        SessionCheckoutRequest request,
        CancellationToken cancellationToken);
}

public sealed class UnconfiguredOperatorSessionApiClient : IOperatorSessionApiClient
{
    public Task<SessionCommandResponse> StartGuestSessionAsync(
        Guid branchId,
        StartGuestSessionRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<SessionCommandResponse> ExtendSessionAsync(
        Guid sessionId,
        ExtendSessionRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<SessionCommandResponse> TransferSessionAsync(
        Guid sessionId,
        TransferSessionRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<SessionCommandResponse> EndSessionAsync(
        Guid sessionId,
        EndSessionRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<SessionCheckoutQuoteResponse> GetCheckoutQuoteAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<SessionCheckoutResponse> CheckoutSessionAsync(
        Guid sessionId,
        SessionCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    private static InvalidOperationException CreateException()
    {
        return new InvalidOperationException("Operator session API client is not configured.");
    }
}
