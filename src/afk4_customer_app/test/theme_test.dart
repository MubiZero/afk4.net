import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/theme/app_theme.dart';

void main() {
  // Фирменный акцент — единственное, что приложение делит с Оператором и вебом: по нему
  // продукт узнаётся. Поверхности у игрока свои (тёмная витрина против плотной админки),
  // а emerald обязан совпадать со значением `--accent` в packages/tokens/tokens.css.
  test('акцент совпадает с продуктовым emerald в обеих темах', () {
    expect(AppTheme.dark().colorScheme.primary, const Color(0xFF2CC592));
    expect(AppTheme.light().colorScheme.primary, const Color(0xFF0B9E74));
  });

  test('тёмная и светлая различаются по яркости, а не только по акценту', () {
    expect(AppTheme.dark().brightness, Brightness.dark);
    expect(AppTheme.light().brightness, Brightness.light);
    expect(AppTheme.dark().canvasColor, const Color(0xFF080C0B));
    expect(AppTheme.light().canvasColor, const Color(0xFFF3F6F5));
  });

  // Фон Scaffold прозрачен намеренно: холст красит AmbientBackground под навигатором.
  // Непрозрачный Scaffold перекрыл бы свет зала и дал шов при переходах между экранами.
  test('экран не красит фон сам — под ним свет зала', () {
    for (final theme in [AppTheme.dark(), AppTheme.light()]) {
      expect(theme.scaffoldBackgroundColor, Colors.transparent);
    }
  });

  test('текст на акценте контрастен: на emerald ни белый, ни серый не читаются', () {
    expect(AppTheme.dark().colorScheme.onPrimary, const Color(0xFF04120D));
    expect(AppTheme.light().colorScheme.onPrimary, Colors.white);
  });

  // Размеры в текстовой теме проставляются только при построении MaterialApp, поэтому поля
  // вроде `appBarTheme.titleTextStyle` обязаны нести размер сами. Стиль без него молча
  // рисуется дефолтными 14 пунктами — заголовок экрана становится подписью.
  test('заголовок шапки задан размером, а не ссылкой на текстовую тему', () {
    for (final theme in [AppTheme.dark(), AppTheme.light()]) {
      final title = theme.appBarTheme.titleTextStyle;
      expect(title?.fontSize, isNotNull);
      expect(title!.fontSize!, greaterThanOrEqualTo(20));
    }
  });

  // Палец, а не мышь: 44 — минимум для касания. Главная кнопка экрана крупнее минимума,
  // иначе основное действие требует прицеливания.
  test('минимальная высота интерактивных элементов не ниже 44', () {
    for (final theme in [AppTheme.dark(), AppTheme.light()]) {
      final buttonSize = theme.filledButtonTheme.style?.minimumSize?.resolve({});
      expect(buttonSize, isNotNull);
      expect(buttonSize!.height, greaterThanOrEqualTo(AppTheme.minTouchTarget));
      expect(buttonSize.height, AppTheme.primaryButtonHeight);
    }
  });
}
