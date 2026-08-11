import 'package:flutter/material.dart';

/// Тема приложения — перенос продуктовой палитры из `packages/tokens/tokens.css`.
///
/// Значения скопированы, а не подобраны: клиентское приложение и веб-версия живут на одном
/// телефоне, и расхождение оттенка читается как два разных продукта. Когда палитра в токенах
/// меняется, правится и здесь — тест темы держит соответствие.
class AppTheme {
  const AppTheme._();

  // [data-theme="dark"]
  static const _darkCanvas = Color(0xFF121212);
  static const _darkElevated = Color(0xFF1E1E1E);
  static const _darkCard = Color(0xFF242424);
  static const _darkBorder = Color(0xFF3A3A3A);
  static const _darkTextPrimary = Color(0xEBFFFFFF);
  static const _darkTextSecondary = Color(0xB3FFFFFF);
  static const _darkAccent = Color(0xFF2CC592);
  static const _darkOnAccent = Color(0xFF0A1A14);
  static const _darkDanger = Color(0xFFF87171);

  // [data-theme="light"]
  static const _lightCanvas = Color(0xFFEEF2F7);
  static const _lightElevated = Color(0xFFFFFFFF);
  static const _lightCard = Color(0xFFF7F9FC);
  static const _lightBorder = Color(0xFFCBD5E1);
  static const _lightTextPrimary = Color(0xFF0F172A);
  static const _lightTextSecondary = Color(0xFF475569);
  static const _lightAccent = Color(0xFF0B9E74);
  static const _lightOnAccent = Color(0xFF0F172A);
  static const _lightDanger = Color(0xFFDC2626);

  /// Минимальная сторона зоны касания. Веб-версия жила с 36 (мышь), приложению этого мало:
  /// 44 — общий минимум Apple и WCAG, ниже палец начинает промахиваться.
  static const double minTouchTarget = 44;

  static ThemeData dark() => _build(
        brightness: Brightness.dark,
        canvas: _darkCanvas,
        elevated: _darkElevated,
        card: _darkCard,
        border: _darkBorder,
        textPrimary: _darkTextPrimary,
        textSecondary: _darkTextSecondary,
        accent: _darkAccent,
        onAccent: _darkOnAccent,
        danger: _darkDanger,
      );

  static ThemeData light() => _build(
        brightness: Brightness.light,
        canvas: _lightCanvas,
        elevated: _lightElevated,
        card: _lightCard,
        border: _lightBorder,
        textPrimary: _lightTextPrimary,
        textSecondary: _lightTextSecondary,
        accent: _lightAccent,
        onAccent: _lightOnAccent,
        danger: _lightDanger,
      );

  static ThemeData _build({
    required Brightness brightness,
    required Color canvas,
    required Color elevated,
    required Color card,
    required Color border,
    required Color textPrimary,
    required Color textSecondary,
    required Color accent,
    required Color onAccent,
    required Color danger,
  }) {
    final scheme = ColorScheme(
      brightness: brightness,
      primary: accent,
      onPrimary: onAccent,
      secondary: accent,
      onSecondary: onAccent,
      error: danger,
      onError: brightness == Brightness.dark ? _darkOnAccent : Colors.white,
      surface: elevated,
      onSurface: textPrimary,
      surfaceContainerHighest: card,
      outline: border,
    );

    return ThemeData(
      useMaterial3: true,
      brightness: brightness,
      colorScheme: scheme,
      scaffoldBackgroundColor: canvas,
      textTheme: Typography.material2021(platform: TargetPlatform.android)
          .black
          .apply(bodyColor: textPrimary, displayColor: textPrimary),
      appBarTheme: AppBarTheme(
        backgroundColor: canvas,
        foregroundColor: textPrimary,
        elevation: 0,
        centerTitle: false,
      ),
      cardTheme: CardThemeData(
        color: card,
        elevation: 0,
        shape: RoundedRectangleBorder(
          side: BorderSide(color: border),
          borderRadius: BorderRadius.circular(12),
        ),
      ),
      dividerTheme: DividerThemeData(color: border, space: 1, thickness: 1),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size(0, minTouchTarget),
          backgroundColor: accent,
          foregroundColor: onAccent,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size(0, minTouchTarget),
          foregroundColor: textPrimary,
          side: BorderSide(color: border),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          minimumSize: const Size(0, minTouchTarget),
          foregroundColor: accent,
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: elevated,
        constraints: const BoxConstraints(minHeight: minTouchTarget),
        labelStyle: TextStyle(color: textSecondary),
        border: OutlineInputBorder(
          borderSide: BorderSide(color: border),
          borderRadius: BorderRadius.circular(8),
        ),
        enabledBorder: OutlineInputBorder(
          borderSide: BorderSide(color: border),
          borderRadius: BorderRadius.circular(8),
        ),
        focusedBorder: OutlineInputBorder(
          borderSide: BorderSide(color: accent, width: 2),
          borderRadius: BorderRadius.circular(8),
        ),
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: elevated,
        indicatorColor: accent.withValues(alpha: 0.18),
        surfaceTintColor: Colors.transparent,
      ),
    );
  }
}
