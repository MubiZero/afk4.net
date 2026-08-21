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
  // Дверь одна на всех и клуба не называет: аккаунт принадлежит человеку, а не заведению.
  test('вход по коду отдаёт сессию и запоминает её в клиенте', () async {
    final http = _RecordingClient((_) => makeResponse(sessionJson()));
    final client = PlayerApiClient(baseUrl: 'https://api', httpClient: http);

    final session = await client.confirmSignIn(phoneNumber: '+992900000000', code: '123456');

    expect(session.displayName, 'Иван');
    expect(client.session, isNotNull);
    expect(http.requests.single.url.path, '/api/public/register/confirm');
    expect(jsonDecode(http.requests.single.body), isNot(contains('organizationId')));
  });

  test('просьба прислать код идёт на общую дверь и не называет клуб', () async {
    final http = _RecordingClient(
        (_) => makeResponse('{"expiresInSeconds":300,"resendAfterSeconds":60}'));
    final client = PlayerApiClient(baseUrl: 'https://api', httpClient: http);

    await client.startSignIn('+992900000000');

    expect(http.requests.single.url.path, '/api/public/register/start');
    expect(jsonDecode(http.requests.single.body), {'phoneNumber': '+992900000000'});
  });

  test('неверный код поднимается как ошибка с кодом, а не как пустой ответ', () async {
    final http = _RecordingClient((_) => makeResponse('{"error":"invalid_code"}', status: 400));
    final client = PlayerApiClient(baseUrl: 'https://api', httpClient: http);

    await expectLater(
      client.confirmSignIn(phoneNumber: '+992900000000', code: '000000'),
      throwsA(isA<PlayerApiException>().having((e) => e.statusCode, 'statusCode', 400)),
    );
  });

  // Один аккаунт на все клубы: без этого заголовка сервер не знает, чей кошелёк показывать.
  test('выбранный клуб едет заголовком на своих маршрутах', () async {
    final http = _RecordingClient((_) => makeResponse('{"ok":true}'));
    final client = PlayerApiClient(baseUrl: 'https://api', httpClient: http, session: theSession())
      ..organizationId = 'o7';

    await client.getJson('/api/me/dashboard');

    expect(http.requests.single.headers['X-AFK4-Organization'], 'o7');
  });

  test('публичная дверь заголовок клуба не несёт — там ещё нет ни клуба, ни игрока', () async {
    final http = _RecordingClient(
        (_) => makeResponse('{"expiresInSeconds":300,"resendAfterSeconds":60}'));
    final client = PlayerApiClient(baseUrl: 'https://api', httpClient: http)
      ..organizationId = 'o7';

    await client.startSignIn('+992900000000');

    expect(http.requests.single.headers.containsKey('X-AFK4-Organization'), isFalse);
  });

  // 409 club_not_selected — это «счёта здесь ещё нет», а не сбой: экран показывает по нему
  // приглашение, а не сообщение об ошибке.
  test('«счёта в клубе нет» отличается от прочих отказов', () async {
    final http = _RecordingClient((_) => makeResponse('{"error":"club_not_selected"}', status: 409));
    final client = PlayerApiClient(baseUrl: 'https://api', httpClient: http, session: theSession());

    await expectLater(
      client.getReservations(),
      throwsA(isA<PlayerApiException>().having((e) => e.isNoAccountInClub, 'isNoAccountInClub', isTrue)),
    );
  });

  test('PIN уходит на маршрут личности и не ждёт тела в ответ', () async {
    final http = _RecordingClient((_) => makeResponse('', status: 204));
    final client = PlayerApiClient(baseUrl: 'https://api', httpClient: http, session: theSession());

    await client.setPin('4321');

    expect(http.requests.single.url.path, '/api/me/pin');
    expect(http.requests.single.method, 'PUT');
    expect(jsonDecode(http.requests.single.body), {'pin': '4321'});
  });

  test('короткий PIN поднимается ошибкой сервера, а не молча проглатывается', () async {
    final http = _RecordingClient((_) => makeResponse('{"error":"invalid_pin"}', status: 400));
    final client = PlayerApiClient(baseUrl: 'https://api', httpClient: http, session: theSession());

    await expectLater(
      client.setPin('12'),
      throwsA(isA<PlayerApiException>().having((e) => e.statusCode, 'statusCode', 400)),
    );
  });

  // Общей суммы денег у человека нет: у каждого клуба своя касса.
  test('«кто я» разбирается в личность и список клубов со своими деньгами', () async {
    final http = _RecordingClient((_) => makeResponse(jsonEncode({
          'person': {
            'platformPersonId': 'pp1',
            'phoneNumber': '+992900000000',
            'displayName': 'Иван',
            'preferredLocale': 'ru',
            'phoneVerified': true,
            'pinSet': false,
            'networkBanned': false,
          },
          'clubs': [
            {
              'organizationId': 'o1',
              'organizationName': 'CyberX',
              'playerAccountId': 'p1',
              'homeBranchId': 'b1',
              'currencyCode': 'TJS',
              'walletBalanceMinorUnits': 12000,
              'heldMinorUnits': 5000,
              'debtMinorUnits': 0,
              'visitCount': 3,
            },
          ],
        })));
    final client = PlayerApiClient(baseUrl: 'https://api', httpClient: http, session: theSession());

    final me = await client.getMe();

    expect(me.person.pinSet, isFalse);
    expect(me.clubs.single.heldBalance.minorUnits, 5000);
    expect(me.clubAt('o1'), isNotNull);
    expect(me.clubAt('o2'), isNull);
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

    await client.confirmSignIn(phoneNumber: '+992900000000', code: '123456');
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
