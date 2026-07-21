namespace AFK4.Platform.Api.Payments.Eskhata;

// Собирает клиент под org: читает EskhataMerchantConfig (BranchId==null), расшифровывает Hash-Key.
// Возвращает null, если конфиг отсутствует/неполон.
public interface IEskhataMerchantClientFactory
{
    Task<IEskhataMerchantClient?> CreateForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);
}
