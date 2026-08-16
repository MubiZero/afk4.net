import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/auth/player_session.dart';

String sessionJson({String access = 'access-1', String refresh = 'refresh-1'}) => jsonEncode({
      'playerAccountId': 'p1',
      'organizationId': 'o1',
      'displayName': 'Иван',
      'phoneVerified': true,
      'accessToken': access,
      'accessTokenExpiresAtUtc': '2026-08-11T12:00:00Z',
      'refreshToken': refresh,
      'refreshTokenExpiresAtUtc': '2026-09-11T12:00:00Z',
    });

class _RecordingClient extends http.BaseClient {
  _RecordingClient(this.responder);

  final http.Response Function(http.Request request) responder;
  final List<http.Request> requests = [];

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final typed = request as http.Request;
    requests.add(typed);
    final response = responder(typed);
    return http.StreamedResponse(
      Stream.value(utf8.encode(response.body)),
      response.statusCode,
      headers: response.headers,
    );
  }
}

PlayerSession theSession({String access = 'access-1', String refresh = 'refresh-1'}) =>
    PlayerSession.fromJson(jsonDecode(sessionJson(access: access, refresh: refresh)) as Map<String, dynamic>);

void main() {
  test('вход отдаёт сессию и запоминает её в клиенте', () async {
    final http = _RecordingClient((_) => makeResponse(sessionJson()));
    final client = PlayerApiClient(baseUrl: 'https://api', httpClient: http);

    final session = await client.signIn(organizationId: 'o1', phoneNumber: '+992900000000', password: 'pw');

    expect(session.displayName, 'Иван');
    expect(client.session, isNotNull);
    expect(http.requests.single.url.path, '/api/public/player/sign-in');
  });

  test('неверный пароль поднимается как ошибка с кодом, а не как пустой ответ', () async {
    final http = _RecordingClient((_) => makeResponse('{"error":"invalid"}', status: 401));
    final client = PlayerApiClient(baseUrl: 'https://api', httpClient: http);

    await expectLater(
      client.signIn(organizationId: 'o1', phoneNumber: '+992900000000', password: 'wrong'),
      throwsA(isA<PlayerApiException>().having((e) => e.statusCode, 'statusCode', 401)),
    );
  });

  // Перенос правила из веб-версии: на 401 клиент продлевает токен ОДИН раз и повторяет запрос.
  test('истёкший токен продлевается один раз, и запрос повторяется новым токеном', () async {
    var call = 0;
    final http = _RecordingClient((request) {
      call++;
      if (call == 1) return makeResponse('{"error":"expired"}', status: 401);
      if (request.url.path == '/api/public/player/refresh') {
        return makeResponse(sessionJson(access: 'access-2', refresh: 'refresh-2'));
      }
      return makeResponse('{"ok":true}');
    });
    final client = PlayerApiClient(baseUrl: 'https://api', httpClient: http, session: theSession());

    final body = await client.getJson('/api/me/dashboard');

    expect(body['ok'], isTrue);
    expect(client.session!.accessToken, 'access-2');
    expect(http.requests, hasLength(3));
    expect(http.requests.last.headers['Authorization'], 'Bearer access-2');
  });

  // Если продление не удалось, дальше пытаться некуда: сессия сбрасывается, приложение
  // возвращает игрока ко входу. Молча оставить мёртвый токен — значит показывать ошибки вечно.
  test('провал продления сбрасывает сессию и сообщает наверх', () async {
    PlayerSession? observed = theSession();
    final http = _RecordingClient((request) => request.url.path == '/api/public/player/refresh'
        ? makeResponse('{"error":"expired"}', status: 401)
        : makeResponse('{"error":"expired"}', status: 401));
    final client = PlayerApiClient(
      baseUrl: 'https://api',
      httpClient: http,
      session: theSession(),
      onSessionChanged: (next) => observed = next,
    );

    await expectLater(client.getJson('/api/me/dashboard'), throwsA(isA<PlayerApiException>()));
    expect(client.session, isNull);
    expect(observed, isNull);
  });

  test('продление пробуется ровно один раз — не бесконечный цикл на упорной 401', () async {
    var refreshCalls = 0;
    final http = _RecordingClient((request) {
      if (request.url.path == '/api/public/player/refresh') {
        refreshCalls++;
        return makeResponse(sessionJson(access: 'access-2'));
      }
      return makeResponse('{"error":"expired"}', status: 401);
    });
    final client = PlayerApiClient(baseUrl: 'https://api', httpClient: http, session: theSession());

    await expectLater(client.getJson('/api/me/dashboard'), throwsA(isA<PlayerApiException>()));
    expect(refreshCalls, 1);
  });

  // Refresh-токен одноразовый: продлив его, сервер помечает старый отозванным. Главный экран
  // открывается несколькими запросами сразу, и через час после входа все они получают 401
  // одновременно. Пока продление шло у каждого своё, первый выигрывал, а остальные предъявляли
  // уже отозванный токен, получали отказ и сносили только что выданную сессию — игрок видел
  // «проверьте соединение» на живой связи, и лечил это только повторным входом.
  test('параллельные 401 продлевают токен один раз, а не наперегонки', () async {
    var refreshCalls = 0;
    final http = _RecordingClient((request) {
      if (request.url.path == '/api/public/player/refresh') {
        refreshCalls++;
        // Второе продление тем же токеном сервер отвергает — он уже отозван.
        return refreshCalls == 1
            ? makeResponse(sessionJson(access: 'access-2', refresh: 'refresh-2'))
            : makeResponse('{"error":"revoked"}', status: 401);
      }
      return request.headers['Authorization'] == 'Bearer access-2'
          ? makeResponse('{"ok":true}')
          : makeResponse('{"error":"expired"}', status: 401);
    });
    final client = PlayerApiClient(baseUrl: 'https://api', httpClient: http, session: theSession());

    final results = await Future.wait([
      client.getJson('/api/me/dashboard'),
      client.getJson('/api/me/profile'),
      client.getJson('/api/me/reviews/pending'),
    ]);

    expect(refreshCalls, 1);
    expect(results.every((body) => body['ok'] == true), isTrue);
    expect(client.session, isNotNull);
    expect(client.session!.accessToken, 'access-2');
  });

  // Продление выдаёт НОВЫЙ refresh-токен, и о нём обязан узнать тот, кто хранит сессию на
  // диске. Пока об этом не сообщали, на диске оставался отозванный токен: приложение работало
  // до перезапуска, а наутро встречало игрока ошибкой.
  test('продление сообщает наверх новую сессию, а не только меняет её в памяти', () async {
    final observed = <PlayerSession?>[];
    var call = 0;
    final http = _RecordingClient((request) {
      call++;
      if (call == 1) return makeResponse('{"error":"expired"}', status: 401);
      if (request.url.path == '/api/public/player/refresh') {
        return makeResponse(sessionJson(access: 'access-2', refresh: 'refresh-2'));
      }
      return makeResponse('{"ok":true}');
    });
    final client = PlayerApiClient(baseUrl: 'https://api', httpClient: http, session: theSession());
    client.onSessionChanged = observed.add;

    await client.getJson('/api/me/dashboard');

    expect(observed, hasLength(1));
    expect(observed.single!.refreshToken, 'refresh-2');
  });

  // Правило из веб-версии: обновление сессии НЕ пересоздаёт клиента. Иначе дерево экранов
  // перемонтируется и опросы стартуют заново — на главной это видно как мигание живой сессии.
  test('смена сессии не меняет саму личность клиента', () async {
    final http = _RecordingClient((_) => makeResponse(sessionJson()));
    final client = PlayerApiClient(baseUrl: 'https://api', httpClient: http);
    final identity = identityHashCode(client);

    await client.signIn(organizationId: 'o1', phoneNumber: '+992900000000', password: 'pw');
    client.updateSession(null);

    expect(identityHashCode(client), identity);
    expect(client.session, isNull);
  });

  test('запрос без сессии идёт без заголовка авторизации', () async {
    final http = _RecordingClient((_) => makeResponse('{"ok":true}'));
    final client = PlayerApiClient(baseUrl: 'https://api', httpClient: http);

    await client.getJson('/api/public/thing');

    expect(http.requests.single.headers.containsKey('Authorization'), isFalse);
  });
}

http.Response makeResponse(String body, {int status = 200}) =>
    http.Response(body, status, headers: {'content-type': 'application/json; charset=utf-8'});
