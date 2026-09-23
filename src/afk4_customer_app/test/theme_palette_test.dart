import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/packages/packages_screen.dart';
import 'package:afk4_customer_app/theme/app_palette.dart';
import 'package:afk4_customer_app/theme/app_theme.dart';

import 'support/fake_http.dart';

/// Контраст по WCAG: 4.5 — порог для текста, 3 — для значков и крупных знаков.
double contrast(Color a, Color b) {
  final la = a.computeLuminance();
  final lb = b.computeLuminance();
  final (hi, lo) = la > lb ? (la, lb) : (lb, la);
  return (hi + 0.05) / (lo + 0.05);
}

const _redClub = Color(0xFFD64545);

void main() {
  // Смысл цвета — «ошибка», «всё в порядке», «деньги прибыли» — живёт в теме. Сырой цвет в
  // виджете не знает ни про светлую тему, ни про цвет клуба: он выглядит правильно ровно на том
  // экране, где его подобрали, и нигде больше. Файлы темы и знак бренда — единственные места,
  // где цвет записывается числом.
  test('виджеты не пишут цвет числом в обход темы', () {
    final raw = RegExp(
      r'Color\(0x|Color\.from(ARGB|RGBO)|Colors\.(?!transparent\b)\w+|'
      r'AppTheme\.(emerald|emeraldBright|violet)\b',
    );
    final offenders = <String>[];
    for (final entity in Directory('lib').listSync(recursive: true)) {
      if (entity is! File || !entity.path.endsWith('.dart')) continue;
      final path = entity.path.replaceAll(r'\', '/');
      if (path.startsWith('lib/theme/')) continue;
      if (path.startsWith('lib/l10n/app_localizations')) continue;
      final lines = entity.readAsLinesSync();
      for (var index = 0; index < lines.length; index++) {
        final line = lines[index];
        if (line.trimLeft().startsWith('//')) continue;
        if (raw.hasMatch(line)) offenders.add('$path:${index + 1}: ${line.trim()}');
      }
    }
    expect(offenders, isEmpty, reason: offenders.join('\n'));
  });

  test('обе темы несут палитру смыслов', () {
    for (final theme in [AppTheme.dark(), AppTheme.light()]) {
      expect(theme.extension<AppPalette>(), isNotNull);
    }
  });

  // «Всё в порядке» и «деньги прибыли» — не цвет клуба. У клуба с красным логотипом приход
  // денег в выписке иначе окрашивался бы тем же красным, что и ошибка.
  test('успех и приход денег не перекрашиваются цветом клуба', () {
    final plain = AppTheme.dark().extension<AppPalette>()!;
    final red = AppTheme.dark(clubColor: _redClub).extension<AppPalette>()!;
    expect(red.success, plain.success);
    expect(red.income, plain.income);
    expect(red.success, isNot(AppTheme.dark(clubColor: _redClub).colorScheme.primary));
  });

  // Значения подобраны по фону, на котором они стоят: зелёный, читаемый на почти чёрном,
  // на белом листе выцветает до 2:1.
  test('смысловые цвета читаются на поверхностях своей темы', () {
    for (final theme in [AppTheme.dark(), AppTheme.light()]) {
      final palette = theme.extension<AppPalette>()!;
      final surfaces = [theme.colorScheme.surface, theme.colorScheme.surfaceContainerHighest];
      for (final surface in surfaces) {
        final name = '${theme.brightness} on $surface';
        expect(contrast(palette.success, surface), greaterThanOrEqualTo(4.5), reason: name);
        expect(contrast(palette.income, surface), greaterThanOrEqualTo(4.5), reason: name);
        expect(contrast(palette.rating, surface), greaterThanOrEqualTo(3), reason: name);
        expect(contrast(theme.colorScheme.error, surface), greaterThanOrEqualTo(3), reason: name);
      }
    }
  });

  // Фото зала и карта одинаковы в обеих темах, поэтому и подписи поверх них одни и те же:
  // белый текст на затемнении, а не «текст темы», который в светлой стал бы чёрным на чёрном.
  test('подписи поверх фото не зависят от темы и читаются на затемнении', () {
    final dark = AppTheme.dark().extension<AppPalette>()!;
    final light = AppTheme.light().extension<AppPalette>()!;
    expect(light.onMedia, dark.onMedia);
    expect(light.mediaScrim, dark.mediaScrim);
    expect(light.ratingOnMedia, dark.ratingOnMedia);

    // Худший случай — затемнение над белым кадром.
    final scrimOverWhite = Color.alphaBlend(dark.mediaScrim, Colors.white);
    expect(contrast(dark.onMedia, scrimOverWhite), greaterThanOrEqualTo(4.5));
    expect(contrast(Color.alphaBlend(dark.onMediaMuted, scrimOverWhite), scrimOverWhite),
        greaterThanOrEqualTo(4.5));
    expect(contrast(dark.ratingOnMedia, scrimOverWhite), greaterThanOrEqualTo(3));
  });

  test('без темы приложения виджет получает палитру по яркости, а не падает', () {
    expect(AppPalette.fallbackFor(Brightness.dark), AppTheme.dark().extension<AppPalette>());
    expect(AppPalette.fallbackFor(Brightness.light), AppTheme.light().extension<AppPalette>());
  });

  // Настоящий экран в клубе с красным логотипом: действующий пакет подписан цветом «в порядке»,
  // а не красным акцентом клуба и не фирменным зелёным числом, которое не знает о светлой теме.
  testWidgets('действующий пакет подписан цветом успеха из темы', (tester) async {
    final mine = jsonEncode([
      {
        'playerPackageId': 'own1',
        'packageDefinitionId': 'pkg1',
        'playerAccountId': 'player1',
        'name': 'Ночной 5ч',
        'purchasedPrice': {'currencyCode': 'TJS', 'minorUnits': 40000},
        'includedSeconds': 18000,
        'bonusSeconds': 0,
        'remainingIncludedSeconds': 9000,
        'remainingBonusSeconds': 0,
        'purchasedAtUtc': '2026-08-01T10:00:00Z',
        'expiresAtUtc': '2026-09-01T10:00:00Z',
      },
    ]);
    final http = FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/packages' => (mine, 200),
          _ => ('[]', 200),
        });

    for (final theme in [
      AppTheme.dark(clubColor: _redClub),
      AppTheme.light(clubColor: _redClub),
    ]) {
      await tester.pumpWidget(MaterialApp(
        theme: theme,
        locale: const Locale('ru'),
        localizationsDelegates: appLocalizationsDelegates,
        supportedLocales: appSupportedLocales,
        home: PackagesScreen(
          api: PlayerApiClient(baseUrl: 'https://api', httpClient: http),
          branchId: 'b1',
          clock: () => DateTime(2026, 8, 15, 12),
        ),
      ));
      await tester.pumpAndSettle();

      final left = tester.widget<Text>(find.textContaining('Осталось'));
      expect(left.style?.color, theme.extension<AppPalette>()!.success,
          reason: '${theme.brightness}');
    }
  });
}
