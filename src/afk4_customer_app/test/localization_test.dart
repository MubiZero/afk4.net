import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:afk4_customer_app/l10n/app_localizations.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';

Widget harness(Locale locale, Widget child) => MaterialApp(
      locale: locale,
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: child,
    );

void main() {
  testWidgets('поддерживаются ровно три языка продукта', (tester) async {
    expect(
      appSupportedLocales.map((l) => l.languageCode).toSet(),
      {'ru', 'en', 'tg'},
    );
  });

  testWidgets('строка приходит на выбранном языке', (tester) async {
    for (final (locale, expected) in [
      (const Locale('ru'), 'Загрузка…'),
      (const Locale('en'), 'Loading…'),
    ]) {
      await tester.pumpWidget(harness(
        locale,
        Builder(builder: (context) => Text(L.of(context).customerCommonLoading)),
      ));
      expect(find.text(expected), findsOneWidget);
    }
  });

  // Таджикский обязан быть переводом, а не копией русского — то же правило стережёт
  // guard-тест каталога на стороне TypeScript. Здесь проверяем, что перевод доехал до Dart.
  testWidgets('таджикский доезжает и не совпадает с русским', (tester) async {
    late String tg;
    late String ru;
    await tester.pumpWidget(harness(
      const Locale('tg'),
      Builder(builder: (context) {
        tg = L.of(context).customerCommonLoading;
        return const SizedBox();
      }),
    ));
    await tester.pumpWidget(harness(
      const Locale('ru'),
      Builder(builder: (context) {
        ru = L.of(context).customerCommonLoading;
        return const SizedBox();
      }),
    ));
    expect(tg, isNotEmpty);
    expect(tg, isNot(ru));
  });
}
