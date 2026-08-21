/// Расписание клуба, каким его читает витрина.
///
/// «Открыт ли он сейчас» — вопрос, который игрок задаёт до выхода из дома, и ответ на него
/// стоит дороже самого расписания: семь строк «10:00–23:00» на карточке никто не читает.
library;

import '../l10n/app_localizations.dart';

/// Один день расписания. День недели по ISO-8601: 1 = понедельник … 7 = воскресенье.
class OpeningDay {
  const OpeningDay({
    required this.dayOfWeek,
    required this.isClosed,
    this.openTime,
    this.closeTime,
  });

  final int dayOfWeek;
  final bool isClosed;

  /// «HH:mm». Закрытие раньше открытия — ночная смена до утра следующего дня: у клубов это
  /// обычное расписание, а не опечатка.
  final String? openTime;
  final String? closeTime;

  factory OpeningDay.fromJson(Map<String, dynamic> json) => OpeningDay(
        dayOfWeek: (json['dayOfWeek'] as num).toInt(),
        isClosed: json['isClosed'] as bool? ?? false,
        openTime: json['openTime'] as String?,
        closeTime: json['closeTime'] as String?,
      );

  Map<String, dynamic> toJson() => {
        'dayOfWeek': dayOfWeek,
        'isClosed': isClosed,
        if (openTime != null) 'openTime': openTime,
        if (closeTime != null) 'closeTime': closeTime,
      };
}

/// Что показать про клуб прямо сейчас.
enum OpeningStatusKind { openUntil, openAllDay, opensAt, closedToday, unknown }

class OpeningStatus {
  const OpeningStatus(this.kind, [this.time]);

  final OpeningStatusKind kind;

  /// «HH:mm» — до какого часа открыто или с какого откроется. null для остальных состояний.
  final String? time;
}

/// Открыт ли клуб в момент [now] и до какого часа.
///
/// Считается по местному времени телефона: клуб и игрок в одном городе, а разбирать часовые
/// пояса ради этого — обещать точность, которой в расписании всё равно нет.
OpeningStatus openingStatusAt(List<OpeningDay> schedule, DateTime now) {
  if (schedule.isEmpty) return const OpeningStatus(OpeningStatusKind.unknown);

  final today = _dayFor(schedule, now.weekday);
  final yesterday = _dayFor(schedule, now.weekday == 1 ? 7 : now.weekday - 1);
  final minutesNow = now.hour * 60 + now.minute;

  // Вчерашняя ночная смена может ещё идти: в три часа ночи клуб открыт по вчерашней строке
  // расписания, а не по сегодняшней.
  if (yesterday != null && !yesterday.isClosed) {
    final open = _minutes(yesterday.openTime);
    final close = _minutes(yesterday.closeTime);
    if (open != null && close != null && close < open && minutesNow < close) {
      return OpeningStatus(OpeningStatusKind.openUntil, yesterday.closeTime);
    }
  }

  if (today == null) return const OpeningStatus(OpeningStatusKind.unknown);
  if (today.isClosed) return const OpeningStatus(OpeningStatusKind.closedToday);

  final open = _minutes(today.openTime);
  final close = _minutes(today.closeTime);
  if (open == null || close == null) return const OpeningStatus(OpeningStatusKind.unknown);

  // Круглые сутки: «00:00–23:59» и подобное. Называем это своими словами — «до 23:59»
  // звучит как скорое закрытие.
  if (open == 0 && close >= 23 * 60 + 59) {
    return const OpeningStatus(OpeningStatusKind.openAllDay);
  }

  final worksPastMidnight = close < open;
  final isOpen = worksPastMidnight ? minutesNow >= open : minutesNow >= open && minutesNow < close;

  if (isOpen) return OpeningStatus(OpeningStatusKind.openUntil, today.closeTime);
  if (minutesNow < open) return OpeningStatus(OpeningStatusKind.opensAt, today.openTime);

  // Сегодня уже закрылись — ближайшее открытие завтра, и его час честнее, чем «закрыто».
  final tomorrow = _dayFor(schedule, now.weekday == 7 ? 1 : now.weekday + 1);
  return tomorrow == null || tomorrow.isClosed
      ? const OpeningStatus(OpeningStatusKind.closedToday)
      : OpeningStatus(OpeningStatusKind.opensAt, tomorrow.openTime);
}

OpeningDay? _dayFor(List<OpeningDay> schedule, int weekday) {
  for (final day in schedule) {
    if (day.dayOfWeek == weekday) return day;
  }
  return null;
}

int? _minutes(String? time) {
  if (time == null) return null;
  final parts = time.split(':');
  if (parts.length != 2) return null;
  final hours = int.tryParse(parts[0]);
  final minutes = int.tryParse(parts[1]);
  if (hours == null || minutes == null) return null;
  return hours * 60 + minutes;
}

/// Что сказать про часы зала прямо сейчас: одна фраза и открыт ли он.
///
/// Живёт рядом с расчётом расписания, потому что и витрина, и подробности клуба задают часам
/// один и тот же вопрос — и обязаны отвечать на него одинаковыми словами.
///
/// null — расписания нет или оно записано не по формату. Придумать за клуб часы работы значит
/// отправить игрока к закрытой двери, поэтому в этом случае лучше промолчать.
({String text, bool open})? openingNowLabel(L l, List<OpeningDay> schedule, DateTime now) {
  if (schedule.isEmpty) return null;

  final status = openingStatusAt(schedule, now);
  return switch (status.kind) {
    OpeningStatusKind.openUntil =>
      (text: l.customerClubPickerOpenUntil(status.time ?? ''), open: true),
    OpeningStatusKind.openAllDay => (text: l.customerClubPickerOpenAllDay, open: true),
    OpeningStatusKind.opensAt =>
      (text: l.customerClubPickerOpensAt(status.time ?? ''), open: false),
    OpeningStatusKind.closedToday => (text: l.customerClubPickerClosedToday, open: false),
    OpeningStatusKind.unknown => null,
  };
}
