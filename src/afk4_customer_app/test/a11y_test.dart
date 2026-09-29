import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/shell/pressable.dart';
import 'package:afk4_customer_app/theme/app_theme.dart';

double _contrast(Color a, Color b) {
  final la = a.computeLuminance();
  final lb = b.computeLuminance();
  return la > lb ? (la + 0.05) / (lb + 0.05) : (lb + 0.05) / (la + 0.05);
}

void main() {
  // WCAG 1.4.11: граница элемента управления — не меньше 3:1 к соседним цветам. Рамка поля шла
  // цветом рамки карточек (1,3:1), и пустое поле на карточке не читалось полем.
  for (final (name, theme) in [('тёмная', AppTheme.dark()), ('светлая', AppTheme.light())]) {
    test('$name тема: рамка поля ввода держит 3:1 к фону и карточке', () {
      final border = (theme.inputDecorationTheme.enabledBorder! as OutlineInputBorder).borderSide.color;
      final fill = Color.alphaBlend(theme.inputDecorationTheme.fillColor!, theme.colorScheme.surfaceContainerHighest);
      for (final background in [theme.canvasColor, theme.colorScheme.surfaceContainerHighest, fill]) {
        expect(_contrast(border, background), greaterThanOrEqualTo(3), reason: '$background');
      }
    });

    // Подпись вкладки была 11 и отличалась у выбранной только подложкой значка.
    test('$name тема: подпись вкладки не мельче 12, выбранная отличима', () {
      final labels = theme.navigationBarTheme.labelTextStyle!;
      final idle = labels.resolve(<WidgetState>{})!;
      final chosen = labels.resolve({WidgetState.selected})!;
      expect(idle.fontSize, greaterThanOrEqualTo(12));
      expect(chosen.fontSize, greaterThanOrEqualTo(12));
      expect(chosen.fontWeight, isNot(idle.fontWeight));
      expect(chosen.color, isNot(idle.color));
    });
  }

  // Плитки и карточки клубов — кнопки и с клавиатуры: в веб-сборке их нельзя было выбрать Tab-ом.
  testWidgets('плитка получает фокус с клавиатуры и нажимается Enter', (tester) async {
    var pressed = 0;
    await tester.pumpWidget(MaterialApp(
      theme: AppTheme.dark(),
      home: Scaffold(
        body: Center(
          child: Pressable(onPressed: () => pressed++, child: const SizedBox(width: 120, height: 80)),
        ),
      ),
    ));

    await tester.sendKeyEvent(LogicalKeyboardKey.tab);
    await tester.pump();
    await tester.sendKeyEvent(LogicalKeyboardKey.enter);
    await tester.pump();

    expect(pressed, 1);
  });
}
