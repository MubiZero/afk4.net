import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Запоминает выбранное оформление между запусками.
///
/// Хранится там же и так же, как язык: это предпочтение, а не секрет. Пока игрок ничего не
/// выбрал, хранилище пустое, и приложение остаётся тёмным — оно живёт в компьютерном клубе,
/// ночью, и системная светлая тема телефона не повод слепить человека в тёмном зале.
class ThemePreferenceStore {
  const ThemePreferenceStore();

  static const String _key = 'afk4.player.theme';

  /// Оформление, пока игрок не выбрал своё.
  static const ThemeMode fallback = ThemeMode.dark;

  static const Map<ThemeMode, String> _codes = {
    ThemeMode.system: 'system',
    ThemeMode.dark: 'dark',
    ThemeMode.light: 'light',
  };

  Future<ThemeMode?> read() async {
    final prefs = await SharedPreferences.getInstance();
    final code = prefs.getString(_key);
    if (code == null) return null;
    for (final entry in _codes.entries) {
      if (entry.value == code) return entry.key;
    }
    // Значение из будущей или испорченной версии забывается, а не роняет запуск.
    await prefs.remove(_key);
    return null;
  }

  Future<void> write(ThemeMode mode) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_key, _codes[mode]!);
  }
}
