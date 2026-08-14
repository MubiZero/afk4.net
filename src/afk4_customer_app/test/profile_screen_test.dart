import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/profile/profile_screen.dart';

import 'package:afk4_customer_app/theme/brand_mark.dart';

import 'support/fake_http.dart';

String _profileJson({
  String name = 'Иван',
  String? phone = '+992900000000',
  String? locale,
  bool marketing = false,
  bool phoneVerified = true,
}) =>
    jsonEncode({
      'playerAccountId': 'p1',
      'displayName': name,
      'phoneNumber': phone,
      'phoneVerified': phoneVerified,
      'preferredLocale': locale,
      'marketingOptIn': marketing,
    });

Widget harness(
  FakeHttpClient http, {
  VoidCallback? onSignOut,
  VoidCallback? onChangeClub,
  ValueChanged<Locale>? onLocaleChanged,
  Locale locale = const Locale('ru'),
}) =>
    MaterialApp(
      locale: locale,
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: ProfileScreen(
        api: PlayerApiClient(baseUrl: 'https://api', httpClient: http),
        onSignOut: onSignOut ?? () {},
        onChangeClub: onChangeClub ?? () {},
        onLocaleChanged: onLocaleChanged ?? (_) {},
      ),
    );

void main() {
  // От подтверждённости зависят пополнение и брони — профиль обязан показывать состояние,
  // а не только сам номер.
  testWidgets('неподтверждённый номер назван неподтверждённым и зовёт подтвердить', (tester) async {
    await tester.pumpWidget(
        harness(FakeHttpClient((_) => (_profileJson(phoneVerified: false), 200))));
    await tester.pumpAndSettle();

    expect(find.textContaining('Номер не подтверждён'), findsOneWidget);

    await tester.tap(find.text('Подтвердить номер'));
    await tester.pumpAndSettle();

    expect(find.text('Подтверждение номера'), findsOneWidget);
  });

  // Смена номера — та же процедура: код уходит на новый номер и доказывает владение им.
  testWidgets('подтверждённый номер можно сменить отсюда же', (tester) async {
    await tester.pumpWidget(harness(FakeHttpClient((_) => (_profileJson(), 200))));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Изменить номер'));
    await tester.pumpAndSettle();

    expect(find.text('Подтверждение номера'), findsOneWidget);
    expect(find.text('+992900000000'), findsWidgets);
  });

  testWidgets('показывает имя и телефон игрока', (tester) async {
    await tester.pumpWidget(harness(FakeHttpClient((_) => (_profileJson(), 200))));
    await tester.pumpAndSettle();

    expect(find.text('Иван'), findsOneWidget);
    expect(find.text('+992900000000'), findsOneWidget);
  });

  // Веб оставлял вечный скелет: непонятно, грузится или сломалось.
  testWidgets('сбой загрузки виден и лечится повтором', (tester) async {
    var attempt = 0;
    final http = FakeHttpClient((_) =>
        ++attempt == 1 ? ('{"error":"boom"}', 500) : (_profileJson(), 200));
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();
    expect(find.text('Не удалось загрузить профиль.'), findsOneWidget);

    await tester.tap(find.text('Повторить'));
    await tester.pumpAndSettle();

    expect(find.text('Иван'), findsOneWidget);
  });

  // Веб предлагал только русский и английский — в Таджикистане это теряет часть аудитории.
  testWidgets('таджикский предлагается наравне с остальными языками', (tester) async {
    await tester.pumpWidget(harness(FakeHttpClient((_) => (_profileJson(), 200))));
    await tester.pumpAndSettle();

    expect(find.text('Таджикский'), findsOneWidget);
    expect(find.text('Русский'), findsOneWidget);
    expect(find.text('English'), findsOneWidget);
  });

  testWidgets('выбор языка применяется сразу и уходит на сервер', (tester) async {
    Locale? applied;
    final http = FakeHttpClient((_) => (_profileJson(locale: 'tg'), 200));
    await tester.pumpWidget(harness(http, onLocaleChanged: (locale) => applied = locale));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Таджикский'));
    await tester.pumpAndSettle();

    expect(applied, const Locale('tg'));
    expect(http.bodies.single, {'preferredLocale': 'tg'});
    expect(find.text('Сохранено'), findsOneWidget);
  });

  // Язык — настройка интерфейса: заминка в сети не повод показывать игроку чужой язык.
  testWidgets('язык применяется даже когда сервер не ответил', (tester) async {
    Locale? applied;
    final http = FakeHttpClient((request) =>
        request.method == 'GET' ? (_profileJson(), 200) : ('{"error":"boom"}', 500));
    await tester.pumpWidget(harness(http, onLocaleChanged: (locale) => applied = locale));
    await tester.pumpAndSettle();

    await tester.tap(find.text('English'));
    await tester.pumpAndSettle();

    expect(applied, const Locale('en'));
    expect(find.text('Не удалось сохранить'), findsOneWidget);
  });

  testWidgets('согласие на рассылку шлёт только изменённое поле', (tester) async {
    final http = FakeHttpClient((request) =>
        (_profileJson(marketing: request.method != 'GET'), 200));
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();

    await tester.tap(find.byType(Switch));
    await tester.pumpAndSettle();

    expect(http.bodies.single, {'marketingOptIn': true});
  });

  testWidgets('выход и смена клуба зовут свои обработчики', (tester) async {
    var signedOut = false;
    var changedClub = false;
    await tester.pumpWidget(harness(
      FakeHttpClient((_) => (_profileJson(), 200)),
      onSignOut: () => signedOut = true,
      onChangeClub: () => changedClub = true,
    ));
    await tester.pumpAndSettle();

    // Выходы стоят в самом низу профиля: на невысоком экране до них надо доскроллить, и с
    // запасом — прокрутка «до видимости» оставляет кнопку под прилипшей шапкой.
    await tester.drag(find.byType(CustomScrollView), const Offset(0, -400));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Выйти'));
    await tester.tap(find.text('Сменить клуб'));
    await tester.pump();

    expect(signedOut, isTrue);
    expect(changedClub, isTrue);
  });

  // Приложение носит цвет и знак клуба — игрок пришёл к нему. Наш знак стоит в самом низу
  // настроек: подпись платформы, которая никому не мешает и никого не путает.
  testWidgets('знак платформы стоит в конце профиля', (tester) async {
    await tester.pumpWidget(harness(FakeHttpClient((_) => (_profileJson(), 200))));
    await tester.pumpAndSettle();

    await tester.drag(find.byType(CustomScrollView), const Offset(0, -400));
    await tester.pumpAndSettle();

    expect(find.byType(BrandMark), findsOneWidget);
    expect(find.text('Работает на AFK4.NET'), findsOneWidget);
  });
}
