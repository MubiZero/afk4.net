import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/shell/push_note.dart';
import 'package:afk4_customer_app/theme/app_theme.dart';

double contrast(Color a, Color b) {
  final la = a.computeLuminance();
  final lb = b.computeLuminance();
  final (hi, lo) = la > lb ? (la, lb) : (lb, la);
  return (hi + 0.05) / (lo + 0.05);
}

void main() {
  // Фирменный акцент — единственное, что приложение делит с Оператором и вебом: по нему
  // продукт узнаётся. Поверхности у игрока свои (тёмная витрина против плотной админки),
  // а emerald обязан совпадать со значением `--accent` в packages/tokens/tokens.css.
  //
  // В светлой теме акцент — `--accent-text` тех же токенов (#087A53), а не `--accent`
  // (#0B9E74): им здесь набраны и надписи, и заливка кнопок, а #0B9E74 на белом даёт 3,4:1 —
  // ниже порога для текста. Веб пришёл к тому же значению для текста по той же причине.
  test('акцент совпадает с продуктовым emerald в обеих темах', () {
    expect(AppTheme.dark().colorScheme.primary, const Color(0xFF2CC592));
    expect(AppTheme.light().colorScheme.primary, const Color(0xFF087A53));
  });

  // Светлая тема была написана, но не включалась, и её акцент никто не проверял на белом
  // листе. Акцентом набраны ссылки и текстовые кнопки, на нём же стоят надписи главных кнопок —
  // оба сочетания обязаны читаться, и для фирменного цвета, и для цвета клуба.
  test('в светлой теме акцент читается на листе и надпись читается на акценте', () {
    for (final club in <Color?>[
      null,
      const Color(0xFFF5D90A), // жёлтый
      const Color(0xFF22D3EE), // бирюзовый
      const Color(0xFFD64545), // красный
      const Color(0xFF1E3A8A), // тёмно-синий
    ]) {
      final scheme = AppTheme.light(clubColor: club).colorScheme;
      final name = club?.toString() ?? 'emerald';
      expect(contrast(scheme.primary, scheme.surface), greaterThanOrEqualTo(4.5), reason: name);
      expect(contrast(scheme.onPrimary, scheme.primary), greaterThanOrEqualTo(4.5), reason: name);
    }
  });

  // Второстепенный текст — подзаголовки витрины и входа, подписи сумм — стоит прямо на холсте
  // и на листах. Проверяется по значениям: на свете зала пиксельная проверка экрана врёт.
  test('второстепенный текст читается на холсте и на листе в обеих темах', () {
    for (final theme in [AppTheme.dark(), AppTheme.light()]) {
      final muted = theme.colorScheme.onSurfaceVariant;
      for (final background in [theme.canvasColor, theme.colorScheme.surface]) {
        final shown = Color.alphaBlend(muted, background);
        expect(contrast(shown, background), greaterThanOrEqualTo(4.5),
            reason: '${theme.brightness} on $background');
      }
    }
  });

  // Ошибкой набран текст отказа, и стоит он не только на белом, но и на подкрашенных
  // подложках карточек.
  test('в светлой теме текст ошибки читается и на подкрашенной подложке', () {
    final scheme = AppTheme.light().colorScheme;
    for (final background in [scheme.surface, AppTheme.light().canvasColor, scheme.secondaryContainer]) {
      expect(contrast(scheme.error, background), greaterThanOrEqualTo(4.5), reason: '$background');
    }
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
    expect(contrast(AppTheme.light().colorScheme.onPrimary, AppTheme.light().colorScheme.primary),
        greaterThanOrEqualTo(4.5));
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

  // Палец, а не мышь: 48 — минимум для касания. Главная кнопка экрана крупнее минимума,
  // иначе основное действие требует прицеливания.
  test('минимальная высота интерактивных элементов не ниже 48', () {
    expect(AppTheme.minTouchTarget, greaterThanOrEqualTo(48));
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
  test('надпись на цвете клуба выбирается по контрасту', () {
    expect(AppTheme.onAccentFor(const Color(0xFFF5D90A)), isNot(Colors.white));
    expect(AppTheme.onAccentFor(const Color(0xFF1E3A8A)), Colors.white);
    // Чистый красный и оранжевый темнее порога яркости 0,42, но тёмная надпись на них
    // контрастнее белой: белая на красном даёт 4:1, тёмная — 4,7:1.
    for (final accent in [
      ...awkwardClubColors,
      const Color(0xFFFF0000),
      const Color(0xFFFF8C00),
      const Color(0xFF00A3FF),
      AppTheme.emerald,
    ]) {
      expect(AppTheme.onAccentFor(accent), moreContrastingLabel(accent), reason: '$accent');
    }
  });

  // Цвет клуба задаёт владелец, и он бывает любым: насыщенный синий почти не отличается по
  // яркости от чёрного фона, почти белый — от белого листа. Приводить его только по светлоте
  // мало: у синего и жёлтого одной светлоты яркость различается в разы. Порог — контраст с
  // каждым фоном, на котором акцентом набраны надписи: холст, лист, карточка и подсветка
  // выбранного чипа.
  group('цвет клуба читается в обеих темах', () {
    for (final club in <Color?>[null, ...awkwardClubColors]) {
      final name = club == null ? 'emerald' : '#${club.toARGB32().toRadixString(16)}';
      for (final build in [AppTheme.dark, AppTheme.light]) {
        final theme = build(clubColor: club);
        final scheme = theme.colorScheme;
        final label = '$name, ${theme.brightness.name}';

        test('акцент как текст — $label', () {
          final backgrounds = [theme.canvasColor, scheme.surface, scheme.surfaceContainerHighest];
          for (final background in [
            ...backgrounds,
            // Выбранный чип и строка филиала: подпись акцентом на его же подсветке.
            for (final base in [scheme.surface, scheme.surfaceContainerHighest])
              Color.alphaBlend(scheme.primary.withValues(alpha: 0.12), base),
          ]) {
            expect(contrast(scheme.primary, background), greaterThanOrEqualTo(4.5),
                reason: '${scheme.primary} on $background');
          }
          // Значки на подложках плотнее — плитки быстрых действий, карточка кошелька.
          for (final base in backgrounds) {
            final plate = Color.alphaBlend(scheme.primary.withValues(alpha: 0.18), base);
            expect(contrast(scheme.primary, plate), greaterThanOrEqualTo(3),
                reason: '${scheme.primary} on $plate');
          }
        });

        test('надпись на кнопке — $label', () {
          expect(scheme.onPrimary, moreContrastingLabel(scheme.primary));
          expect(contrast(scheme.onPrimary, scheme.primary), greaterThanOrEqualTo(4.5));
        });

        test('текст на подсвеченной подложке — $label', () {
          expect(contrast(scheme.onSecondaryContainer, scheme.secondaryContainer),
              greaterThanOrEqualTo(4.5));
        });
      }
    }
  });

  // Полоса уведомления залита подсвеченной подложкой. Кнопка «Открыть» на ней была набрана
  // акцентом, и в светлой теме фирменный акцент на собственной подсветке давал 3,9:1.
  testWidgets('кнопка на полосе уведомления читается на её подложке', (tester) async {
    for (final club in <Color?>[null, ...awkwardClubColors]) {
      for (final theme in [AppTheme.dark(clubColor: club), AppTheme.light(clubColor: club)]) {
        await tester.pumpWidget(MaterialApp(
          theme: theme,
          locale: const Locale('ru'),
          localizationsDelegates: appLocalizationsDelegates,
          supportedLocales: appSupportedLocales,
          home: Scaffold(body: PushNote(text: 'Бронь подтверждена', onOpen: () {}, onDismiss: () {})),
        ));
        // Смена темы в MaterialApp анимирована: без ожидания видны цвета предыдущей.
        await tester.pumpAndSettle();

        final open = tester.widget<Text>(
          find.descendant(of: find.byType(TextButton), matching: find.byType(Text)),
        );
        final shown = DefaultTextStyle.of(tester.element(find.byWidget(open))).style.color;
        expect(contrast(shown!, theme.colorScheme.secondaryContainer), greaterThanOrEqualTo(4.5),
            reason: '$club, ${theme.brightness.name}');
      }
    }
  });
}

/// Цвета клубов, на которых подбор акцента ломается первым.
const awkwardClubColors = <Color>[
  Color(0xFF0000FF), // насыщенный синий: яркость как у тёмно-серого
  Color(0xFF8B0000), // тёмно-красный
  Color(0xFF0A0A0A), // почти чёрный
  Color(0xFFFAFAFA), // почти белый
  Color(0xFFFFEB3B), // жёлтый
  Color(0xFFF5D90A), // жёлтый логотип
  Color(0xFF22D3EE), // бирюзовый
  Color(0xFFD64545), // красный
  Color(0xFF1E3A8A), // тёмно-синий
  Color(0xFF0A1240), // почти чёрный синий
];

/// Из тёмной и белой надписи — та, что контрастнее с цветом кнопки.
Color moreContrastingLabel(Color accent) {
  const darkLabel = Color(0xFF04120D);
  return contrast(darkLabel, accent) >= contrast(Colors.white, accent) ? darkLabel : Colors.white;
}
