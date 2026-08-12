import 'package:flutter/widgets.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'localization_setup.dart';

/// Запоминает выбранный язык между запусками.
///
/// Хранится в обычных настройках: это предпочтение, а не секрет. Пока игрок ничего не выбрал,
/// хранилище пустое, и приложение берёт язык устройства — навязывать русский телефону,
/// настроенному на таджикский, незачем.
class LocalePreferenceStore {
  const LocalePreferenceStore();

  static const String _key = 'afk4.player.locale';

  Future<Locale?> read() async {
    final prefs = await SharedPreferences.getInstance();
    final code = prefs.getString(_key);
    if (code == null) return null;
    // Язык мог исчезнуть из поддерживаемых между версиями — тогда предпочтение забывается,
    // а не роняет запуск.
    final supported = appSupportedLocales.any((locale) => locale.languageCode == code);
    if (!supported) {
      await prefs.remove(_key);
      return null;
    }
    return Locale(code);
  }

  Future<void> write(Locale locale) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_key, locale.languageCode);
  }
}
