/// Правила поверх сгенерированных контрактов.
///
/// Сами контракты приезжают из `AFK4.Shared.Contracts` и знают только поля, которые есть на
/// проводе. Всё, что выводится ИЗ этих полей — «отменяемо ли», «сколько осталось мест», —
/// живёт здесь: генератору неоткуда узнать про такое, а экранам одно и то же правило нужно
/// в нескольких местах, и повторять его в каждом виджете значило бы однажды разойтись.
library;

import 'contracts.dart';

/// Режим сессии. `fixed` — оплачена наперёд, показывается остаток; `open` — счётчик времени
/// и накопленная стоимость.
enum SessionDurationMode { open, fixed }

extension ActiveSessionRules on ActiveSessionDto {
  /// Режим сессии словом с провода — разобранный в перечисление, чтобы экран не сверял строки.
  SessionDurationMode get mode =>
      durationMode == 'fixed' ? SessionDurationMode.fixed : SessionDurationMode.open;
}

extension MeRules on MeDto {
  /// Счёт в названном клубе. null — человек в этом клубе ещё ничего не делал, и счёта там
  /// пока нет. Это нормальное состояние, а не сбой.
  MyClubDto? clubAt(String? organizationId) {
    if (organizationId == null) return null;
    for (final club in clubs) {
      if (club.organizationId == organizationId) return club;
    }
    return null;
  }
}

extension PlayerBookingRulesRules on PlayerBookingRulesDto {
  bool get bookingOff => acceptanceMode == 'off';

  bool get reviewedByStaff => acceptanceMode == 'manual';
}

extension TariffOptionRules on TariffOptionDto {
  /// Действует ли тариф прямо сейчас — по часам клуба, а не телефона. Важно там, где играть
  /// начинают сию секунду; для брони на завтра ответа нет вовсе (`null`), и тариф тогда
  /// остаётся выбираемым: «сейчас» к нему просто не относится.
  bool get isAvailableNow => appliesNow != false;
}

extension PackageOptionRules on PackageOptionDto {
  /// Всё время пакета вместе с бонусным: игрок покупает часы, а не две отдельные величины.
  int get totalSeconds => includedSeconds + bonusSeconds;
}

extension PlayerTournamentRules on PlayerTournamentDto {
  bool get isCancelled => state == 'cancelled';

  bool get isFree => entryFee.minorUnits == 0;

  /// Сколько мест осталось. null — потолка нет, и «осталось N» было бы выдумкой.
  int? get freeSpots => capacity == 0 ? null : (capacity - registeredCount).clamp(0, capacity);
}

extension PlayerPackageRules on PlayerPackageDto {
  int get remainingSeconds => remainingIncludedSeconds + remainingBonusSeconds;

  bool get isSpent => remainingSeconds <= 0;

  bool isExpired(DateTime now) => expiresAtUtc != null && !expiresAtUtc!.isAfter(now);

  /// Пакетом ещё можно играть: время осталось и срок не вышел.
  bool isUsable(DateTime now) => !isSpent && !isExpired(now);
}

extension ReservationQuoteRules on ReservationQuoteDto {
  /// Тариф берёт больше, чем игрок забронировал, — об этом надо сказать до подтверждения.
  bool get hasMinimum => billableMinutes > requestedMinutes;
}

extension PlayerReservationRules on PlayerReservationDto {
  /// Отменить можно то, что ещё не состоялось. Отменённую или уже отыгранную бронь трогать
  /// нечего — кнопка там только сбивает с толку.
  bool get isCancellable => state == 'pending' || state == 'confirmed';
}

extension ShopOrderRules on ShopOrderDto {
  /// Отменить можно, пока заказ не выдан: после «принесли» отменять нечего.
  bool get isCancellable => status == 'placed' || status == 'accepted';

  /// Заказ ещё в работе — за ним есть смысл следить.
  bool get isOpen => status == 'placed' || status == 'accepted';
}

extension CashbackEntryRules on CashbackEntryDto {
  /// Источник начисления: `topup`, `shop`, `session` или null, если причина незнакомая.
  String? get source {
    final parts = reason.split(':');
    return parts.length >= 2 && parts[0] == 'cashback' ? parts[1] : null;
  }
}

extension PlayerLoyaltyRules on PlayerLoyaltyDto {
  /// Клуб не начисляет кешбэк ни за что — рассказывать о нём нечего.
  bool get isOff =>
      !(topUpEnabled && topUpPercentBasisPoints > 0) &&
      !(shopEnabled && shopPercentBasisPoints > 0) &&
      !(sessionEnabled && sessionPercentBasisPoints > 0);
}

extension PlayerAchievementRules on PlayerAchievementDto {
  bool get unlocked => unlockedAtUtc != null;
}
