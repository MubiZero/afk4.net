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
import 'package:afk4_customer_app/profile/profile_screen.dart';
import 'package:afk4_customer_app/organization/organization_directory.dart';

class _StubDirectory extends OrganizationDirectory {
  _StubDirectory(this.clubs) : super(baseUrl: 'https://stub');

  final List<Organization> clubs;

  @override
  Future<List<Organization>> search({String? query}) async => clubs;
}

/// Отвечает как настоящий сервер: сессия на вход, деньги и сессия на главную, пустые
/// списки на всё остальное.
class _FakeHttp extends http.BaseClient {
  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final body = switch (request.url.path) {
      '/api/me/dashboard' => _dashboardJson,
      '/api/me/profile' => _profileJson,
      '/api/me/features' => '{"features":["online_topup"]}',
      '/api/me/wallet/top-up-intents' => '[]',
      _ => _sessionJson,
    };
    return http.StreamedResponse(
      Stream.value(utf8.encode(body)),
      200,
      headers: const {'content-type': 'application/json; charset=utf-8'},
    );
  }
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

/// Пара, выданная при продлении. refresh-токен ДРУГОЙ: сервер одноразовый старый отзывает.
const _rotatedSessionJson = '''
{"playerAccountId":"p1","organizationId":"11111111-1111-1111-1111-111111111111",
 "displayName":"Иван","phoneVerified":true,
 "accessToken":"access-2","accessTokenExpiresAtUtc":"2026-08-11T13:00:00Z",
 "refreshToken":"refresh-2","refreshTokenExpiresAtUtc":"2026-09-11T12:00:00Z"}''';

const _profileJson = '''
{"playerAccountId":"p1","displayName":"Иван","phoneNumber":"+992900000000",
 "phoneVerified":true,"preferredLocale":null,"marketingOptIn":false}''';

const _dashboardJson = '''
{"walletBalance":{"currencyCode":"TJS","minorUnits":120050},
 "debtBalance":{"currencyCode":"TJS","minorUnits":0},
 "activeSession":null}''';

/// Штатный мок пакета пишет сюда же — заглядываем внутрь, чтобы проверить, что выход
/// действительно стирает сессию с устройства, а не только убирает её с экрана.
late Map<String, String> secureValues;

/// Сервер, у которого протух токен доступа. Продление либо выдаёт новую пару, либо отвергает
/// уже отозванный refresh-токен — второе и видит игрок, не заходивший сутки.
class _ExpiredTokenHttp extends http.BaseClient {
  _ExpiredTokenHttp({required this.refreshSucceeds});

  final bool refreshSucceeds;
  int refreshCalls = 0;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    if (request.url.path == '/api/public/player/refresh') {
      refreshCalls++;
      return _respond(
        refreshSucceeds ? _rotatedSessionJson : '{"error":"revoked"}',
        refreshSucceeds ? 200 : 401,
      );
    }

    final authorized = request.headers['Authorization'] == 'Bearer access-2';
    if (!authorized) return _respond('{"error":"expired"}', 401);

    return _respond(switch (request.url.path) {
      '/api/me/dashboard' => _dashboardJson,
      '/api/me/profile' => _profileJson,
      '/api/me/features' => '{"features":["online_topup"]}',
      _ => '[]',
    }, 200);
  }

  static http.StreamedResponse _respond(String body, int status) => http.StreamedResponse(
        Stream.value(utf8.encode(body)),
        status,
        headers: const {'content-type': 'application/json; charset=utf-8'},
      );
}

Widget buildApp({Locale? locale = const Locale('ru'), http.Client? httpClient}) => CustomerApp(
      locale: locale,
      directory: _StubDirectory(const [_cyberx]),
      api: PlayerApiClient(baseUrl: 'https://api', httpClient: httpClient ?? _FakeHttp()),
      sessionStore: const PlayerSessionStore(),
    );

/// Снимает дерево: у главной экрана есть опрос раз в 30 секунд, и оставленный таймер
/// валит тест как «pending timer».
Future<void> unmount(WidgetTester tester) => tester.pumpWidget(const SizedBox.shrink());

/// Выход и смена клуба живут в разделе «Профиль».
Future<void> openProfile(WidgetTester tester) async {
  await tester.tap(find.text('Профиль'));
  await tester.pumpAndSettle();
}

Future<void> signOut(WidgetTester tester) async {
  await openProfile(tester);
  // Выход стоит в самом низу профиля — на невысоком экране до него надо доскроллить, и с
  // запасом: прокрутка «до видимости» оставляет кнопку под прилипшей шапкой. Список
  // указывается явно — разделы живут в IndexedStack, и «первый Scrollable» это главная.
  final profileList = find.descendant(
    of: find.byType(ProfileScreen),
    matching: find.byType(Scrollable),
  );
  await tester.scrollUntilVisible(find.text('Выйти'), 200, scrollable: profileList);
  await tester.drag(profileList, const Offset(0, -160));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Выйти'));
  await tester.pumpAndSettle();
}

Future<void> signIn(WidgetTester tester) async {
  await tester.tap(find.text('CyberX'));
  await tester.pumpAndSettle();
  await tester.enterText(find.byType(TextField).first, '+992900000000');
  await tester.enterText(find.byType(TextField).last, 'secret');
  // Кнопка ищется по типу, а не по подписи: тест языка запускает приложение на английском.
  await tester.tap(find.byType(FilledButton));
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

    expect(find.text('Вход'), findsOneWidget);
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
    await unmount(tester);
  });

  // Продление выдаёт НОВЫЙ refresh-токен, старый сервер отзывает. Пока продлённую сессию не
  // писали на диск, там оставался отозванный токен: приложение доживало до перезапуска, а на
  // следующий день встречало игрока ошибкой, которую снимал только повторный вход.
  testWidgets('продлённый токен сохраняется на устройство, а не только в память', (tester) async {
    await tester.pumpWidget(buildApp());
    await tester.pumpAndSettle();
    await signIn(tester);
    await unmount(tester);

    // Следующий запуск: токен доступа протух, продление проходит.
    final server = _ExpiredTokenHttp(refreshSucceeds: true);
    await tester.pumpWidget(buildApp(httpClient: server));
    await tester.pumpAndSettle();

    expect(server.refreshCalls, 1);
    expect(find.text('Иван'), findsOneWidget);
    expect(secureValues.values.join(), contains('refresh-2'));
    expect(secureValues.values.join(), isNot(contains('refresh-1')));
    await unmount(tester);
  });

  // То, что игрок видел на самом деле: связь в порядке, а на экране «проверьте соединение».
  // Мёртвая сессия — это вопрос входа, и ответ на него экран входа, а не совет про интернет.
  testWidgets('отозванный токен уводит на вход, а не в ошибку соединения', (tester) async {
    await tester.pumpWidget(buildApp());
    await tester.pumpAndSettle();
    await signIn(tester);
    await unmount(tester);

    await tester.pumpWidget(buildApp(httpClient: _ExpiredTokenHttp(refreshSucceeds: false)));
    await tester.pumpAndSettle();

    expect(find.text('Вход'), findsOneWidget);
    expect(find.textContaining('соединение'), findsNothing);
    expect(secureValues, isEmpty);
  });

  // Устройство бывает общим: после выхода следующий вошедший не должен видеть чужие данные.
  testWidgets('выход стирает сохранённую сессию, а не только экран', (tester) async {
    await tester.pumpWidget(buildApp());
    await tester.pumpAndSettle();
    await signIn(tester);

    await signOut(tester);

    expect(find.text('Вход'), findsOneWidget);
    expect(secureValues, isEmpty);
  });

  // Клуб при выходе сохраняется: игрок вышел из своего аккаунта, а не сменил заведение.
  testWidgets('выход оставляет выбранный клуб', (tester) async {
    await tester.pumpWidget(buildApp());
    await tester.pumpAndSettle();
    await signIn(tester);

    await signOut(tester);

    expect(find.text('Выберите клуб'), findsNothing);
    expect(find.text('CyberX'), findsOneWidget);
  });

  testWidgets('смена клуба возвращает к выбору и убирает сессию', (tester) async {
    await tester.pumpWidget(buildApp());
    await tester.pumpAndSettle();
    await signIn(tester);
    await signOut(tester);

    await tester.tap(find.text('Сменить клуб'));
    await tester.pumpAndSettle();

    expect(find.text('Выберите клуб'), findsOneWidget);
    expect(secureValues, isEmpty);
  });

  // Язык — предпочтение, а не разовая настройка сессии: спрашивать его каждый запуск
  // означает возвращать игрока к языку телефона, который он только что отверг.
  testWidgets('выбранный язык переживает перезапуск приложения', (tester) async {
    await tester.pumpWidget(buildApp(locale: null));
    await tester.pumpAndSettle();
    await signIn(tester);

    await tester.tap(find.text('Profile'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Русский'));
    await tester.pumpAndSettle();
    expect(find.text('Профиль'), findsWidgets);

    await tester.pumpWidget(const SizedBox());
    await tester.pumpWidget(buildApp(locale: null));
    await tester.pumpAndSettle();

    expect(find.text('Главная'), findsOneWidget);
    await unmount(tester);
  });
}
