import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'player_session.dart';

/// Хранилище сессии. В вебе токены лежали в `localStorage`; на телефоне им место в Keychain
/// (iOS) и Keystore (Android) — телефон теряют чаще, чем ноутбук, и потерянный вместе с ним
/// доступ к кошельку игрока стоит дороже удобства.
class PlayerSessionStore {
  const PlayerSessionStore({FlutterSecureStorage? storage})
      : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;

  static const String _key = 'afk4.player.session';

  Future<PlayerSession?> read() async {
    final raw = await _storage.read(key: _key);
    if (raw == null || raw.isEmpty) return null;
    try {
      return PlayerSession.fromJson(jsonDecode(raw) as Map<String, dynamic>);
    } catch (_) {
      // Битая или устаревшая по формату запись = нет сессии. Чистим, чтобы не спотыкаться
      // о неё при каждом запуске.
      await clear();
      return null;
    }
  }

  Future<void> write(PlayerSession session) =>
      _storage.write(key: _key, value: jsonEncode(session.toJson()));

  Future<void> clear() => _storage.delete(key: _key);
}
