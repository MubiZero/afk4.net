using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Branches;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Branches;

/// <summary>
/// Чем живёт филиал, который настройки приёма гостей не открывал ни разу, и в каких границах
/// вообще бывают эти числа.
///
/// Строки в <c>branch_booking_settings</c> у большинства филиалов не будет никогда — это норма, а
/// не пробел: значения по умолчанию выбраны так, чтобы ненастроенный клуб вёл себя осторожно.
/// Брони он принимает и подтверждает сам (иначе функция, за которую заплачено, молчит), но с
/// незнакомого гостя просит предоплату и держит для него одну бронь за раз — риск пустого зала
/// несёт клуб, и по умолчанию этот риск минимальный.
/// </summary>
public static class BranchBookingSettingsDefaults
{
    /// <summary>Брони принимаем и подтверждаем сами: включённая функция должна работать без стойки.</summary>
    public const string AcceptanceMode = BranchBookingAcceptanceModes.Auto;

    /// <summary>Четверть часа на ответ: обещание, которое стойка выполняет и в загруженный вечер.</summary>
    public const int RespondWithinMinutes = 15;

    /// <summary>С незнакомого гостя — предоплата: у брони без денег ничего не стоит не состояться.</summary>
    public const bool RequirePrepaymentFromNewGuests = true;

    /// <summary>Одна бронь у новичка: пока клуб его не знает, зал под него не занимают целиком.</summary>
    public const int MaxActiveReservationsForNewGuests = 1;

    /// <summary>Три визита — и гость свой: одного мало, десять — придирка к постоянному клиенту.</summary>
    public const int RegularAfterVisits = 3;

    /// <summary>Двадцать минут после начала место ещё ждёт: столько занимает дорога и очередь.</summary>
    public const int HoldSeatAfterStartMinutes = 20;

    /// <summary>Предоплату при неявке не удерживаем: штраф по умолчанию — это не то, о чём молчат.</summary>
    public const bool KeepPrepaymentOnNoShow = false;

    public const int MinRespondWithinMinutes = 5;
    public const int MaxRespondWithinMinutes = 24 * 60;
    public const int MinActiveReservationsForNewGuests = 1;
    public const int MaxActiveReservationsLimit = 20;
    public const int MaxRegularAfterVisits = 100;
    public const int MaxHoldSeatAfterStartMinutes = 240;

    /// <summary>Настройки филиала: строка клуба, если она есть, и значения по умолчанию, если нет.</summary>
    public static BranchBookingSettingsDto For(
        Guid organizationId,
        Guid branchId,
        BranchBookingSettingsEntity? configured) =>
        configured is null
            ? new BranchBookingSettingsDto(
                organizationId,
                branchId,
                AcceptanceMode,
                RespondWithinMinutes,
                RequirePrepaymentFromNewGuests,
                MaxActiveReservationsForNewGuests,
                RegularAfterVisits,
                HoldSeatAfterStartMinutes,
                KeepPrepaymentOnNoShow,
                UpdatedAtUtc: null)
            : new BranchBookingSettingsDto(
                configured.OrganizationId,
                configured.BranchId,
                configured.AcceptanceMode,
                configured.RespondWithinMinutes,
                configured.RequirePrepaymentFromNewGuests,
                configured.MaxActiveReservationsForNewGuests,
                configured.RegularAfterVisits,
                configured.HoldSeatAfterStartMinutes,
                configured.KeepPrepaymentOnNoShow,
                configured.UpdatedAtUtc);

    /// <summary>
    /// Единственное чтение настроек филиала на весь бэкенд. И сервис настроек, и бронирование, и
    /// ответ приложению ходят сюда: вторая копия «а если строки нет» однажды разошлась бы с первой,
    /// и клуб получил бы один ответ на стойке и другой в приложении.
    /// </summary>
    public static async Task<BranchBookingSettingsDto> ResolveAsync(
        PlatformDbContext dbContext,
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var configured = await dbContext.BranchBookingSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                row => row.BranchId == branchId && row.OrganizationId == organizationId,
                cancellationToken);

        return For(organizationId, branchId, configured);
    }

    /// <summary>Что не так с присланными настройками, или null, если всё в порядке.</summary>
    public static string? Validate(UpdateBranchBookingSettingsRequest request)
    {
        if (!BranchBookingAcceptanceModes.IsSupported(request.AcceptanceMode))
        {
            return $"AcceptanceMode must be one of: {string.Join(", ", BranchBookingAcceptanceModes.All)}.";
        }

        if (request.RespondWithinMinutes is < MinRespondWithinMinutes or > MaxRespondWithinMinutes)
        {
            return $"RespondWithinMinutes must be between {MinRespondWithinMinutes} and {MaxRespondWithinMinutes}.";
        }

        if (request.MaxActiveReservationsForNewGuests
            is < MinActiveReservationsForNewGuests or > MaxActiveReservationsLimit)
        {
            return "MaxActiveReservationsForNewGuests must be between "
                + $"{MinActiveReservationsForNewGuests} and {MaxActiveReservationsLimit}.";
        }

        // Ноль визитов — законный ответ «новичков у нас нет»: клуб снимает ограничения со всех.
        if (request.RegularAfterVisits is < 0 or > MaxRegularAfterVisits)
        {
            return $"RegularAfterVisits must be between 0 and {MaxRegularAfterVisits}.";
        }

        if (request.HoldSeatAfterStartMinutes is < 0 or > MaxHoldSeatAfterStartMinutes)
        {
            return $"HoldSeatAfterStartMinutes must be between 0 and {MaxHoldSeatAfterStartMinutes}.";
        }

        return null;
    }
}
