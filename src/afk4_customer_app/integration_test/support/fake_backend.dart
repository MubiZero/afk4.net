import 'dart:convert';

import 'package:http/http.dart' as http;

/// Сервер в памяти для сквозного сценария.
///
/// От заглушек модульных тестов отличается тем, что помнит состояние: созданная бронь
/// попадает в список, закрытые разделы отвечают 401 без токена, а клуб, который игрока ещё
/// не знает, отвечает 409 на всё, кроме того, чем счёт и открывается. Сквозной тест иначе
/// проверял бы, что кнопки нажимаются, а не что приложение и сервер сходятся.
///
/// Сеть здесь из двух залов, а игрок в ней новый — то самое сочетание, из-за которого сервер
/// не может завести счёт сам: человек придёт в один зал, а кошелёк оказался бы в другом.
class FakeBackend extends http.BaseClient {
  FakeBackend({this.smsCode = '4321'});

  /// Код, который «пришёл в SMS». Любой другой отклоняется как неверный.
  final String smsCode;

  static const String organizationId = '11111111-1111-1111-1111-111111111111';
  static const String rudakiBranchId = '22222222-2222-2222-2222-222222222222';
  static const String somoniBranchId = '33333333-3333-3333-3333-333333333333';
  static const String rudakiName = 'На Рудаки';
  static const String somoniName = 'На Сомони';

  /// Столько филиал обещает думать над заявкой — по нему в приложении идёт отсчёт.
  static const int respondWithinMinutes = 15;
  static const String accessToken = 'access-1';
  static const String playerName = 'Иван';
  static const String clubName = 'CyberX';

  /// Брони игрока. Пустые в начале сценария, пополняются его же руками.
  final List<Map<String, dynamic>> reservations = [];

  /// Запросы по порядку: `'МЕТОД /путь'`. По ним видно, куда приложение сходило и —
  /// не менее важно — куда не сходило до открытия счёта и после выхода.
  final List<String> log = [];

  /// Заголовок авторизации у каждого запроса; null — его не было.
  final List<String?> authHeaders = [];

  /// Зал, в котором открылся счёт игрока. null — счёта в клубе ещё нет, и клубу нечего о
  /// нём рассказать: ни денег, ни истории.
  String? accountBranchId;

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

  /// Что человек может спросить у клуба, в котором у него ещё нет счёта: кто он сам, правила
  /// приёма зала — и два действия, которыми счёт открывается. Всё остальное принадлежит счёту.
  static bool _allowedWithoutAccount(String method, String path) =>
      (method == 'GET' && path == '/api/me') ||
      path.endsWith('/booking-rules') ||
      (method == 'POST' && (path == '/api/me/reservations' ||
          path == '/api/me/wallet/top-up-intent'));

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

    if (path.startsWith('/api/me') &&
        accountBranchId == null &&
        !_allowedWithoutAccount(method, path)) {
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
      ('GET', '/api/me/branches/$rudakiBranchId/tariffs') => ('[]', 200),
      ('GET', '/api/me/branches/$somoniBranchId/tariffs') => ('[]', 200),
      ('GET', '/api/me/branches/$rudakiBranchId/booking-rules') => (
          jsonEncode(_bookingRules(rudakiBranchId)),
          200,
        ),
      ('GET', '/api/me/branches/$somoniBranchId/booking-rules') => (
          jsonEncode(_bookingRules(somoniBranchId)),
          200,
        ),
      ('GET', '/api/me/features') => ('{"features":["online_booking","online_topup"]}', 200),
      ('GET', '/api/me/profile') => (jsonEncode(_profile), 200),
      ('GET', '/api/me/reservations') => (jsonEncode(reservations), 200),
      ('POST', '/api/me/reservations') => _createReservation(body),
      ('GET', '/api/me/visits') => ('{"items":[],"nextCursor":null}', 200),
      ('GET', '/api/me/purchases') => ('{"items":[],"nextCursor":null}', 200),
      ('GET', '/api/me/wallet/top-up-intents') => ('[]', 200),
      _ => ('{"error":"not_found"}', 404),
    };
  }

  /// Первая бронь заводит счёт — и только если названо, в каком зале. Сеть из двух залов
  /// угадать это не даёт: тот же 409, которым отвечает настоящий сервер.
  ///
  /// Место клуб назначает потом — новая бронь приходит без него и в состоянии «ожидает».
  /// Раз зал смотрит заявки руками, у неё есть срок ответа: молчание до него снимает заявку
  /// и возвращает деньги целиком.
  (String, int) _createReservation(Map<String, dynamic> body) {
    if (accountBranchId == null) {
      final branchId = body['branchId'] as String?;
      if (branchId == null) return ('{"error":"branch_required"}', 409);
      if (branchId != rudakiBranchId && branchId != somoniBranchId) {
        return ('{"error":"branch_not_found"}', 409);
      }
      accountBranchId = branchId;
    }

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
    return (jsonEncode(created), 200);
  }

  /// Сеть из двух залов: у каждого своё имя и свой адрес — по ним игрок и узнаёт своё место.
  static const Map<String, dynamic> _club = {
    'organizationId': organizationId,
    'slug': 'cyberx',
    'name': clubName,
    'currencyCode': 'TJS',
    'places': [
      {
        'branchId': rudakiBranchId,
        'name': rudakiName,
        'city': 'Душанбе',
        'address': 'проспект Рудаки, 12',
      },
      {
        'branchId': somoniBranchId,
        'name': somoniName,
        'city': 'Душанбе',
        'address': 'улица Сомони, 40',
      },
    ],
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

  /// Кто я и где у меня счета. Общей суммы денег нет: у каждого клуба своя касса. Пока счёта
  /// в клубе нет, список клубов пуст — приложение по нему и понимает, что клуб игрока не знает.
  Map<String, dynamic> get _me => {
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
          if (accountBranchId != null)
            {
              'organizationId': organizationId,
              'organizationName': clubName,
              'playerAccountId': 'p1',
              'homeBranchId': accountBranchId,
              'currencyCode': 'TJS',
              'walletBalanceMinorUnits': 120050,
              'heldMinorUnits': 4500,
              'debtMinorUnits': 0,
              'visitCount': 3,
            },
        ],
      };

  /// Зал смотрит заявки руками и предоплаты с этого игрока не требует. Спрашивается до счёта:
  /// новичку это нужнее всего.
  static Map<String, dynamic> _bookingRules(String branchId) => {
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

  Map<String, dynamic> get _profile => {
        'playerAccountId': 'p1',
        'displayName': playerName,
        'phoneNumber': '+992900000000',
        'phoneVerified': true,
        'preferredLocale': null,
        'marketingOptIn': false,
        'homeBranchId': accountBranchId,
        'homeBranchName': accountBranchId == somoniBranchId ? somoniName : rudakiName,
      };
}
