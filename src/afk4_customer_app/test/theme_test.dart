import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/theme/app_theme.dart';

void main() {
  // Палитра — не «подобранная под Flutter», а та же, что в packages/tokens/tokens.css.
  // Приложение и веб стоят рядом на одном телефоне; расхождение цвета читается как разные
  // продукты. Значения продублированы здесь намеренно: тест обязан ловить дрейф темы.
  test('акцент совпадает с продуктовым emerald в обеих темах', () {
    expect(AppTheme.dark().colorScheme.primary, const Color(0xFF2CC592));
    expect(AppTheme.light().colorScheme.primary, const Color(0xFF0B9E74));
  });

  test('тёмная и светлая различаются по яркости, а не только по акценту', () {
    expect(AppTheme.dark().brightness, Brightness.dark);
    expect(AppTheme.light().brightness, Brightness.light);
    expect(AppTheme.dark().scaffoldBackgroundColor, const Color(0xFF121212));
    expect(AppTheme.light().scaffoldBackgroundColor, const Color(0xFFEEF2F7));
  });

  test('текст на акценте тёмный — на emerald белый не читается', () {
    expect(AppTheme.dark().colorScheme.onPrimary, const Color(0xFF0A1A14));
    expect(AppTheme.light().colorScheme.onPrimary, const Color(0xFF0F172A));
  });

  // Палец, а не мышь: 44 — минимум для касания. Material по умолчанию даёт 48 на кнопку,
  // но целимся явно, иначе плотные списки незаметно уползают ниже порога.
  test('минимальная высота интерактивных элементов не ниже 44', () {
    for (final theme in [AppTheme.dark(), AppTheme.light()]) {
      final buttonSize = theme.filledButtonTheme.style?.minimumSize?.resolve({});
      expect(buttonSize, isNotNull);
      expect(buttonSize!.height, greaterThanOrEqualTo(44));
    }
  });
}
