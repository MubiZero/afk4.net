import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/theme/brand_mark.dart';

/// Знак — не оформление экрана, а перенос `brand/afk4-mark.svg`. Правила здесь те же, что
/// в `brand/README.md`: если знак разойдётся с мастер-файлом, приложение перестанет быть
/// тем же продуктом, что сайт и Оператор.
void main() {
  test('активны ровно три ячейки по диагонали', () {
    final active = [
      for (var row = 0; row < 3; row++)
        for (var column = 0; column < 3; column++)
          if (BrandMark.isActive(row, column)) (row, column),
    ];

    expect(active, [(0, 0), (1, 1), (2, 2)]);
  });

  test('цвета знака совпадают с мастер-файлом бренда', () {
    expect(BrandMark.accent, const Color(0xFF2DD4A7));
    expect(BrandMark.inactive, const Color(0xFF173028));
    expect(BrandMark.tile, const Color(0xFF0E2019));
  });

  // Словесный знак всегда заглавными, `.NET` — акцентом. Правило бренда, а не вкус экрана.
  testWidgets('словесный знак заглавный, а «.NET» выделен акцентом', (tester) async {
    await tester.pumpWidget(const MaterialApp(home: Scaffold(body: BrandMark())));

    final text = tester.widget<Text>(find.byType(Text));
    final spans = (text.textSpan! as TextSpan).children!.cast<TextSpan>();

    expect(spans.map((span) => span.text).join(), 'AFK4.NET');
    expect(spans.last.text, '.NET');
    expect(spans.last.style?.color, BrandMark.accent);
  });

  testWidgets('знак умеет жить без слова — там, где название уже написано', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(home: Scaffold(body: BrandMark(showWordmark: false))),
    );

    expect(find.byType(Text), findsNothing);
    expect(find.byType(CustomPaint), findsWidgets);
  });
}
