import 'dart:convert';

import 'package:http/http.dart' as http;

/// Сервер в памяти для сквозного сценария.
///
/// От заглушек модульных тестов отличается тем, что помнит состояние: созданная бронь
/// попадает в список, а закрытые разделы отвечают 401 без токена. Сквозной тест иначе
/// проверял бы, что кнопки нажимаются, а не что приложение и сервер сходятся.
class FakeBackend extends http.BaseClient {
  FakeBackend({this.smsCode = '4321'});

  /// Код, который «пришёл в SMS». Любой другой отклоняется как неверный.
  final String smsCode;

  static const String organizationId = '11111111-1111-1111-1111-111111111111';
  static const String branchId = '22222222-2222-2222-2222-222222222222';

  /// Столько филиал обещает думать над заявкой — по нему в приложении идёт отсчёт.
  static const int respondWithinMinutes = 15;
  static const String accessToken = 'access-1';
  static const String playerName = 'Иван';
  static const String clubName = 'CyberX';

  /// Брони игрока. Пустые в начале сценария, пополняются его же руками.
  final List<Map<String, dynamic>> reservations = [];

  /// Запросы по порядку: `'МЕТОД /путь'`. По ним видно, куда приложение сходило и —
  /// не менее важно — куда не сходило после выхода.
  final List<String> log = [];

  /// Заголовок авторизации у каждого запроса; null — его не было.
  final List<String?> authHeaders = [];

  int _nextReservation = 1;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final path = request.url.path;
    log.add('${request.method} $path');
    authHeaders.add(request.headers['Authorization']);

    final body = request is http.Request && request.body.isNotEmpty
        ? jsonDecode(request.body) as Map<String, dynamic>
        : const <String, dynamic>{};

    final (payload, status) = _route(request.method, path, body, request.headers);
    return http.StreamedResponse(
      Stream.value(utf8.encode(payload)),
      status,
      headers: const {'content-type': 'application/json; charset=utf-8'},
    );
  }

  (String, int) _route(
    String method,
    String path,
    Map<String, dynamic> body,
    Map<String, String> headers,
  ) {
    // Закрытые разделы требуют токен по-настоящему: забытый заголовок должен валить
    // сценарий, а не тихо отдавать чужие данные.
    if (path.startsWith('/api/me') && headers['Authorization'] != 'Bearer $accessToken') {
      return ('{"error":"unauthorized"}', 401);
    }

    // Аккаунт один на всю сеть, поэтому клуб называется на каждом запросе. Сервер, который
    // молча подставит клуб за клиента, показал бы игроку чужой кошелёк.
    if (path.startsWith('/api/me') && headers['X-AFK4-Organization'] != organizationId) {
      return ('{"error":"club_not_selected"}', 409);
    }

    return switch ((method, path)) {
      ('GET', '/api/public/organizations') => (jsonEncode([_club]), 200),
      // Дверь одна и для нового человека, и для давнего — и клуба она не называет.
      ('POST', '/api/public/register/start') => (
          '{"expiresInSeconds":300,"resendAfterSeconds":60}',
          200,
        ),
      ('POST', '/api/public/register/confirm') => body['code'] == smsCode
          ? (jsonEncode(_session), 200)
          : ('{"error":"invalid_code"}', 400),
      ('GET', '/api/me') => (jsonEncode(_me), 200),
      ('GET', '/api/me/dashboard') => (jsonEncode(_dashboard), 200),
      ('GET', '/api/me/branches/$branchId/tariffs') => ('[]', 200),
      ('GET', '/api/me/branches/$branchId/booking-rules') => (jsonEncode(_bookingRules), 200),
      ('GET', '/api/me/features') => ('{"features":["online_booking","online_topup"]}', 200),
      ('GET', '/api/me/profile') => (jsonEncode(_profile), 200),
      ('GET', '/api/me/reservations') => (jsonEncode(reservations), 200),
      ('POST', '/api/me/reservations') => (jsonEncode(_createReservation(body)), 200),
      ('GET', '/api/me/visits') => ('{"items":[],"nextCursor":null}', 200),
      ('GET', '/api/me/purchases') => ('{"items":[],"nextCursor":null}', 200),
      ('GET', '/api/me/wallet/top-up-intents') => ('[]', 200),
      _ => ('{"error":"not_found"}', 404),
    };
  }

  /// Место клуб назначает потом — новая бронь приходит без него и в состоянии «ожидает».
  /// Раз филиал смотрит заявки руками, у неё есть срок ответа: молчание до него снимает
  /// заявку и возвращает деньги целиком.
  Map<String, dynamic> _createReservation(Map<String, dynamic> body) {
    final created = <String, dynamic>{
      'reservationId': 'r${_nextReservation++}',
      'seatId': null,
      'seatName': null,
      'startsAtUtc': body['startsAtUtc'],
      'endsAtUtc': body['endsAtUtc'],
      'state': 'pending',
      'note': null,
      'respondByUtc': DateTime.now()
          .toUtc()
          .add(const Duration(minutes: respondWithinMinutes))
          .toIso8601String(),
    };
    reservations.add(created);
    return created;
  }

  static const Map<String, dynamic> _club = {
    'organizationId': organizationId,
    'slug': 'cyberx',
    'name': clubName,
  };

  static const Map<String, dynamic> _session = {
    'playerAccountId': 'p1',
    'organizationId': organizationId,
    'platformPersonId': 'pp1',
    'preferredLocale': 'ru',
    'profileCompleted': true,
    'displayName': playerName,
    'phoneVerified': true,
    'accessToken': accessToken,
    'accessTokenExpiresAtUtc': '2099-01-01T00:00:00Z',
    'refreshToken': 'refresh-1',
    'refreshTokenExpiresAtUtc': '2099-01-01T00:00:00Z',
  };

  /// Кто я и где у меня счета. Общей суммы денег нет: у каждого клуба своя касса.
  static const Map<String, dynamic> _me = {
    'person': {
      'platformPersonId': 'pp1',
      'phoneNumber': '+992900000000',
      'displayName': playerName,
      'preferredLocale': 'ru',
      'phoneVerified': true,
      'pinSet': false,
      'networkBanned': false,
    },
    'clubs': [
      {
        'organizationId': organizationId,
        'organizationName': clubName,
        'playerAccountId': 'p1',
        'homeBranchId': branchId,
        'currencyCode': 'TJS',
        'walletBalanceMinorUnits': 120050,
        'heldMinorUnits': 4500,
        'debtMinorUnits': 0,
        'visitCount': 3,
      },
    ],
  };

  /// Филиал смотрит заявки руками и предоплаты с этого игрока не требует.
  static const Map<String, dynamic> _bookingRules = {
    'branchId': branchId,
    'acceptanceMode': 'manual',
    'respondWithinMinutes': respondWithinMinutes,
    'prepaymentRequired': false,
    'activeReservations': 0,
    'maxActiveReservations': null,
    'holdSeatAfterStartMinutes': 20,
  };

  static const Map<String, dynamic> _dashboard = {
    'walletBalance': {'currencyCode': 'TJS', 'minorUnits': 120050},
    'heldBalance': {'currencyCode': 'TJS', 'minorUnits': 4500},
    'debtBalance': {'currencyCode': 'TJS', 'minorUnits': 0},
    'activeSession': null,
  };

  static const Map<String, dynamic> _profile = {
    'playerAccountId': 'p1',
    'displayName': playerName,
    'phoneNumber': '+992900000000',
    'phoneVerified': true,
    'preferredLocale': null,
    'marketingOptIn': false,
    'homeBranchId': branchId,
  };
}
