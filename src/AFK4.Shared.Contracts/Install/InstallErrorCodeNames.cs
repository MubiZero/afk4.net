namespace AFK4.Shared.Contracts.Install;

/// <summary>
/// Машинные имена отказов установки. Нужны затем, что мастер установки говорит на трёх языках, а
/// текст отказа с сервера — всегда английский: показать его человеку у ПК нельзя, а назвать
/// причину своими словами по коду — можно.
/// </summary>
public static class InstallErrorCodeNames
{
    /// <summary>На выбранное место уже привязан другой ПК.</summary>
    public const string SeatOccupied = "seat_occupied";

    /// <summary>
    /// Код установки не подходит: неизвестен, истёк, отозван или исчерпан. Одна причина на все
    /// четыре: угадывающему код не надо подсказывать, какой из них был почти верным.
    /// </summary>
    public const string InstallCodeInvalid = "install_code_invalid";
}
