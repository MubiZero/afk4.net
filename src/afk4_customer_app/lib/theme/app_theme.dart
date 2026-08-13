import 'package:flutter/material.dart';

/// Тема клиентского приложения.
///
/// Приложение игрока — не админка: им пользуются в тёмном зале, между катками, одной рукой.
/// Отсюда собственная палитра, а не перенос `packages/tokens/tokens.css`: у Оператора светлый
/// плотный интерфейс за стойкой, у игрока — тёмная витрина с крупными цифрами. Общим остаётся
/// фирменный emerald: по нему приложение и узнаётся как AFK4.
///
/// Тёмная тема здесь не «ночной режим», а единственный вид продукта — как у игровых площадок,
/// рядом с которыми это приложение лежит на телефоне. Светлая оставлена рабочей для тех, кто
/// включил её системно, но проектируется тёмная.
class AppTheme {
  const AppTheme._();

  /// Фирменный акцент. Совпадает с `--accent` тёмной темы токенов — один продукт.
  static const Color emerald = Color(0xFF2CC592);
  static const Color emeraldBright = Color(0xFF5FF0BE);

  /// Второй свет — им подсвечиваются игровые состояния (идущая сессия, бронь).
  static const Color violet = Color(0xFF7B5CFF);

  // Тёмная сторона: почти чёрный с зелёным уклоном, чтобы акцент не выглядел вклеенным.
  static const Color _darkCanvas = Color(0xFF080C0B);
  static const Color _darkSurface = Color(0xFF111917);
  static const Color _darkCard = Color(0xFF161F1D);
  static const Color _darkBorder = Color(0xFF25302D);
  static const Color _darkText = Color(0xFFF2FBF8);
  static const Color _darkTextMuted = Color(0xB3EAFBF6);
  static const Color _darkOnAccent = Color(0xFF04120D);
  static const Color _darkDanger = Color(0xFFFF7A7A);

  // Светлая сторона: тот же строй форм, дневные поверхности.
  static const Color _lightCanvas = Color(0xFFF3F6F5);
  static const Color _lightSurface = Color(0xFFFFFFFF);
  static const Color _lightCard = Color(0xFFFFFFFF);
  static const Color _lightBorder = Color(0xFFDDE5E2);
  static const Color _lightText = Color(0xFF0B1512);
  static const Color _lightTextMuted = Color(0xFF5A6B66);
  static const Color _lightAccent = Color(0xFF0B9E74);
  static const Color _lightOnAccent = Color(0xFFFFFFFF);
  static const Color _lightDanger = Color(0xFFDC2626);

  /// Минимальная сторона зоны касания: 44 — общий минимум Apple и WCAG.
  static const double minTouchTarget = 44;

  /// Высота основной кнопки. Крупнее минимума: главное действие экрана не должно требовать
  /// прицеливания.
  static const double primaryButtonHeight = 54;

  /// Скругления. Одно значение на карточки и листы, второе — на кнопки и поля.
  static const double radiusCard = 24;
  static const double radiusControl = 16;

  /// Свет под акцентными поверхностями — то, чем тёмный интерфейс отличается от чёрного.
  static List<BoxShadow> accentGlow(Color color) => [
        BoxShadow(color: color.withValues(alpha: 0.28), blurRadius: 28, offset: const Offset(0, 12)),
      ];

  static ThemeData dark() => _build(
        brightness: Brightness.dark,
        canvas: _darkCanvas,
        surface: _darkSurface,
        card: _darkCard,
        border: _darkBorder,
        text: _darkText,
        textMuted: _darkTextMuted,
        accent: emerald,
        onAccent: _darkOnAccent,
        danger: _darkDanger,
      );

  static ThemeData light() => _build(
        brightness: Brightness.light,
        canvas: _lightCanvas,
        surface: _lightSurface,
        card: _lightCard,
        border: _lightBorder,
        text: _lightText,
        textMuted: _lightTextMuted,
        accent: _lightAccent,
        onAccent: _lightOnAccent,
        danger: _lightDanger,
      );

  static ThemeData _build({
    required Brightness brightness,
    required Color canvas,
    required Color surface,
    required Color card,
    required Color border,
    required Color text,
    required Color textMuted,
    required Color accent,
    required Color onAccent,
    required Color danger,
  }) {
    final scheme = ColorScheme(
      brightness: brightness,
      primary: accent,
      onPrimary: onAccent,
      secondary: violet,
      onSecondary: Colors.white,
      error: danger,
      onError: brightness == Brightness.dark ? _darkOnAccent : Colors.white,
      surface: surface,
      onSurface: text,
      onSurfaceVariant: textMuted,
      surfaceContainerHighest: card,
      outline: border,
      outlineVariant: border,
    );

    final seed = ThemeData(useMaterial3: true, brightness: brightness, colorScheme: scheme);
    final typography = _typography(seed.textTheme).apply(bodyColor: text, displayColor: text);

    return ThemeData(
      useMaterial3: true,
      brightness: brightness,
      colorScheme: scheme,
      // Фон экрана прозрачен намеренно: его красит `AmbientBackground` под навигатором, иначе
      // свет зала перекрывался бы каждым Scaffold и появлялся бы шов при переходах.
      scaffoldBackgroundColor: Colors.transparent,
      canvasColor: canvas,
      splashFactory: InkSparkle.splashFactory,
      textTheme: typography,
      appBarTheme: AppBarTheme(
        // Шапка растворяется в фоне: свет зала за ней должен быть виден целиком.
        backgroundColor: Colors.transparent,
        surfaceTintColor: Colors.transparent,
        foregroundColor: text,
        elevation: 0,
        scrolledUnderElevation: 0,
        centerTitle: false,
        // Размер задан числом, а не ссылкой на `headlineSmall`: размеры в текстовой теме
        // проставляются только при построении MaterialApp, а поля вроде `titleTextStyle`
        // их не получают — стиль без размера молча падает на дефолтные 14 пунктов.
        titleTextStyle: TextStyle(
          color: text,
          fontSize: 22,
          fontWeight: FontWeight.w700,
          letterSpacing: -0.5,
        ),
      ),
      cardTheme: CardThemeData(
        color: card,
        elevation: 0,
        margin: EdgeInsets.zero,
        clipBehavior: Clip.antiAlias,
        shape: RoundedRectangleBorder(
          side: BorderSide(color: border),
          borderRadius: BorderRadius.circular(radiusCard),
        ),
      ),
      dividerTheme: DividerThemeData(color: border, space: 1, thickness: 1),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size(0, primaryButtonHeight),
          backgroundColor: accent,
          foregroundColor: onAccent,
          disabledBackgroundColor: accent.withValues(alpha: 0.35),
          textStyle: const TextStyle(
            fontSize: 16,
            fontWeight: FontWeight.w600,
            letterSpacing: -0.1,
          ),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusControl)),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size(0, minTouchTarget + 4),
          foregroundColor: text,
          backgroundColor: brightness == Brightness.dark
              ? Colors.white.withValues(alpha: 0.04)
              : Colors.transparent,
          side: BorderSide(color: border),
          textStyle: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusControl)),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          minimumSize: const Size(0, minTouchTarget),
          foregroundColor: accent,
          textStyle: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600),
        ),
      ),
      floatingActionButtonTheme: FloatingActionButtonThemeData(
        backgroundColor: accent,
        foregroundColor: onAccent,
        elevation: 0,
        focusElevation: 0,
        hoverElevation: 0,
        highlightElevation: 0,
        extendedTextStyle: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusControl)),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: brightness == Brightness.dark ? Colors.white.withValues(alpha: 0.05) : surface,
        constraints: const BoxConstraints(minHeight: primaryButtonHeight),
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
        labelStyle: TextStyle(color: textMuted),
        floatingLabelStyle: TextStyle(color: accent),
        // Рамка по умолчанию не рисуется: поле — заливка, а не коробка. Обводка появляется
        // там, где она несёт смысл, — на фокусе и на ошибке.
        border: OutlineInputBorder(
          borderSide: BorderSide.none,
          borderRadius: BorderRadius.circular(radiusControl),
        ),
        enabledBorder: OutlineInputBorder(
          borderSide: BorderSide(color: border),
          borderRadius: BorderRadius.circular(radiusControl),
        ),
        focusedBorder: OutlineInputBorder(
          borderSide: BorderSide(color: accent, width: 2),
          borderRadius: BorderRadius.circular(radiusControl),
        ),
        errorBorder: OutlineInputBorder(
          borderSide: BorderSide(color: danger),
          borderRadius: BorderRadius.circular(radiusControl),
        ),
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: brightness == Brightness.dark
            ? const Color(0xFF0C1211).withValues(alpha: 0.92)
            : surface,
        indicatorColor: accent.withValues(alpha: 0.22),
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        height: 68,
        labelTextStyle: WidgetStatePropertyAll(
          TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: textMuted),
        ),
      ),
      bottomSheetTheme: BottomSheetThemeData(
        backgroundColor: brightness == Brightness.dark ? _darkSurface : surface,
        surfaceTintColor: Colors.transparent,
        showDragHandle: true,
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
        ),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: brightness == Brightness.dark ? _darkSurface : surface,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusCard)),
      ),
      snackBarTheme: SnackBarThemeData(
        backgroundColor: brightness == Brightness.dark ? const Color(0xFF1C2624) : _lightText,
        contentTextStyle: const TextStyle(color: _darkText, fontWeight: FontWeight.w500),
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusControl)),
      ),
      segmentedButtonTheme: SegmentedButtonThemeData(
        style: SegmentedButton.styleFrom(
          backgroundColor: brightness == Brightness.dark
              ? Colors.white.withValues(alpha: 0.04)
              : surface,
          selectedBackgroundColor: accent,
          selectedForegroundColor: onAccent,
          foregroundColor: textMuted,
          side: BorderSide(color: border),
          textStyle: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusControl)),
        ),
      ),
      tabBarTheme: TabBarThemeData(
        labelColor: text,
        unselectedLabelColor: textMuted,
        indicatorColor: accent,
        dividerColor: Colors.transparent,
        labelStyle: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600),
      ),
      progressIndicatorTheme: ProgressIndicatorThemeData(
        color: accent,
        linearTrackColor: border,
        circularTrackColor: Colors.transparent,
      ),
    );
  }

  /// Крупные заголовки с плотным трекингом и цифры фиксированной ширины: суммы и таймеры не
  /// должны дёргаться при каждом обновлении.
  static TextTheme _typography(TextTheme base) => base.copyWith(
        displaySmall: base.displaySmall?.copyWith(
          fontWeight: FontWeight.w700,
          letterSpacing: -1.4,
          fontFeatures: const [FontFeature.tabularFigures()],
        ),
        headlineLarge: base.headlineLarge?.copyWith(fontWeight: FontWeight.w700, letterSpacing: -1),
        headlineMedium: base.headlineMedium?.copyWith(
          fontWeight: FontWeight.w700,
          letterSpacing: -0.8,
          fontFeatures: const [FontFeature.tabularFigures()],
        ),
        headlineSmall: base.headlineSmall?.copyWith(fontWeight: FontWeight.w700, letterSpacing: -0.5),
        titleLarge: base.titleLarge?.copyWith(fontWeight: FontWeight.w700, letterSpacing: -0.4),
        titleMedium: base.titleMedium?.copyWith(fontWeight: FontWeight.w600, letterSpacing: -0.2),
        bodyLarge: base.bodyLarge?.copyWith(letterSpacing: -0.1),
        bodyMedium: base.bodyMedium?.copyWith(letterSpacing: -0.1),
        labelLarge: base.labelLarge?.copyWith(fontWeight: FontWeight.w600),
      );
}
