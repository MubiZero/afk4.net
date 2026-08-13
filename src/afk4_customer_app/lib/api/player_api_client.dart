import 'dart:convert';

import 'package:http/http.dart' as http;

import '../auth/player_session.dart';
import 'dto.dart';

/// Ошибка запроса к API. Несёт код состояния: 401 на входе — «неверный пароль», 403 на
/// действии — «возможность выключена», и на экране это разные тексты.
class PlayerApiException implements Exception {
  const PlayerApiException(this.statusCode, this.message);

  final int? statusCode;
  final String message;

  @override
  String toString() => 'PlayerApiException($statusCode, $message)';
}

/// Клиент API игрока.
///
/// Живёт долго и переживает смену сессии: вход и продление токена меняют её ВНУТРИ клиента,
/// а не создают новый. Пересоздание перемонтировало бы дерево экранов и перезапустило опросы —
/// на главной это видно как мигание живой сессии. Правило перенесено из веб-версии.
class PlayerApiClient {
  PlayerApiClient({
    required this.baseUrl,
    http.Client? httpClient,
    PlayerSession? session,
    void Function(PlayerSession?)? onSessionChanged,
  })  : _http = httpClient ?? http.Client(),
        _session = session,
        _onSessionChanged = onSessionChanged;

  final String baseUrl;
  final http.Client _http;
  final void Function(PlayerSession?)? _onSessionChanged;

  PlayerSession? _session;
  PlayerSession? get session => _session;

  /// Смена сессии без пересоздания клиента — см. комментарий к классу.
  void updateSession(PlayerSession? next) {
    _session = next;
    _onSessionChanged?.call(next);
  }

  Future<PlayerSession> signIn({
    required String organizationId,
    required String phoneNumber,
    required String password,
  }) async {
    final body = await _post('/api/public/player/sign-in', {
      'organizationId': organizationId,
      'phoneNumber': phoneNumber,
      'password': password,
    });
    final signedIn = PlayerSession.fromJson(body);
    updateSession(signedIn);
    return signedIn;
  }

  /// Просит код для входа. Ответ одинаков и для известного, и для незнакомого номера —
  /// сервер намеренно не подсказывает, кто где играет.
  Future<PhoneVerificationStarted> startCodeSignIn({
    required String organizationId,
    required String phoneNumber,
  }) async =>
      _parse(
        await _post('/api/public/player/sign-in/code', {
          'organizationId': organizationId,
          'phoneNumber': phoneNumber,
        }),
        PhoneVerificationStarted.fromJson,
      );

  /// Вход по коду. Он же подтверждает номер: прочитать код с этого телефона — то же
  /// доказательство, которого требует подтверждение.
  Future<PlayerSession> confirmCodeSignIn({
    required String organizationId,
    required String phoneNumber,
    required String code,
  }) async {
    final body = await _post('/api/public/player/sign-in/code/confirm', {
      'organizationId': organizationId,
      'phoneNumber': phoneNumber,
      'code': code,
    });
    final signedIn = PlayerSession.fromJson(body);
    updateSession(signedIn);
    return signedIn;
  }

  Future<Map<String, dynamic>> getJson(String path) async {
    var response = await _send('GET', path);
    if (response.statusCode == 401 && await _refreshOnce()) {
      response = await _send('GET', path);
    }
    return _decode(response);
  }

  /// Главный экран: кошелёк, долг и текущая сессия.
  Future<PlayerDashboard> getDashboard() async =>
      _parse(await getJson('/api/me/dashboard'), PlayerDashboard.fromJson);

  /// Разбор ответа. Недостающее или чужого типа поле — такая же неудача запроса, как сетевой
  /// сбой: экран покажет ошибку загрузки вместо падения.
  static T _parse<T>(Map<String, dynamic> body, T Function(Map<String, dynamic>) fromJson) {
    try {
      return fromJson(body);
    } catch (_) {
      throw const PlayerApiException(null, 'malformed-body');
    }
  }

  /// Возможности, включённые клубу. Список неполный или недоступный — не повод прятать
  /// кнопку: право на запись всё равно проверяет сервер, а спрятанная кнопка выглядит как
  /// «сломалось». Отсюда fail-open: ошибка поднимается наверх, экран трактует её как
  /// «считаем включённым». Кривой ответ (нет массива) считается ошибкой, а не пустым списком —
  /// иначе рассинхрон версий тихо выключил бы половину приложения.
  Future<List<String>> getFeatures() async {
    final body = await getJson('/api/me/features');
    final features = body['features'];
    if (features is! List) {
      throw const PlayerApiException(null, 'malformed-features');
    }
    return features.cast<String>();
  }

  Future<CursorPage<PlayerVisit>> getVisits({String? cursor}) async =>
      _parse(await getJson(_withCursor('/api/me/visits', cursor)),
          (body) => CursorPage.fromJson(body, PlayerVisit.fromJson));

  Future<VisitReceipt> getVisitReceipt(String sessionId) async => _parse(
        await getJson('/api/me/visits/${Uri.encodeComponent(sessionId)}/receipt'),
        VisitReceipt.fromJson,
      );

  Future<CursorPage<PlayerPurchase>> getPurchases({String? cursor}) async =>
      _parse(await getJson(_withCursor('/api/me/purchases', cursor)),
          (body) => CursorPage.fromJson(body, PlayerPurchase.fromJson));

  static String _withCursor(String path, String? cursor) =>
      cursor == null ? path : '$path?cursor=${Uri.encodeQueryComponent(cursor)}';

  Future<PlayerProfile> getProfile() async =>
      _parse(await getJson('/api/me/profile'), PlayerProfile.fromJson);

  /// Меняет только переданные поля: не указанное остаётся как было. Слать весь профиль
  /// целиком значит затирать чужие изменения тем, что экран успел прочитать.
  Future<PlayerProfile> updateProfile({String? preferredLocale, bool? marketingOptIn}) async {
    final body = <String, dynamic>{};
    if (preferredLocale != null) body['preferredLocale'] = preferredLocale;
    if (marketingOptIn != null) body['marketingOptIn'] = marketingOptIn;
    return _parse(await sendJson('PATCH', '/api/me/profile', body), PlayerProfile.fromJson);
  }

  /// Просит прислать код на номер. Ошибки различимы по коду состояния: 400 — номер не похож
  /// на номер, 429 — рано или слишком часто, 502 — SMS не ушла.
  Future<PhoneVerificationStarted> startPhoneVerification(String phone) async => _parse(
        await sendJson('POST', '/api/me/phone/start-verification', {'phone': phone}),
        PhoneVerificationStarted.fromJson,
      );

  /// Подтверждает номер кодом. 400 — код неверен, 410 — устарел или его нет, 409 — номер уже
  /// занят другим игроком клуба.
  Future<String> confirmPhone(String code) async {
    final body = await sendJson('POST', '/api/me/phone/confirm', {'code': code});
    final phone = body['phone'];
    if (phone is! String) throw const PlayerApiException(null, 'malformed-body');
    return phone;
  }

  Future<List<PlayerReservation>> getReservations() async {
    final list = await getJsonList('/api/me/reservations');
    return list.map((item) => _parse(item, PlayerReservation.fromJson)).toList();
  }

  Future<PlayerReservation> createReservation({
    required DateTime startsAtUtc,
    required DateTime endsAtUtc,
  }) async {
    final body = await sendJson('POST', '/api/me/reservations', {
      'startsAtUtc': startsAtUtc.toUtc().toIso8601String(),
      'endsAtUtc': endsAtUtc.toUtc().toIso8601String(),
    });
    return _parse(body, PlayerReservation.fromJson);
  }

  Future<PlayerReservation> cancelReservation(String reservationId) async {
    final body = await sendJson(
        'DELETE', '/api/me/reservations/${Uri.encodeComponent(reservationId)}');
    return _parse(body, PlayerReservation.fromJson);
  }

  /// Продлевает идущую сессию. Деньги списываются сразу, поэтому запрос несёт ключ
  /// идемпотентности — см. `newIdempotencyKey`. Ответ сервера не разбирается: главный экран
  /// всё равно перечитывает себя, а состояние сессии он берёт оттуда, а не из эха команды.
  ///
  /// 409 — на кошельке не хватает денег, 404 — сессия уже не идёт (или чужая).
  Future<void> extendSession({
    required String sessionId,
    required int additionalMinutes,
    required String idempotencyKey,
  }) async {
    await sendJson('POST', '/api/me/sessions/${Uri.encodeComponent(sessionId)}/extend', {
      'additionalMinutes': additionalMinutes,
      'idempotencyKey': idempotencyKey,
    });
  }

  Future<List<TopUpIntent>> getTopUpIntents() async {
    final list = await getJsonList('/api/me/wallet/top-up-intents');
    return list.map((item) => _parse(item, TopUpIntent.fromJson)).toList();
  }

  Future<TopUpIntent> createTopUpIntent({
    required int amountMinorUnits,
    required String currencyCode,
  }) async {
    final body = await sendJson('POST', '/api/me/wallet/top-up-intent', {
      'amountMinorUnits': amountMinorUnits,
      'currencyCode': currencyCode,
    });
    return _parse(body, TopUpIntent.fromJson);
  }

  Future<List<Map<String, dynamic>>> getJsonList(String path) async {
    var response = await _send('GET', path);
    if (response.statusCode == 401 && await _refreshOnce()) {
      response = await _send('GET', path);
    }
    return _decodeList(response);
  }

  /// Запрос с телом под сессией. Как и чтение, продлевает токен один раз и повторяет —
  /// с тем же телом.
  Future<Map<String, dynamic>> sendJson(String method, String path, [Object? body]) async {
    var response = await _send(method, path, body: body);
    if (response.statusCode == 401 && await _refreshOnce()) {
      response = await _send(method, path, body: body);
    }
    return _decode(response);
  }

  Future<Map<String, dynamic>> _post(String path, Object body) async {
    final response = await _send('POST', path, body: body);
    return _decode(response);
  }

  Future<http.Response> _send(String method, String path, {Object? body}) async {
    final uri = Uri.parse('$baseUrl$path');
    final headers = <String, String>{};
    final token = _session?.accessToken;
    if (token != null) headers['Authorization'] = 'Bearer $token';
    if (body != null) headers['Content-Type'] = 'application/json';

    final request = http.Request(method, uri)..headers.addAll(headers);
    if (body != null) request.body = jsonEncode(body);

    try {
      return await http.Response.fromStream(await _http.send(request));
    } catch (_) {
      throw const PlayerApiException(null, 'network');
    }
  }

  /// Одна попытка продления на запрос. Больше одной — верный способ зациклиться на сервере,
  /// который упорно отвечает 401.
  Future<bool> _refreshOnce() async {
    final current = _session;
    if (current == null) return false;

    final response = await _send('POST', '/api/public/player/refresh', body: {
      'refreshToken': current.refreshToken,
    });

    if (response.statusCode != 200) {
      // Продлить не вышло — сессии больше нет. Оставить мёртвый токен значит показывать
      // игроку ошибки до конца времён вместо экрана входа.
      updateSession(null);
      return false;
    }

    updateSession(PlayerSession.fromJson(
      jsonDecode(utf8.decode(response.bodyBytes)) as Map<String, dynamic>,
    ));
    return true;
  }

  Map<String, dynamic> _decode(http.Response response) {
    final body = _body(response);
    if (body is! Map<String, dynamic>) throw const PlayerApiException(null, 'malformed-body');
    return body;
  }

  List<Map<String, dynamic>> _decodeList(http.Response response) {
    final body = _body(response);
    if (body is! List) throw const PlayerApiException(null, 'malformed-body');
    return body.cast<Map<String, dynamic>>();
  }

  /// Ответ не той формы — такая же неудача запроса, как сетевой сбой: рассинхрон версий или
  /// прокси-заглушка не должны прилетать в экран необработанной ошибкой типа.
  Object? _body(http.Response response) {
    if (response.statusCode != 200) {
      throw PlayerApiException(response.statusCode, _errorMessage(response));
    }
    try {
      return jsonDecode(utf8.decode(response.bodyBytes));
    } catch (_) {
      throw const PlayerApiException(null, 'malformed-body');
    }
  }

  static String _errorMessage(http.Response response) {
    try {
      final parsed = jsonDecode(utf8.decode(response.bodyBytes));
      if (parsed is Map<String, dynamic> && parsed['error'] is String) return parsed['error'] as String;
    } catch (_) {
      // Тело не JSON — пусть будет код состояния.
    }
    return 'HTTP ${response.statusCode}';
  }
}
