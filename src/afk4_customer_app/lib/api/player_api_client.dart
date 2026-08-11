import 'dart:convert';

import 'package:http/http.dart' as http;

import '../auth/player_session.dart';

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

  Future<Map<String, dynamic>> getJson(String path) async {
    var response = await _send('GET', path);
    if (response.statusCode == 401 && await _refreshOnce()) {
      response = await _send('GET', path);
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
    if (response.statusCode != 200) {
      throw PlayerApiException(response.statusCode, _errorMessage(response));
    }
    return jsonDecode(utf8.decode(response.bodyBytes)) as Map<String, dynamic>;
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
