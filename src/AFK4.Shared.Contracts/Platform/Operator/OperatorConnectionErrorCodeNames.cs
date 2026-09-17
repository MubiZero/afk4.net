namespace AFK4.Shared.Contracts.Platform.Operator;

/// <summary>
/// Машинные имена отказов при подключении админки клуба к клубу.
///
/// Этот экран человек видит раньше всего остального — до входа, до языка интерфейса он уже
/// выбран. Английская фраза сервера («OrganizationSlug must contain only lowercase letters…»)
/// доезжала до него дословно: непонятно и похоже на поломку программы.
/// </summary>
public static class OperatorConnectionErrorCodeNames
{
    /// <summary>Не указано ни адреса клуба и филиала, ни кода подключения.</summary>
    public const string InputMissing = "connection_input_missing";

    /// <summary>Указано и то и другое сразу.</summary>
    public const string InputAmbiguous = "connection_input_ambiguous";

    /// <summary>Адрес клуба или филиала записан не по формату.</summary>
    public const string SlugInvalid = "connection_slug_invalid";

    /// <summary>Код подключения пустой.</summary>
    public const string SetupCodeRequired = "setup_code_required";

    /// <summary>Кодом подключения уже воспользовались или его отозвали.</summary>
    public const string SetupCodeNotUsable = "setup_code_not_usable";
}
