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
    await localizations(tester, const Locale('tg'));

    expect(formatDateTime(_start, 'ru'), contains(':'));
    expect(formatDateTime(_start, 'tg'), formatDateTime(_start, 'ru'));
    expect(dateLocale('tg'), 'ru');
  });
}
