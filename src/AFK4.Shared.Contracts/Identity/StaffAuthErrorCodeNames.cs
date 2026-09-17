namespace AFK4.Shared.Contracts.Identity;

/// <summary>
/// Машинные причины отказа на входе сотрудника. Клиент по ним и подбирает слова: текст сервера
/// английский, а мастер и приложение клуба работают на трёх языках.
/// </summary>
public static class StaffAuthErrorCodeNames
{
    /// <summary>
    /// Пять промахов подряд — вход в эту учётную запись закрыт на четверть часа. Отдельно от
    /// обычного «неверно»: там человек ищет опечатку, здесь ждёт.
    /// </summary>
    public const string TooManyPasswordAttempts = "too_many_password_attempts";
}
