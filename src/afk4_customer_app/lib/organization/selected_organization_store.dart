import 'dart:convert';

import 'package:shared_preferences/shared_preferences.dart';

import 'organization.dart';

/// Запоминает выбранный клуб между запусками: спрашивать его при каждом открытии приложения —
/// то же, что спрашивать логин заново.
///
/// Хранится в обычных настройках, а не в защищённом хранилище: это не секрет, а предпочтение.
/// Токены сессии живут отдельно и в Keychain/Keystore.
class SelectedOrganizationStore {
  const SelectedOrganizationStore();

  static const String _key = 'afk4.player.selectedOrganization';

  Future<Organization?> read() async {
    final prefs = await SharedPreferences.getInstance();
    final raw = prefs.getString(_key);
    if (raw == null) return null;
    try {
      return Organization.fromJson(jsonDecode(raw) as Map<String, dynamic>);
    } catch (_) {
      // Формат мог измениться между версиями приложения. Спросить клуб заново неприятно,
      // но лучше, чем падать на старте с непонятной ошибкой.
      await prefs.remove(_key);
      return null;
    }
  }

  Future<void> write(Organization organization) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_key, jsonEncode(organization.toJson()));
  }

  Future<void> clear() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_key);
  }
}
