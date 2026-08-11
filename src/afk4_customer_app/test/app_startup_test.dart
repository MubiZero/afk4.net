import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/app.dart';
import 'package:afk4_customer_app/auth/player_session_store.dart';
import 'package:afk4_customer_app/organization/organization.dart';
import 'package:afk4_customer_app/organization/organization_directory.dart';

class _StubDirectory extends OrganizationDirectory {
  _StubDirectory(this.clubs) : super(baseUrl: 'https://stub');

  final List<Organization> clubs;

  @override
  Future<List<Organization>> search({String? query}) async => clubs;
}

class _FakeHttp extends http.BaseClient {
  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async => http.StreamedResponse(
        Stream.value(utf8.encode(_sessionJson)),
        200,
        headers: const {'content-type': 'application/json; charset=utf-8'},
      );
}

const _cyberx = Organization(
  organizationId: '11111111-1111-1111-1111-111111111111',
  slug: 'cyberx',
  name: 'CyberX',
);

const _sessionJson = '''
{"playerAccountId":"p1","organizationId":"11111111-1111-1111-1111-111111111111",
 "displayName":"Иван","phoneVerified":true,
 "accessToken":"access-1","accessTokenExpiresAtUtc":"2026-08-11T12:00:00Z",
 "refreshToken":"refresh-1","refreshTokenExpiresAtUtc":"2026-09-11T12:00:00Z"}''';

/// Штатный мок пакета пишет сюда же — заглядываем внутрь, чтобы проверить, что выход
/// действительно стирает сессию с устройства, а не только убирает её с экрана.
late Map<String, String> secureValues;

Widget buildApp() => CustomerApp(
      locale: const Locale('ru'),
      directory: _StubDirectory(const [_cyberx]),
      api: PlayerApiClient(baseUrl: 'https://api', httpClient: _FakeHttp()),
      sessionStore: const PlayerSessionStore(),
    );

Future<void> signIn(WidgetTester tester) async {
  await tester.tap(find.text('CyberX'));
  await tester.pumpAndSettle();
  await tester.enterText(find.byType(TextField).first, '+992900000000');
  await tester.enterText(find.byType(TextField).last, 'secret');
  await tester.tap(find.text('Войти'));
  await tester.pumpAndSettle();
}

void main() {
  setUp(() {
    SharedPreferences.setMockInitialValues({});
    secureValues = {};
    FlutterSecureStorage.setMockInitialValues(secureValues);
  });

  testWidgets('первый запуск ведёт на выбор клуба', (tester) async {
    await tester.pumpWidget(buildApp());
    await tester.pumpAndSettle();

    expect(find.text('Выберите клуб'), findsOneWidget);
  });

  testWidgets('после выбора клуба показывается вход именно в него', (tester) async {
    await tester.pumpWidget(buildApp());
    await tester.pumpAndSettle();

    await tester.tap(find.text('CyberX'));
    await tester.pumpAndSettle();

    expect(find.text('Вход в портал'), findsOneWidget);
    expect(find.text('CyberX'), findsOneWidget);
  });

  // Спрашивать клуб и пароль при каждом запуске — то же, что выкидывать игрока из аккаунта.
  testWidgets('вход переживает перезапуск приложения', (tester) async {
    await tester.pumpWidget(buildApp());
    await tester.pumpAndSettle();
    await signIn(tester);
    expect(find.text('Иван'), findsOneWidget);

    await tester.pumpWidget(const SizedBox());
    await tester.pumpWidget(buildApp());
    await tester.pumpAndSettle();

    expect(find.text('Иван'), findsOneWidget);
    expect(find.text('Выберите клуб'), findsNothing);
  });

  // Устройство бывает общим: после выхода следующий вошедший не должен видеть чужие данные.
  testWidgets('выход стирает сохранённую сессию, а не только экран', (tester) async {
    await tester.pumpWidget(buildApp());
    await tester.pumpAndSettle();
    await signIn(tester);

    await tester.tap(find.text('Выйти'));
    await tester.pumpAndSettle();

    expect(find.text('Вход в портал'), findsOneWidget);
    expect(secureValues, isEmpty);
  });

  // Клуб при выходе сохраняется: игрок вышел из своего аккаунта, а не сменил заведение.
  testWidgets('выход оставляет выбранный клуб', (tester) async {
    await tester.pumpWidget(buildApp());
    await tester.pumpAndSettle();
    await signIn(tester);

    await tester.tap(find.text('Выйти'));
    await tester.pumpAndSettle();

    expect(find.text('Выберите клуб'), findsNothing);
    expect(find.text('CyberX'), findsOneWidget);
  });

  testWidgets('смена клуба возвращает к выбору и убирает сессию', (tester) async {
    await tester.pumpWidget(buildApp());
    await tester.pumpAndSettle();
    await signIn(tester);
    await tester.tap(find.text('Выйти'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Сменить клуб'));
    await tester.pumpAndSettle();

    expect(find.text('Выберите клуб'), findsOneWidget);
    expect(secureValues, isEmpty);
  });
}
