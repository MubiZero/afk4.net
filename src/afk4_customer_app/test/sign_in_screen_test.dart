import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/auth/sign_in_screen.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/organization/organization.dart';

import 'support/fake_http.dart';

const _club = Organization(
  organizationId: '11111111-1111-1111-1111-111111111111',
  slug: 'cyberx',
  name: 'CyberX',
);

/// Сессия давнего игрока: имя и язык у него уже спрошены.
String _sessionJson({bool profileCompleted = true, String? organizationId}) => jsonEncode({
      'playerAccountId': ?(organizationId == null ? null : 'p1'),
      'organizationId': ?organizationId,
      'platformPersonId': 'pp1',
      'displayName': profileCompleted ? 'Иван' : '',
      'phoneVerified': true,
      'accessToken': 'access-1',
      'accessTokenExpiresAtUtc': '2026-08-11T12:00:00Z',
      'refreshToken': 'refresh-1',
      'refreshTokenExpiresAtUtc': '2026-09-11T12:00:00Z',
      'preferredLocale': null,
      'profileCompleted': profileCompleted,
    });

const String _codeSent = '{"expiresInSeconds":300,"resendAfterSeconds":60}';

Widget harness(
  PlayerApiClient api, {
  VoidCallback? onSignedIn,
  VoidCallback? onChangeClub,
  ValueChanged<Locale>? onLocaleChanged,
}) =>
    MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: SignInScreen(
        organization: _club,
        api: api,
        onSignedIn: onSignedIn ?? () {},
        onChangeClub: onChangeClub ?? () {},
        onLocaleChanged: onLocaleChanged,
      ),
    );

PlayerApiClient clientWith(http.Client inner) =>
    PlayerApiClient(baseUrl: 'https://api', httpClient: inner);

/// Первый шаг двери: номер и просьба прислать код.
Future<void> askForCode(WidgetTester tester, {String phone = '+992900000000'}) async {
  await tester.enterText(find.byType(TextField).first, phone);
  await tester.tap(find.widgetWithText(FilledButton, 'Прислать код'));
  await tester.pumpAndSettle();
}

void main() {
  // Дверь одна на всех: и тот, кто здесь впервые, и тот, кто играет третий год, называют
  // номер и получают код. Клуб в этом не участвует — аккаунт один на всю сеть.
  testWidgets('вход по коду доводится до сессии и не называет клуб', (tester) async {
    var signedIn = false;
    final inner = FakeHttpClient((request) => switch (request.url.path) {
          '/api/public/register/start' => (_codeSent, 200),
          _ => (_sessionJson(), 200),
        });
    await tester.pumpWidget(harness(clientWith(inner), onSignedIn: () => signedIn = true));

    await askForCode(tester);
    expect(find.textContaining('Код отправлен'), findsOneWidget);

    await tester.enterText(find.byType(TextField).last, '123456');
    await tester.tap(find.widgetWithText(FilledButton, 'Войти'));
    await tester.pumpAndSettle();

    expect(inner.paths, [
      '/api/public/register/start',
      '/api/public/register/confirm',
    ]);
    expect(inner.bodies.last['code'], '123456');
    expect(inner.bodies.last.containsKey('organizationId'), isFalse);
    expect(signedIn, isTrue);
  });

  // Пароля больше нет: вход по паролю на сервере не существует, и предлагать его значит
  // вести игрока в дверь, которой нет.
  testWidgets('пароль не предлагается ни первым способом, ни запасным', (tester) async {
    await tester.pumpWidget(harness(clientWith(FakeHttpClient((_) => (_codeSent, 200)))));

    expect(find.text('PIN или пароль'), findsNothing);
    expect(find.text('Войти по паролю'), findsNothing);
    expect(find.widgetWithText(FilledButton, 'Прислать код'), findsOneWidget);
  });

  // Незнакомого человека спрашивают об имени и языке — и это вся регистрация.
  testWidgets('новому человеку дверь задаёт имя и язык', (tester) async {
    var signedIn = false;
    final inner = FakeHttpClient((request) => switch (request.url.path) {
          '/api/public/register/start' => (_codeSent, 200),
          '/api/public/register/confirm' => (_sessionJson(profileCompleted: false), 200),
          _ => ('{"platformPersonId":"pp1","phoneNumber":"+992900000000","displayName":"Фаррух",'
              '"preferredLocale":"tg","phoneVerified":true,"pinSet":false,"networkBanned":false}', 200),
        });
    Locale? chosen;
    await tester.pumpWidget(harness(
      clientWith(inner),
      onSignedIn: () => signedIn = true,
      onLocaleChanged: (locale) => chosen = locale,
    ));

    await askForCode(tester);
    await tester.enterText(find.byType(TextField).last, '123456');
    await tester.tap(find.widgetWithText(FilledButton, 'Войти'));
    await tester.pumpAndSettle();

    // Пока имя не названо, наверх ещё ничего не сообщено.
    expect(signedIn, isFalse);
    expect(find.text('Как вас зовут'), findsOneWidget);

    await tester.enterText(find.byType(TextField).first, 'Фаррух');
    await tester.tap(find.text('Таджикский'));
    await tester.tap(find.widgetWithText(FilledButton, 'Продолжить'));
    await tester.pumpAndSettle();

    expect(inner.paths.last, '/api/me');
    expect(inner.bodies.last['displayName'], 'Фаррух');
    expect(inner.bodies.last['preferredLocale'], 'tg');
    expect(chosen, const Locale('tg'));
    expect(signedIn, isTrue);
  });

  // Давнего игрока тащить через форму знакомства значит не узнать его.
  testWidgets('знакомого человека форма знакомства не задерживает', (tester) async {
    var signedIn = false;
    final inner = FakeHttpClient((request) => switch (request.url.path) {
          '/api/public/register/start' => (_codeSent, 200),
          _ => (_sessionJson(organizationId: _club.organizationId), 200),
        });
    await tester.pumpWidget(harness(clientWith(inner), onSignedIn: () => signedIn = true));

    await askForCode(tester);
    await tester.enterText(find.byType(TextField).last, '123456');
    await tester.tap(find.widgetWithText(FilledButton, 'Войти'));
    await tester.pumpAndSettle();

    expect(find.text('Как вас зовут'), findsNothing);
    expect(signedIn, isTrue);
  });

  testWidgets('имя обязательно — пустое поле не отправляется на сервер', (tester) async {
    final inner = FakeHttpClient((request) => switch (request.url.path) {
          '/api/public/register/start' => (_codeSent, 200),
          _ => (_sessionJson(profileCompleted: false), 200),
        });
    await tester.pumpWidget(harness(clientWith(inner)));

    await askForCode(tester);
    await tester.enterText(find.byType(TextField).last, '123456');
    await tester.tap(find.widgetWithText(FilledButton, 'Войти'));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(FilledButton, 'Продолжить'));
    await tester.pumpAndSettle();

    expect(find.text('Введите имя'), findsOneWidget);
    expect(inner.paths, isNot(contains('/api/me')));
  });

  testWidgets('неверный код называется неверным, а не отказом входа', (tester) async {
    final inner = FakeHttpClient((request) => request.url.path == '/api/public/register/start'
        ? (_codeSent, 200)
        : ('{"error":"invalid_code"}', 400));
    await tester.pumpWidget(harness(clientWith(inner)));

    await askForCode(tester);
    await tester.enterText(find.byType(TextField).last, '000000');
    await tester.tap(find.widgetWithText(FilledButton, 'Войти'));
    await tester.pumpAndSettle();

    expect(find.text('Неверный код'), findsOneWidget);
  });

  // Кривой номер — это не «неверный код»: чинить нужно другое поле.
  testWidgets('непохожий на номер телефон назван своей причиной', (tester) async {
    final inner = FakeHttpClient((_) => ('{"error":"invalid_phone"}', 400));
    await tester.pumpWidget(harness(clientWith(inner)));

    await askForCode(tester, phone: '12');

    expect(find.text('Проверьте номер телефона'), findsOneWidget);
  });

  testWidgets('закрытый аккаунт отправляет в клуб, а не по кругу', (tester) async {
    final inner = FakeHttpClient((request) => request.url.path == '/api/public/register/start'
        ? (_codeSent, 200)
        : ('{"error":"account_disabled"}', 403));
    await tester.pumpWidget(harness(clientWith(inner)));

    await askForCode(tester);
    await tester.enterText(find.byType(TextField).last, '123456');
    await tester.tap(find.widgetWithText(FilledButton, 'Войти'));
    await tester.pumpAndSettle();

    expect(find.text('Вход в аккаунт закрыт. Обратитесь в клуб.'), findsOneWidget);
  });

  testWidgets('показывает клуб, в который входим', (tester) async {
    await tester.pumpWidget(harness(clientWith(FakeHttpClient((_) => (_codeSent, 200)))));

    expect(find.text('CyberX'), findsOneWidget);
  });

  testWidgets('номер с лишними пробелами обрезается — иначе сервер его не узнает',
      (tester) async {
    final inner = FakeHttpClient((_) => (_codeSent, 200));
    await tester.pumpWidget(harness(clientWith(inner)));

    await askForCode(tester, phone: '  +992900000000  ');

    expect(inner.bodies.single['phoneNumber'], '+992900000000');
  });

  // Обрыв связи и неверный код — разные беды. Показать «неверный код» при пропавшем
  // интернете значит отправить игрока искать SMS, которая давно пришла.
  testWidgets('обрыв связи показывает сетевую ошибку, а не «неверный код»', (tester) async {
    final inner = FakeHttpClient((_) => throw const SocketExceptionStub());
    await tester.pumpWidget(harness(clientWith(inner)));

    await askForCode(tester);

    expect(find.text('Нет связи с сервером. Проверьте интернет.'), findsOneWidget);
    expect(find.text('Неверный код'), findsNothing);
  });

  testWidgets('во время отправки кнопка заблокирована — двойной тап не шлёт два кода',
      (tester) async {
    var calls = 0;
    final inner = FakeHttpClient(
      (_) {
        calls++;
        return (_codeSent, 200);
      },
      delay: const Duration(milliseconds: 200),
    );
    await tester.pumpWidget(harness(clientWith(inner)));

    await tester.enterText(find.byType(TextField).first, '+992900000000');
    await tester.tap(find.widgetWithText(FilledButton, 'Прислать код'));
    await tester.pump();

    expect(find.text('Отправляем…'), findsOneWidget);
    await tester.tap(find.byType(FilledButton));
    await tester.pumpAndSettle();

    expect(calls, 1);
  });

  testWidgets('«сменить клуб» доступен со входа — иначе ошибся клубом и заперт', (tester) async {
    var changed = false;
    await tester.pumpWidget(harness(
      clientWith(FakeHttpClient((_) => (_codeSent, 200))),
      onChangeClub: () => changed = true,
    ));

    await tester.tap(find.text('Сменить клуб'));
    await tester.pump();

    expect(changed, isTrue);
  });
}

class SocketExceptionStub implements Exception {
  const SocketExceptionStub();
}
