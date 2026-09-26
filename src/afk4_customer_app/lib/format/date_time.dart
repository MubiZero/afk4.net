import 'package:intl/intl.dart';

import '../l10n/app_localizations.dart';

/// День и время строкой для списков, чеков и броней. Часовой пояс — устройства: игрок
/// смотрит историю там же, где играл.
///
/// Соседние дни называются словами: «12 авг.» заставляет считать, какой сегодня день, а
/// «Сегодня» и «Завтра» — самые частые случаи и в истории, и в бронях. Даты не этого года
/// показываются с годом: без него декабрь позапрошлого выглядел так же, как декабрь этого.
String formatDateTime(L l, DateTime value, String locale, {DateTime? now}) {
  final local = value.toLocal();
  final today = _dayOnly(now?.toLocal() ?? DateTime.now());
  final day = _dayOnly(local);
  final time = DateFormat.Hm(dateLocale(locale)).format(local);

  final named = switch (day.difference(today).inDays) {
    0 => l.customerCommonToday,
    -1 => l.customerCommonYesterday,
    1 => l.customerCommonTomorrow,
    _ => null,
  };
  if (named != null) return l.customerCommonDayAtTime(named, time);

  return l.customerCommonDayAtTime(_monthDay(local, locale, withYear: day.year != today.year), time);
}

/// Календарная дата без времени и без «Сегодня»: день рождения, «14 март 2001». Приходит строкой
/// `yyyy-MM-dd` — часовой пояс к ней не относится.
String formatCalendarDate(String isoDate, String locale) {
  final date = DateTime.parse(isoDate);
  return _monthDay(date, locale, withYear: true);
}

/// «12 август», «12 август 2025». Для таджикского — свои названия месяцев: `intl` таджикского не
/// знает, а русские «12 авг.» в таджикском интерфейсе — чужой язык.
String _monthDay(DateTime local, String locale, {required bool withYear}) {
  if (_isTajik(locale)) {
    final day = '${local.day} ${_tajikMonths[local.month - 1]}';
    return withYear ? '$day ${local.year}' : day;
  }
  final pattern = withYear ? DateFormat.yMMMd(dateLocale(locale)) : DateFormat.MMMd(dateLocale(locale));
  return pattern.format(local);
}

bool _isTajik(String locale) => locale == 'tg' || locale.startsWith('tg_') || locale.startsWith('tg-');

// В именительном падеже, как их пишут в датах: «31 октябр 2026».
const _tajikMonths = [
  'январ', 'феврал', 'март', 'апрел', 'май', 'июн', 'июл', 'август', 'сентябр', 'октябр', 'ноябр', 'декабр',
];

/// Промежуток брони. День называется один раз: «Сегодня, 14:00 — 16:00» вместо строки,
/// где одно и то же «Сегодня» повторяется дважды.
String formatTimeRange(L l, DateTime start, DateTime end, String locale, {DateTime? now}) {
  final from = formatDateTime(l, start, locale, now: now);
  final sameDay = _dayOnly(start.toLocal()) == _dayOnly(end.toLocal());
  final to = sameDay
      ? DateFormat.Hm(dateLocale(locale)).format(end.toLocal())
      : formatDateTime(l, end, locale, now: now);
  return '$from — $to';
}

DateTime _dayOnly(DateTime value) => DateTime(value.year, value.month, value.day);

/// `intl` не знает таджикского и на нём падает — для времени откат на русский (часы и минуты —
/// цифры), а названия месяцев подставляет [_monthDay].
String dateLocale(String locale) =>
    Intl.verifiedLocale(locale, DateFormat.localeExists, onFailure: (_) => 'ru')!;

/// Сколько длился визит, с точностью до минуты. Незакрытый визит считается по текущий момент.
/// Единицы берутся из каталога строк: «ч» и «мин» на английском интерфейсе выглядят так же
/// нелепо, как «h» и «min» на русском.
String formatVisitDuration(L l, DateTime start, DateTime? end, {DateTime? now}) {
  final finish = end ?? now ?? DateTime.now();
  return formatDurationMinutes(l, finish.difference(start).inMinutes);
}

/// Длительность словами — часы и минуты. Отдельно от визита: столько же времени бывает и в
/// движении по кошельку, а «1 ч 30 мин» человек читает одинаково в обоих местах.
String formatDurationMinutes(L l, int minutes) {
  final total = minutes < 0 ? 0 : minutes;
  final hours = total ~/ 60;
  return hours > 0
      ? l.customerHistoryDurationHoursMinutes('$hours', '${total % 60}')
      : l.customerHistoryDurationMinutes('$total');
}
