import 'dart:async';
import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/auth/player_session.dart';
import 'package:afk4_customer_app/push/push_service.dart';
import 'package:afk4_customer_app/push/push_tokens.dart';

/// Запоминает запросы к серверу и отвечает как договорено: регистрация и снятие устройства
/// возвращают пустой 204.
class _RecordingClient extends http.BaseClient {
  _RecordingClient({this.status = 204});

  final int status;
  final List<({String method, String path, Map<String, dynamic>? body})> calls = [];

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final body = request is http.Request && request.body.isNotEmpty
        ? jsonDecode(request.body) as Map<String, dynamic>
        : null;
    calls.add((method: request.method, path: request.url.path, body: body));
    return http.StreamedResponse(const Stream<List<int>>.empty(), status);
  }
}

class _StubTokens implements PushTokens {
  _StubTokens({this.isSupported = true, this.granted = true});

  @override
  final bool isSupported;

  bool granted;

  /// Токен устройства. Меняется в тестах присваиванием — так же, как его меняет Firebase.
  String? currentToken = 'token-1';

  final StreamController<String> refresh = StreamController<String>.broadcast();

  @override
  String get platform => 'android';

  @override
  Future<bool> requestPermission() async => granted;

  @override
  Future<String?> token() async => currentToken;

  @override
  Stream<String> get onTokenRefresh => refresh.stream;
}

PlayerApiClient buildApi(http.Client client) {
  final api = PlayerApiClient(baseUrl: 'https://api.test', httpClient: client);
  api.updateSession(PlayerSession(
    playerAccountId: 'p1',
    organizationId: 'o1',
    displayName: 'Иван',
    phoneVerified: true,
    accessToken: 'access',
    accessTokenExpiresAtUtc: DateTime.utc(2030),
    refreshToken: 'refresh',
    refreshTokenExpiresAtUtc: DateTime.utc(2030),
  ));
  return api;
}

void main() {
  setUp(() => SharedPreferences.setMockInitialValues({}));

  test('вход регистрирует устройство на сервере', () async {
    final client = _RecordingClient();
    final tokens = _StubTokens();
    final push = PushService(api: buildApi(client), tokens: tokens, locale: 'ru');

    await push.syncAfterSignIn();

    final call = client.calls.single;
    expect(call.method, 'POST');
    expect(call.path, '/api/me/devices');
    expect(call.body, {'pushToken': 'token-1', 'platform': 'android', 'locale': 'ru'});
  });

  /// Отказ системе — это ответ игрока, а не сбой: запоминаем его, чтобы переключатель
  /// в настройках показывал правду.
  test('отказ в разрешении не регистрирует устройство и запоминается', () async {
    final client = _RecordingClient();
    final push = PushService(api: buildApi(client), tokens: _StubTokens(granted: false));

    await push.syncAfterSignIn();

    expect(client.calls, isEmpty);
    expect(await push.isEnabled(), isFalse);
  });

  /// Выход из аккаунта: токен снимается, иначе уведомления придут прежнему игроку — на том
  /// же телефоне.
  test('выход снимает устройство', () async {
    final client = _RecordingClient();
    final push = PushService(api: buildApi(client), tokens: _StubTokens());
    await push.syncAfterSignIn();

    await push.clearOnSignOut();

    expect(client.calls.last.method, 'DELETE');
    expect(client.calls.last.path, '/api/me/devices/token-1');
  });

  test('выключение в настройках снимает устройство и запоминается', () async {
    final client = _RecordingClient();
    final push = PushService(api: buildApi(client), tokens: _StubTokens());
    await push.enable();

    await push.disable();

    expect(await push.isEnabled(), isFalse);
    expect(client.calls.last.method, 'DELETE');
  });

  /// Выключенные уведомления не должны включаться сами при следующем входе.
  test('после выключения вход не регистрирует устройство заново', () async {
    final client = _RecordingClient();
    final tokens = _StubTokens();
    final push = PushService(api: buildApi(client), tokens: tokens);
    await push.enable();
    await push.disable();
    client.calls.clear();

    await push.syncAfterSignIn();

    expect(client.calls, isEmpty);
  });

  test('включение обратно снова регистрирует устройство', () async {
    final client = _RecordingClient();
    final push = PushService(api: buildApi(client), tokens: _StubTokens());
    await push.disable();

    final enabled = await push.enable();

    expect(enabled, isTrue);
    expect(client.calls.last.method, 'POST');
    expect(await push.isEnabled(), isTrue);
  });

  /// Firebase меняет токен сам. Не сообщить новый — значит однажды молча перестать
  /// получать уведомления.
  test('смена токена уходит на сервер', () async {
    final client = _RecordingClient();
    final tokens = _StubTokens();
    final push = PushService(api: buildApi(client), tokens: tokens);
    await push.syncAfterSignIn();

    tokens.refresh.add('token-2');
    await Future<void>.delayed(Duration.zero);

    expect(client.calls.last.body?['pushToken'], 'token-2');
  });

  /// Сервер недоступен — экран, на котором игрок только что вошёл, падать не должен.
  test('ошибка сервера при регистрации не бросает исключение', () async {
    final push = PushService(api: buildApi(_RecordingClient(status: 500)), tokens: _StubTokens());

    await expectLater(push.syncAfterSignIn(), completes);
  });

  test('без поддержки платформы ничего не регистрируется', () async {
    final client = _RecordingClient();
    final push = PushService(api: buildApi(client), tokens: _StubTokens(isSupported: false));

    await push.syncAfterSignIn();

    expect(push.isSupported, isFalse);
    expect(client.calls, isEmpty);
  });
}
