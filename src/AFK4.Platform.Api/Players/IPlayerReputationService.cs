using AFK4.Shared.Contracts.Players;

namespace AFK4.Platform.Api.Players;

/// <summary>
/// Единственный факт, которым сеть отвечает клубу про незнакомого гостя: можно ли ему доверять.
///
/// Ограничение живёт здесь, а не в интерфейсе: операторское приложение ходит в тот же API, что и
/// curl, поэтому «скрыто на экране» защитой не считается.
/// </summary>
public interface IPlayerReputationService
{
    /// <summary>
    /// Агрегат по личности, с которой у клуба есть основание спрашивать: заведённая связь либо
    /// живая заявка. <c>null</c> — оснований нет <b>или</b> такой личности не существует: снаружи
    /// эти два случая обязаны быть одним и тем же.
    /// </summary>
    Task<PlayerReputationDto?> GetForLinkedPersonAsync(
        Guid organizationId, Guid platformPersonId, CancellationToken cancellationToken);

    /// <summary>
    /// Агрегат по точному номеру. <c>null</c> возвращается ровно в одном случае — номер не может
    /// принадлежать никому (огрызок, буквы, пустая строка); он ничего ни о ком не выдаёт, поэтому
    /// отличаться ему можно. Незнакомый сети номер отвечает тем же, чем зарегистрированный без
    /// единого визита, — нулями.
    /// </summary>
    Task<PlayerReputationDto?> GetByExactPhoneAsync(string rawPhone, CancellationToken cancellationToken);
}
