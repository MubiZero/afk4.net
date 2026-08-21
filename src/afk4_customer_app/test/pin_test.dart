import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/dto.dart';
import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/profile/pin_sheet.dart';
import 'package:afk4_customer_app/profile/profile_screen.dart';

import 'support/fake_http.dart';

MePerson _person({bool pinSet = false}) => MePerson.fromJson({
      'platformPersonId': 'pp1',
      'phoneNumber': '+992900000000',
      'displayName': 'Иван',
      'preferredLocale': 'ru',
      'phoneVerified': true,
      'pinSet': pinSet,
      'networkBanned': false,
    });

const String _profileJson = '''
{"playerAccountId":"p1","displayName":"Иван","phoneNumber":"+992900000000",
 "phoneVerified":true,"preferredLocale":null,"marketingOptIn":false}''';

Widget sheetHarness(FakeHttpClient http, {bool pinSet = false}) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: Scaffold(
        body: PinSheet(
          api: PlayerApiClient(baseUrl: 'https://api', httpClient: http),
          pinSet: pinSet,
        ),
      ),
    );

Widget profileHarness(FakeHttpClient http, {MePerson? person}) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: ProfileScreen(
        api: PlayerApiClient(baseUrl: 'https://api', httpClient: http),
        person: person,
        onSignOut: () {},
        onChangeClub: () {},
        onLocaleChanged: (_) {},
      ),
    );

Future<void> enterPin(WidgetTester tester, String pin, String repeat) async {
  await tester.enterText(find.byType(TextField).first, pin);
  await tester.enterText(find.byType(TextField).last, repeat);
  await tester.tap(find.widgetWithText(FilledButton, 'Сохранить'));
  await tester.pumpAndSettle();
}

void main() {
  // PIN задаётся только в приложении: клуб сетевой PIN не назначает, и SMS на это не тратится.
  testWidgets('PIN уходит на маршрут личности и закрывает лист', (tester) async {
    final http = FakeHttpClient((_) => ('', 204));
    await tester.pumpWidget(sheetHarness(http));

    await enterPin(tester, '4321', '4321');

    expect(http.paths, ['/api/me/pin']);
    expect(http.bodies.single['pin'], '4321');
  });

  // Экран объясняет, зачем PIN нужен, — иначе игрок принимает его за второй пароль от
  // приложения и не задаёт вовсе.
  testWidgets('лист объясняет назначение PIN человеческими словами', (tester) async {
    await tester.pumpWidget(sheetHarness(FakeHttpClient((_) => ('', 204))));

    expect(find.text('PIN для посадки за ПК'), findsOneWidget);
    expect(find.textContaining('на экране ПК'), findsOneWidget);
    expect(find.textContaining('входите по коду из SMS'), findsOneWidget);
  });

  testWidgets('короткий PIN отклоняется до сервера', (tester) async {
    final http = FakeHttpClient((_) => ('', 204));
    await tester.pumpWidget(sheetHarness(http));

    await enterPin(tester, '12', '12');

    expect(find.text('PIN — от 4 до 8 цифр'), findsOneWidget);
    expect(http.paths, isEmpty);
  });

  // Опечатку в PIN игрок обнаружил бы только у ПК в клубе, где чинить её уже нечем.
  testWidgets('несовпадающий повтор называет своей причиной', (tester) async {
    final http = FakeHttpClient((_) => ('', 204));
    await tester.pumpWidget(sheetHarness(http));

    await enterPin(tester, '4321', '1234');

    expect(find.text('PIN не совпадает'), findsOneWidget);
    expect(http.paths, isEmpty);
  });

  testWidgets('отказ сервера не выдаётся за сохранённый PIN', (tester) async {
    await tester.pumpWidget(
        sheetHarness(FakeHttpClient((_) => (jsonEncode({'error': 'invalid_pin'}), 400))));

    await enterPin(tester, '4321', '4321');

    expect(find.text('PIN — от 4 до 8 цифр'), findsOneWidget);
  });

  // Старый PIN не спрашивается: потребовать его значило бы запереть выход тому, кто забыл.
  testWidgets('смена PIN не спрашивает старый', (tester) async {
    await tester.pumpWidget(sheetHarness(FakeHttpClient((_) => ('', 204)), pinSet: true));

    expect(find.byType(TextField), findsNWidgets(2));
    expect(find.textContaining('Забыли PIN'), findsOneWidget);
  });

  // Профиль — единственная дверь к PIN, и он же говорит, задан тот или нет.
  testWidgets('профиль зовёт задать PIN, пока его нет', (tester) async {
    await tester.pumpWidget(
        profileHarness(FakeHttpClient((_) => (_profileJson, 200)), person: _person()));
    await tester.pumpAndSettle();

    expect(find.text('PIN не задан — за ПК вас пока сажает администратор'), findsOneWidget);
    expect(find.widgetWithText(OutlinedButton, 'Задать PIN'), findsOneWidget);
  });

  testWidgets('заданный PIN профиль предлагает сменить, а не задать заново', (tester) async {
    await tester.pumpWidget(profileHarness(
      FakeHttpClient((_) => (_profileJson, 200)),
      person: _person(pinSet: true),
    ));
    await tester.pumpAndSettle();

    expect(find.text('PIN задан'), findsOneWidget);
    expect(find.widgetWithText(OutlinedButton, 'Сменить PIN'), findsOneWidget);
  });
}
