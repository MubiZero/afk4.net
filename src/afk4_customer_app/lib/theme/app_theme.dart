import 'package:flutter/material.dart';

import 'app_palette.dart';

/// Тема клиентского приложения.
///
/// Приложение игрока — не админка: им пользуются в тёмном зале, между катками, одной рукой.
/// Отсюда собственная палитра, а не перенос `packages/tokens/tokens.css`: у Оператора светлый
/// плотный интерфейс за стойкой, у игрока — тёмная витрина с крупными цифрами. Общим остаётся
/// фирменный emerald: по нему приложение и узнаётся как AFK4.
///
/// Тёмная тема здесь не «ночной режим», а основной вид продукта — как у игровых площадок,
/// рядом с которыми это приложение лежит на телефоне, и она включена, пока игрок не выбрал
/// другое. Светлую (или «как в системе») игрок включает сам, в профиле; проектируется тёмная,
/// а светлая обязана на тех же экранах читаться не хуже.
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
  // `--accent-text` токенов, а не `--accent`: акцентом здесь набраны надписи и залиты кнопки
  // с белым текстом, и #0B9E74 на белом давал 3,4:1 — ниже порога для текста.
  static const Color _lightAccent = Color(0xFF087A53);
  static const Color _lightOnAccent = Color(0xFFFFFFFF);
  // Темнее `--danger` (#DC2626): тот на подкрашенных подложках карточек опускался до 4,2:1.
  static const Color _lightDanger = Color(0xFFB91C1C);

  /// Минимальная сторона зоны касания: 48 — минимум Material, он же с запасом покрывает 44 у
  /// Apple. Приложение держат одной рукой, между катками, в тёмном зале — прицеливаться там
  /// некогда.
  static const double minTouchTarget = 48;

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

  /// Цвет клуба из его брендинга. `null` — цвет не задан или записан не цветом: тогда
  /// приложение остаётся фирменно зелёным, а не красится в случайное значение из базы.
  ///
  /// Принимаются `#RGB`, `#RRGGBB` и `#AARRGGBB`; прозрачность отбрасывается — полупрозрачный
  /// акцент дал бы нечитаемый текст на кнопках.
  static Color? parseBrandColor(String? value) {
    final hex = value?.trim().replaceFirst('#', '');
    if (hex == null || hex.isEmpty) return null;

    final normalized = switch (hex.length) {
      3 => hex.split('').map((character) => '$character$character').join(),
      6 => hex,
      8 => hex.substring(2),
      _ => null,
    };
    final parsed = normalized == null ? null : int.tryParse(normalized, radix: 16);
    return parsed == null ? null : Color(0xFF000000 | parsed);
  }

  /// Порог WCAG для текста. Акцентом набраны ссылки, текстовые кнопки и подписи выбранных
  /// чипов, поэтому цвет клуба держит его, а не порог 3:1 для значков.
  static const double _textContrast = 4.5;

  /// Цвет клуба, приведённый к яркости, на которой им можно писать.
  ///
  /// Тёмно-синий логотип на почти чёрном фоне даст кнопку, которую не видно, а бледно-жёлтый
  /// на белом — то же самое в светлой теме. Одной светлоты для этого мало: насыщенный синий
  /// и жёлтый при одинаковой светлоте различаются по яркости в разы, и синий со светлотой 0,5
  /// на тёмном холсте давал 2:1. Поэтому цвет светлеет (в тёмной теме) или темнеет (в
  /// светлой), пока не прочитается на каждом фоне, где им пишут. Оттенок клуба сохраняется,
  /// меняется только светлота: это его цвет, просто читаемый.
  static Color _fitAccent(Color color, Brightness brightness) {
    final dark = brightness == Brightness.dark;
    final sheets = dark ? const [_darkSurface, _darkCard] : const [_lightSurface, _lightCard];
    return _readable(
      HSLColor.fromColor(color),
      lighten: dark,
      // Подсветка выбранного чипа на карточке клуба и строки филиала на листе — тот же акцент
      // на 12 %, и подпись на ней набрана им же, поэтому фон пересчитывается вместе с цветом.
      backgroundsFor: (accent) => [
        dark ? _darkCanvas : _lightCanvas,
        ...sheets,
        for (final sheet in sheets) Color.alphaBlend(accent.withValues(alpha: 0.12), sheet),
      ],
    );
  }

  /// Сдвигает светлоту [color], пока он не даст [_textContrast] с каждым из фонов. Дальше
  /// всего сдвиг доводит до белого или чёрного, а они читаются на любом фоне своей темы.
  static Color _readable(
    HSLColor color, {
    required bool lighten,
    required List<Color> Function(Color candidate) backgroundsFor,
  }) {
    var fitted = color;
    bool readable(Color candidate) => backgroundsFor(candidate)
        .every((background) => _contrast(candidate, background) >= _textContrast);
    bool canMove() => lighten ? fitted.lightness < 1 : fitted.lightness > 0;
    while (!readable(fitted.toColor()) && canMove()) {
      fitted = fitted.withLightness((fitted.lightness + (lighten ? 0.01 : -0.01)).clamp(0.0, 1.0));
    }
    return fitted.toColor();
  }

  /// Контраст по WCAG 2: отношение относительных яркостей, каждая с поправкой 0,05.
  static double _contrast(Color a, Color b) {
    final la = a.computeLuminance();
    final lb = b.computeLuminance();
    return la > lb ? (la + 0.05) / (lb + 0.05) : (lb + 0.05) / (la + 0.05);
  }

  /// Что писать на цвете клуба: из тёмной и белой надписи — ту, что контрастнее с ним. Не
  /// «всегда белое»: белые буквы на жёлтом не читаются, а чёрные на тёмно-синем — тем более.
  /// И не порог по яркости: равный контраст у этой пары при яркости около 0,19, и на чистом
  /// красном (0,21) порог 0,42 выбирал белую надпись с 4:1 вместо тёмной с 4,7:1.
  static Color onAccentFor(Color accent) =>
      _contrast(_darkOnAccent, accent) >= _contrast(Colors.white, accent)
          ? _darkOnAccent
          : Colors.white;

  /// [clubColor] — акцент из брендинга клуба; `null` оставляет фирменный emerald.
  static ThemeData dark({Color? clubColor}) {
    final accent = clubColor == null ? emerald : _fitAccent(clubColor, Brightness.dark);
    return _build(
      brightness: Brightness.dark,
      canvas: _darkCanvas,
      surface: _darkSurface,
      card: _darkCard,
      border: _darkBorder,
      text: _darkText,
      textMuted: _darkTextMuted,
      accent: accent,
      onAccent: clubColor == null ? _darkOnAccent : onAccentFor(accent),
      danger: _darkDanger,
      palette: AppPalette.dark,
    );
  }

  static ThemeData light({Color? clubColor}) {
    final accent = clubColor == null ? _lightAccent : _fitAccent(clubColor, Brightness.light);
    return _build(
      brightness: Brightness.light,
      canvas: _lightCanvas,
      surface: _lightSurface,
      card: _lightCard,
      border: _lightBorder,
      text: _lightText,
      textMuted: _lightTextMuted,
      accent: accent,
      onAccent: clubColor == null ? _lightOnAccent : onAccentFor(accent),
      danger: _lightDanger,
      palette: AppPalette.light,
    );
  }

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
    required AppPalette palette,
  }) {
    final dark = brightness == Brightness.dark;
    // Material красит этим всё «второстепенное подсвеченное»: тональные кнопки, выбранные
    // чипы, подложки подсказок. Без явного значения он выводит его из secondary и заливает
    // экран фиолетовым — чужим на фирменном зелёном. Задаётся здесь, а не в каждом виджете:
    // иначе фиолетовый вылезает в следующем же месте, где Material решит его применить.
    final secondaryContainer = Color.alphaBlend(
      accent.withValues(alpha: dark ? 0.20 : 0.22),
      dark ? _darkCard : _lightSurface,
    );
    // Текст полосы уведомления и приглашения к отзыву. Светлота 0,26 на светлой подложке
    // давала 3,8:1 даже фирменному зелёному, а жёлтому клубу — 3,4:1, поэтому она только
    // отправная точка, а дальше цвет темнеет (светлеет), пока не прочитается.
    final onSecondaryContainer = _readable(
      HSLColor.fromColor(accent).withLightness(dark ? 0.72 : 0.26),
      lighten: dark,
      backgroundsFor: (_) => [secondaryContainer],
    );

    final scheme = ColorScheme(
      brightness: brightness,
      primary: accent,
      onPrimary: onAccent,
      secondary: violet,
      onSecondary: Colors.white,
      secondaryContainer: secondaryContainer,
      onSecondaryContainer: onSecondaryContainer,
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
      extensions: [palette],
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
      // Выбранный чип по умолчанию красится фиолетовым secondaryContainer из палитры Material —
      // на фирменном тёмно-зелёном экране это чужой цвет. Выбор — такое же утверждение, как
      // нажатая кнопка, поэтому он тоже фирменный.
      chipTheme: ChipThemeData(
        backgroundColor: card,
        selectedColor: accent,
        checkmarkColor: onAccent,
        side: BorderSide(color: border),
        labelStyle: TextStyle(color: text, fontSize: 14, fontWeight: FontWeight.w600),
        secondaryLabelStyle: TextStyle(color: onAccent, fontSize: 14, fontWeight: FontWeight.w600),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusControl)),
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      ),
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
          minimumSize: const Size(0, minTouchTarget),
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
