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

String _sessionJson() => jsonEncode({
      'playerAccountId': 'p1',
      'organizationId': _club.organizationId,
      'displayName': 'Иван',
      'phoneVerified': true,
      'accessToken': 'access-1',
      'accessTokenExpiresAtUtc': '2026-08-11T12:00:00Z',
      'refreshToken': 'refresh-1',
      'refreshTokenExpiresAtUtc': '2026-09-11T12:00:00Z',
    });

Widget harness(PlayerApiClient api, {VoidCallback? onSignedIn, VoidCallback? onChangeClub}) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: SignInScreen(
        organization: _club,
        api: api,
        onSignedIn: onSignedIn ?? () {},
        onChangeClub: onChangeClub ?? () {},
      ),
    );

PlayerApiClient clientWith(http.Client inner) => PlayerApiClient(baseUrl: 'https://api', httpClient: inner);

void main() {
  testWidgets('показывает клуб, в который входим', (tester) async {
    await tester.pumpWidget(harness(clientWith(FakeHttpClient((_) => (_sessionJson(), 200)))));

    expect(find.text('CyberX'), findsOneWidget);
  });

  testWidgets('успешный вход сообщает наверх и шлёт выбранный клуб', (tester) async {
    var signedIn = false;
    final inner = FakeHttpClient((_) => (_sessionJson(), 200));
    await tester.pumpWidget(harness(clientWith(inner), onSignedIn: () => signedIn = true));

    await tester.enterText(find.byType(TextField).first, '+992900000000');
    await tester.enterText(find.byType(TextField).last, 'secret');
    await tester.tap(find.text('Войти'));
    await tester.pumpAndSettle();

    expect(signedIn, isTrue);
    expect(inner.bodies.single['organizationId'], _club.organizationId);
    expect(inner.bodies.single['phoneNumber'], '+992900000000');
  });

  testWidgets('номер с лишними пробелами обрезается — иначе сервер не найдёт игрока', (tester) async {
    final inner = FakeHttpClient((_) => (_sessionJson(), 200));
    await tester.pumpWidget(harness(clientWith(inner)));

    await tester.enterText(find.byType(TextField).first, '  +992900000000  ');
    await tester.enterText(find.byType(TextField).last, 'secret');
    await tester.tap(find.text('Войти'));
    await tester.pumpAndSettle();

    expect(inner.bodies.single['phoneNumber'], '+992900000000');
  });

  testWidgets('неверный пароль показывает ошибку и оставляет форму рабочей', (tester) async {
    await tester.pumpWidget(harness(clientWith(FakeHttpClient((_) => ('{"error":"no"}', 401)))));

    await tester.enterText(find.byType(TextField).first, '+992900000000');
    await tester.enterText(find.byType(TextField).last, 'wrong');
    await tester.tap(find.text('Войти'));
    await tester.pumpAndSettle();

    expect(find.text('Неверный номер или пароль'), findsOneWidget);
    expect(tester.widget<FilledButton>(find.byType(FilledButton)).onPressed, isNotNull);
  });

  // Обрыв связи и неверный пароль — разные беды. Показать «неверный пароль» при пропавшем
  // интернете значит отправить игрока менять правильный пароль.
  testWidgets('обрыв связи показывает сетевую ошибку, а не «неверный пароль»', (tester) async {
    final inner = FakeHttpClient((_) => throw const SocketExceptionStub());
    await tester.pumpWidget(harness(clientWith(inner)));

    await tester.enterText(find.byType(TextField).first, '+992900000000');
    await tester.enterText(find.byType(TextField).last, 'secret');
    await tester.tap(find.text('Войти'));
    await tester.pumpAndSettle();

    expect(find.text('Нет связи с сервером. Проверьте интернет.'), findsOneWidget);
    expect(find.text('Неверный номер или пароль'), findsNothing);
  });

  testWidgets('во время отправки кнопка заблокирована — двойной тап не шлёт два входа', (tester) async {
    var calls = 0;
    final inner = FakeHttpClient(
      (_) {
        calls++;
        return (_sessionJson(), 200);
      },
      delay: const Duration(milliseconds: 200),
    );
    await tester.pumpWidget(harness(clientWith(inner)));

    await tester.enterText(find.byType(TextField).first, '+992900000000');
    await tester.enterText(find.byType(TextField).last, 'secret');
    await tester.tap(find.text('Войти'));
    await tester.pump();

    expect(find.text('Входим…'), findsOneWidget);
    await tester.tap(find.byType(FilledButton));
    await tester.pumpAndSettle();

    expect(calls, 1);
  });

  testWidgets('«сменить клуб» доступен со входа — иначе ошибся клубом и заперт', (tester) async {
    var changed = false;
    await tester.pumpWidget(harness(
      clientWith(FakeHttpClient((_) => (_sessionJson(), 200))),
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
