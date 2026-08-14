import 'dart:async';

import 'package:shared_preferences/shared_preferences.dart';

import '../api/player_api_client.dart';
import 'push_tokens.dart';

/// Уведомления на телефон: включение, выключение и поддержание адреса в актуальном состоянии.
///
/// Игрок управляет ими из настроек, и выключение здесь настоящее: токен снимается с сервера,
/// а не просто прячется флажком в приложении. Приложение, которое нельзя заткнуть, удаляют.
class PushService {
  PushService({required PlayerApiClient api, required PushTokens tokens, String? locale})
      : _api = api,
        _tokens = tokens,
        _locale = locale;

  static const String _enabledKey = 'afk4.player.push.enabled';
  static const String _tokenKey = 'afk4.player.push.token';

  final PlayerApiClient _api;
  final PushTokens _tokens;
  final String? _locale;

  StreamSubscription<String>? _refreshSubscription;

  bool get isSupported => _tokens.isSupported;

  /// Спрашивал ли уже игрок про уведомления. Пока не спрашивал — считаем, что согласен:
  /// разрешение всё равно спросит система, и второе окно поверх системного бессмысленно.
  Future<bool> isEnabled() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getBool(_enabledKey) ?? true;
  }

  /// Вход в аккаунт: сообщаем серверу адрес и подписываемся на его смену.
  Future<void> syncAfterSignIn() async {
    if (!_tokens.isSupported || !await isEnabled()) return;

    if (!await _tokens.requestPermission()) {
      // Игрок отказал системе — запоминаем это, чтобы переключатель в настройках показывал
      // правду, а не наше пожелание.
      await _remember(enabled: false);
      return;
    }

    await _registerCurrentToken();
    _listenForRefresh();
  }

  Future<bool> enable() async {
    if (!_tokens.isSupported) return false;
    if (!await _tokens.requestPermission()) return false;

    await _remember(enabled: true);
    final registered = await _registerCurrentToken();
    if (registered) _listenForRefresh();
    return registered;
  }

  Future<void> disable() async {
    await _remember(enabled: false);
    await _refreshSubscription?.cancel();
    _refreshSubscription = null;
    await _unregisterStoredToken();
  }

  /// Выход из аккаунта. Зовётся до того, как сессия выброшена: после выхода звать уже нечем,
  /// а токен остался бы висеть на прежнем игроке — и уведомления пришли бы ему.
  Future<void> clearOnSignOut() async {
    await _refreshSubscription?.cancel();
    _refreshSubscription = null;
    await _unregisterStoredToken();
  }

  Future<bool> _registerCurrentToken() async {
    final token = await _tokens.token();
    if (token == null) return false;

    try {
      await _api.registerDevice(pushToken: token, platform: _tokens.platform, locale: _locale);
    } on PlayerApiException {
      // Сервер недоступен или отказал: пуши подождут до следующего входа. Экран, на котором
      // игрок только что вошёл, из-за этого падать не должен.
      return false;
    }

    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_tokenKey, token);
    return true;
  }

  Future<void> _unregisterStoredToken() async {
    final prefs = await SharedPreferences.getInstance();
    final token = prefs.getString(_tokenKey);
    if (token == null) return;

    try {
      await _api.unregisterDevice(token);
    } on PlayerApiException {
      // Не достучались — токен всё равно забываем локально: держаться за него нам незачем,
      // а на сервере он умрёт сам, когда FCM ответит, что устройства больше нет.
    }
    await prefs.remove(_tokenKey);
  }

  void _listenForRefresh() {
    _refreshSubscription?.cancel();
    _refreshSubscription = _tokens.onTokenRefresh.listen((token) async {
      try {
        await _api.registerDevice(pushToken: token, platform: _tokens.platform, locale: _locale);
        final prefs = await SharedPreferences.getInstance();
        await prefs.setString(_tokenKey, token);
      } on PlayerApiException {
        // Следующий вход перерегистрирует.
      }
    });
  }

  Future<void> _remember({required bool enabled}) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_enabledKey, enabled);
  }
}
