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

  // Material выводит secondaryContainer из secondary, и без явного значения тональные кнопки
  // и выбранные чипы становятся фиолетовыми посреди фирменного зелёного экрана.
  test('второстепенные подсвеченные поверхности остаются фирменными, а не фиолетовыми', () {
    for (final theme in [AppTheme.dark(), AppTheme.light()]) {
      final container = theme.colorScheme.secondaryContainer;
      expect(container, isNot(AppTheme.violet));
      // Зелёного в подложке больше, чем синего — это оттенок бренда, а не Material по умолчанию.
      expect(container.g, greaterThan(container.b));
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

  // Цвет клуба владелец задаёт в брендинге сети. Приложение носит его, пока игрок в этом
  // клубе: заведение узнают по цвету раньше, чем прочитают название.
  test('цвет клуба становится акцентом приложения', () {
    final theme = AppTheme.dark(clubColor: const Color(0xFFD64545));

    expect(theme.colorScheme.primary.r, greaterThan(theme.colorScheme.primary.g));
    expect(theme.colorScheme.primary, isNot(AppTheme.emerald));
  });

  test('без цвета клуба остаётся фирменный emerald', () {
    expect(AppTheme.dark(clubColor: null).colorScheme.primary, AppTheme.emerald);
  });

  // Мусор в поле цвета — не повод красить приложение в чёрный или падать на запуске.
  test('нецветное значение брендинга игнорируется', () {
    for (final value in ['', '   ', 'зелёный', '#12', '#GGGGGG', null]) {
      expect(AppTheme.parseBrandColor(value), isNull, reason: value ?? 'null');
    }
    expect(AppTheme.parseBrandColor('#D64545'), const Color(0xFFD64545));
    expect(AppTheme.parseBrandColor('d64545'), const Color(0xFFD64545));
    // Короткая запись и запись с прозрачностью тоже встречаются в брендбуках.
    expect(AppTheme.parseBrandColor('#f00'), const Color(0xFFFF0000));
    expect(AppTheme.parseBrandColor('#80D64545'), const Color(0xFFD64545));
  });

  // Тёмно-синий логотип на почти чёрном фоне дал бы кнопку, которой не видно.
  test('слишком тёмный цвет клуба поднимается до различимого', () {
    final theme = AppTheme.dark(clubColor: const Color(0xFF0A1240));

    expect(theme.colorScheme.primary.computeLuminance(),
        greaterThan(const Color(0xFF0A1240).computeLuminance()));
  });

  // Белые буквы на жёлтой кнопке не читаются, чёрные на тёмно-синей — тоже.
  test('надпись на цвете клуба выбирается по его яркости', () {
    expect(AppTheme.onAccentFor(const Color(0xFFF5D90A)), isNot(Colors.white));
    expect(AppTheme.onAccentFor(const Color(0xFF1E3A8A)), Colors.white);
  });
}
