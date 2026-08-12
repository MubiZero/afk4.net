import 'package:intl/intl.dart';

import '../l10n/app_localizations.dart';

/// День и время строкой для списков и чеков. Часовой пояс — устройства: игрок смотрит
/// историю там же, где играл.
String formatDateTime(DateTime value, String locale) =>
    DateFormat.MMMd(dateLocale(locale)).add_Hm().format(value.toLocal());

/// `intl` не знает таджикского и на нём падает — тот же откат на русский, что и у сумм.
String dateLocale(String locale) =>
    Intl.verifiedLocale(locale, DateFormat.localeExists, onFailure: (_) => 'ru')!;

/// Сколько длился визит, с точностью до минуты. Незакрытый визит считается по текущий момент.
/// Единицы берутся из каталога строк: «ч» и «мин» на английском интерфейсе выглядят так же
/// нелепо, как «h» и «min» на русском.
String formatVisitDuration(L l, DateTime start, DateTime? end, {DateTime? now}) {
  final finish = end ?? now ?? DateTime.now();
  final minutes = finish.difference(start).inMinutes;
  final total = minutes < 0 ? 0 : minutes;
  final hours = total ~/ 60;
  return hours > 0
      ? l.customerHistoryDurationHoursMinutes('$hours', '${total % 60}')
      : l.customerHistoryDurationMinutes('$total');
}
