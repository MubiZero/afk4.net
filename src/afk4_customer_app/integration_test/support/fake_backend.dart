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
    if (path.startsWith('/api/me/') && headers['Authorization'] != 'Bearer $accessToken') {
      return ('{"error":"unauthorized"}', 401);
    }

    return switch ((method, path)) {
      ('GET', '/api/public/organizations') => (jsonEncode([_club]), 200),
      ('POST', '/api/public/player/sign-in/code') => (
          '{"expiresInSeconds":300,"resendAfterSeconds":60}',
          200,
        ),
      ('POST', '/api/public/player/sign-in/code/confirm') => body['code'] == smsCode
          ? (jsonEncode(_session), 200)
          : ('{"error":"invalid_code"}', 400),
      ('GET', '/api/me/dashboard') => (jsonEncode(_dashboard), 200),
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
  Map<String, dynamic> _createReservation(Map<String, dynamic> body) {
    final created = <String, dynamic>{
      'reservationId': 'r${_nextReservation++}',
      'seatId': null,
      'seatName': null,
      'startsAtUtc': body['startsAtUtc'],
      'endsAtUtc': body['endsAtUtc'],
      'state': 'pending',
      'note': null,
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
    'displayName': playerName,
    'phoneVerified': true,
    'accessToken': accessToken,
    'accessTokenExpiresAtUtc': '2099-01-01T00:00:00Z',
    'refreshToken': 'refresh-1',
    'refreshTokenExpiresAtUtc': '2099-01-01T00:00:00Z',
  };

  static const Map<String, dynamic> _dashboard = {
    'walletBalance': {'currencyCode': 'TJS', 'minorUnits': 120050},
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
  };
}
