import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/organization/club_card.dart';
import 'package:afk4_customer_app/organization/club_map.dart';
import 'package:afk4_customer_app/organization/organization.dart';
import 'package:afk4_customer_app/theme/app_theme.dart';

/// 48 — минимум Material для касания пальцем; он же покрывает 44 у Apple. Меньшую цель игрок
/// промахивает одной рукой, между катками, в тёмном зале — ровно так, как этим приложением
/// и пользуются.
const double _minTarget = 48;

const _cyberx = Organization(
  organizationId: '11111111-1111-1111-1111-111111111111',
  slug: 'cyberx',
  name: 'CyberX',
  places: [
    ClubPlace(
      branchId: 'b1',
      name: 'На Рудаки',
      city: 'Душанбе',
      address: 'пр. Рудаки, 1',
      latitude: 38.5598,
      longitude: 68.7870,
    ),
  ],
  rating: 4.6,
  reviewCount: 12,
);

const _nightOwl = Organization(
  organizationId: '33333333-3333-3333-3333-333333333333',
  slug: 'nightowl',
  name: 'Night Owl',
  places: [
    ClubPlace(
      branchId: 'b2',
      name: 'Night Owl',
      city: 'Худжанд',
      latitude: 40.2833,
      longitude: 69.6167,
    ),
  ],
);

Widget _app(Widget body, {ThemeData? theme}) => MaterialApp(
      theme: theme ?? AppTheme.dark(),
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: Scaffold(body: body),
    );

void _expectAtLeastMinimum(Size size, String what) {
  expect(size.width, greaterThanOrEqualTo(_minTarget), reason: '$what: ширина $size');
  expect(size.height, greaterThanOrEqualTo(_minTarget), reason: '$what: высота $size');
}

void main() {
  // Оценка на фото — вход в отзывы. Плашка рисовалась 28 пунктов в высоту, и в неё же
  // приходилось попадать пальцем.
  testWidgets('оценка на карточке клуба нажимается пальцем, а не прицеливанием', (tester) async {
    var opened = 0;
    await tester.pumpWidget(_app(ClubCard(
      club: _cyberx,
      onTap: () {},
      onOpenReviews: () => opened++,
      clock: () => DateTime(2026, 8, 12, 14),
    )));
    await tester.pumpAndSettle();

    final target = find.bySemanticsLabel(RegExp('^Отзывы игроков'));
    _expectAtLeastMinimum(tester.getSize(target), 'оценка');

    // Касание у верхнего края зоны — выше нарисованной плашки — всё равно открывает отзывы:
    // проверяется, куда палец попадает, а не только размер разметки.
    final rect = tester.getRect(target);
    await tester.tapAt(rect.topCenter + const Offset(0, 2));
    expect(opened, 1);
  });

  // Точка клуба на карте: кружок 34 пункта в ячейке 44. Промах мимо точки уводит карту, а не
  // открывает клуб.
  testWidgets('точка клуба на карте нажимается пальцем', (tester) async {
    Organization? selected;
    await tester.pumpWidget(_app(ClubMap(
      clubs: const [_cyberx, _nightOwl],
      onSelected: (club) => selected = club,
    )));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));

    final pin = find.byTooltip('CyberX · пр. Рудаки, 1');
    _expectAtLeastMinimum(tester.getSize(pin), 'точка на карте');

    // Край зоны касания, а не центр кружка.
    final rect = tester.getRect(pin);
    await tester.tapAt(rect.centerLeft + const Offset(3, 0));
    expect(selected?.organizationId, _cyberx.organizationId);
  });

  // Тема задаёт минимальный размер кнопок. Для текстовых он был 44: на телефоне Material
  // добирал зону касания до 48 сам, но там, где он этого не делает (узкая плотность, мышь),
  // кнопка «Открыть», «Не сейчас» или «Отмена» оставалась 44.
  testWidgets('кнопки темы не меньше минимума сами, без надбавки платформы', (tester) async {
    for (final base in [AppTheme.dark(), AppTheme.light()]) {
      final theme = base.copyWith(materialTapTargetSize: MaterialTapTargetSize.shrinkWrap);
      await tester.pumpWidget(_app(
        Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextButton(onPressed: () {}, child: const Text('Текст')),
              OutlinedButton(onPressed: () {}, child: const Text('Контур')),
              FilledButton(onPressed: () {}, child: const Text('Заливка')),
            ],
          ),
        ),
        theme: theme,
      ));

      for (final type in [TextButton, OutlinedButton, FilledButton]) {
        expect(tester.getSize(find.byType(type)).height, greaterThanOrEqualTo(_minTarget),
            reason: '$type, ${base.brightness}');
      }
    }
  });

  // Общий сторож на витрину клубов: всё, что принимает касание, — не меньше 48×48 по дереву
  // доступности, то есть там, куда на самом деле попадает палец.
  testWidgets('на карточке клуба и на карте нет целей касания меньше 48', (tester) async {
    final semantics = tester.ensureSemantics();

    await tester.pumpWidget(_app(ClubCard(
      club: _cyberx,
      onTap: () {},
      onOpenReviews: () {},
      onOpenDetails: () {},
      clock: () => DateTime(2026, 8, 12, 14),
    )));
    await tester.pumpAndSettle();
    await expectLater(tester, meetsGuideline(androidTapTargetGuideline));

    await tester.pumpWidget(_app(ClubMap(clubs: const [_cyberx, _nightOwl], onSelected: (_) {})));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
    await expectLater(tester, meetsGuideline(androidTapTargetGuideline));

    semantics.dispose();
  });
}
