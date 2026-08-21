using System;

namespace AFK4.Shared.Contracts.Reservations;

/// <summary>
/// Бронь на компанию: несколько мест на одно время одним действием.
///
/// Мест здесь КОЛИЧЕСТВО, а не список. Игрок в приложении конкретную машину не выбирает — её
/// назначает клуб, — поэтому просить его выбрать пять машин было бы просьбой о том, чего он не
/// решает. Операторская групповая бронь наоборот берёт список: там человек тянет мышью по строкам
/// таймлайна и точно знает, какие места отдаёт.
///
/// Тариф один на всю компанию: сидят вместе, платят по одной цене, и разные тарифы внутри одной
/// брони — это уже не «бронь на компанию», а несколько разных броней.
/// </summary>
/// <param name="BranchId">
/// Филиал, в который компания придёт. Нужен только в первом действии в клубе, где счёта ещё нет:
/// у сети с несколькими филиалами сервер не гадает, куда записать счёт.
/// </param>
public sealed record CreatePlayerReservationGroupRequest(
    int SeatCount,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string? Note,
    Guid? TariffVersionId = null,
    Guid? BranchId = null);

/// <summary>
/// Что получилось из групповой брони: сама группа и её брони. Отдельного состояния у группы нет —
/// оно складывается из состояний броней, а дублировать его значит однажды разойтись с ними.
/// </summary>
public sealed record PlayerReservationGroupDto(
    Guid ReservationGroupId,
    IReadOnlyList<PlayerReservationDto> Reservations,
    long? TotalEstimatedCostMinorUnits,
    string? CurrencyCode);
