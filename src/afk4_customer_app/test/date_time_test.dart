import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/format/date_time.dart';
import 'package:afk4_customer_app/l10n/app_localizations.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';

/// Строки длительности живут в каталоге, поэтому берём их через настоящий `L`.
Future<L> localizations(WidgetTester tester, Locale locale) async {
  late L result;
  await tester.pumpWidget(MaterialApp(
    locale: locale,
    localizationsDelegates: appLocalizationsDelegates,
    supportedLocales: appSupportedLocales,
    home: Builder(builder: (context) {
      result = L.of(context);
      return const SizedBox.shrink();
    }),
  ));
  return result;
}

final _start = DateTime.utc(2026, 8, 12, 10, 0, 0);

void main() {
  testWidgets('длительность больше часа показывается часами и минутами', (tester) async {
    final l = await localizations(tester, const Locale('ru'));

    expect(formatVisitDuration(l, _start, _start.add(const Duration(hours: 2, minutes: 30))), '2 ч 30 мин');
  });

  testWidgets('короткий визит показывается одними минутами', (tester) async {
    final l = await localizations(tester, const Locale('ru'));

    expect(formatVisitDuration(l, _start, _start.add(const Duration(minutes: 45))), '45 мин');
  });

  // Веб зашивал «ч» и «м» прямо в код — на английском интерфейсе визит длился «2ч 30м».
  testWidgets('единицы длительности переводятся вместе с интерфейсом', (tester) async {
    final l = await localizations(tester, const Locale('en'));

    expect(formatVisitDuration(l, _start, _start.add(const Duration(hours: 2))), '2 h 0 min');
  });

  testWidgets('незакрытый визит считается по текущий момент', (tester) async {
    final l = await localizations(tester, const Locale('ru'));

    expect(formatVisitDuration(l, _start, null, now: _start.add(const Duration(minutes: 20))), '20 мин');
  });

  testWidgets('дата показывается с временем и не падает на таджикском', (tester) async {
    final l = await localizations(tester, const Locale('tg'));

    expect(formatDateTime(l, _start, 'ru'), contains(':'));
    expect(formatDateTime(l, _start, 'tg'), formatDateTime(l, _start, 'ru'));
    expect(dateLocale('tg'), 'ru');
  });

  // «12 авг.» заставляет считать, какой сегодня день, а соседние дни — самое частое в
  // истории и в бронях.
  testWidgets('соседние дни называются словами', (tester) async {
    final l = await localizations(tester, const Locale('ru'));
    final now = DateTime(2026, 8, 12, 18, 0);

    expect(formatDateTime(l, DateTime(2026, 8, 12, 14, 30), 'ru', now: now), 'Сегодня, 14:30');
    expect(formatDateTime(l, DateTime(2026, 8, 11, 9, 5), 'ru', now: now), 'Вчера, 09:05');
    expect(formatDateTime(l, DateTime(2026, 8, 13, 20, 0), 'ru', now: now), 'Завтра, 20:00');
  });

  // Без года декабрь позапрошлого выглядел так же, как декабрь этого.
  testWidgets('давняя дата показывается с годом, а этого года — без', (tester) async {
    final l = await localizations(tester, const Locale('ru'));
    final now = DateTime(2026, 8, 12, 18, 0);

    expect(formatDateTime(l, DateTime(2026, 3, 4, 12, 0), 'ru', now: now), isNot(contains('2026')));
    expect(formatDateTime(l, DateTime(2024, 12, 31, 12, 0), 'ru', now: now), contains('2024'));
  });

  testWidgets('промежуток в один день называет день один раз', (tester) async {
    final l = await localizations(tester, const Locale('ru'));
    final now = DateTime(2026, 8, 12, 18, 0);

    expect(
      formatTimeRange(l, DateTime(2026, 8, 13, 14, 0), DateTime(2026, 8, 13, 16, 0), 'ru', now: now),
      'Завтра, 14:00 — 16:00',
    );
    expect(
      formatTimeRange(l, DateTime(2026, 8, 13, 23, 0), DateTime(2026, 8, 14, 1, 0), 'ru', now: now),
      contains('—'),
    );
  });
}
