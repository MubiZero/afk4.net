import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/dashboard/quick_actions.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/shell/app_scaffold.dart';
import 'package:afk4_customer_app/shell/pressable.dart';

Widget wrap(Widget child) => MaterialApp(
  locale: const Locale('ru'),
  localizationsDelegates: appLocalizationsDelegates,
  supportedLocales: appSupportedLocales,
  home: child,
);

void main() {
  // Крупный заголовок при открытии и компактный на середине списка: шапка отдаёт место
  // содержимому, а не занимает его всегда.
  testWidgets('шапка сжимается при прокрутке', (tester) async {
    await tester.pumpWidget(
      wrap(
        AppScaffold(
          eyebrow: 'С возвращением',
          title: 'Иван',
          slivers: [
            SliverList.list(
              children: [
                for (var i = 0; i < 30; i++) SizedBox(height: 60, child: Text('строка $i')),
              ],
            ),
          ],
        ),
      ),
    );
    await tester.pumpAndSettle();

    final expanded = tester.getSize(find.byType(AppBar)).height;

    await tester.drag(find.byType(CustomScrollView), const Offset(0, -300));
    await tester.pumpAndSettle();

    expect(tester.getSize(find.byType(AppBar)).height, lessThan(expanded));
    // Заголовок раздела не пропадает совсем: свёрнутая шапка продолжает говорить, где мы.
    expect(find.text('Иван'), findsOneWidget);
  });

  testWidgets('без надстрочника шапка ниже — лишней пустоты нет', (tester) async {
    Future<double> heightOf(String? eyebrow) async {
      await tester.pumpWidget(
        wrap(
          AppScaffold(
            eyebrow: eyebrow,
            title: 'Брони',
            slivers: const [SliverToBoxAdapter(child: SizedBox(height: 400))],
          ),
        ),
      );
      await tester.pumpAndSettle();
      return tester.getSize(find.byType(AppBar)).height;
    }

    final withEyebrow = await heightOf('С возвращением');
    final without = await heightOf(null);

    expect(without, lessThan(withEyebrow));
  });

  testWidgets('плитки действий в ряду одной ширины', (tester) async {
    await tester.pumpWidget(
      wrap(
        Scaffold(
          body: QuickActions(
            actions: [
              QuickAction(icon: Icons.event_outlined, label: 'Забронировать', onOpen: () {}),
              QuickAction(icon: Icons.savings_outlined, label: 'Кешбэк', onOpen: () {}),
            ],
          ),
        ),
      ),
    );

    final first = tester.getSize(
      find.ancestor(of: find.text('Забронировать'), matching: find.byType(Pressable)),
    );
    final second = tester.getSize(
      find.ancestor(of: find.text('Кешбэк'), matching: find.byType(Pressable)),
    );

    expect(first.width, second.width);
  });

  // Нечётное последнее действие занимает свою половину: растянутое на весь ряд читается как
  // отдельный, более важный блок.
  testWidgets('третья плитка не растягивается на весь ряд', (tester) async {
    await tester.pumpWidget(
      wrap(
        Scaffold(
          body: QuickActions(
            actions: [
              QuickAction(icon: Icons.event_outlined, label: 'Забронировать', onOpen: () {}),
              QuickAction(icon: Icons.local_cafe_outlined, label: 'Заказать', onOpen: () {}),
              QuickAction(icon: Icons.savings_outlined, label: 'Кешбэк', onOpen: () {}),
            ],
          ),
        ),
      ),
    );

    final row = tester.getSize(find.byType(QuickActions)).width;
    final third = tester.getSize(
      find.ancestor(of: find.text('Кешбэк'), matching: find.byType(Pressable)),
    );

    expect(third.width, lessThan(row / 2 + 1));
  });

  testWidgets('нажатие видно из-под пальца', (tester) async {
    await tester.pumpWidget(
      wrap(
        Scaffold(
          body: Center(
            child: Pressable(onPressed: () {}, child: const Text('плитка')),
          ),
        ),
      ),
    );

    double scale() => tester.widget<AnimatedScale>(find.byType(AnimatedScale)).scale;
    expect(scale(), 1);

    final gesture = await tester.startGesture(tester.getCenter(find.text('плитка')));
    await tester.pump();
    expect(scale(), lessThan(1));

    await gesture.up();
    await tester.pumpAndSettle();
    expect(scale(), 1);
  });

  // Обещать отклик там, где ничего не произойдёт, хуже, чем не обещать.
  testWidgets('невключённая плитка не проседает', (tester) async {
    await tester.pumpWidget(
      wrap(
        const Scaffold(
          body: Center(child: Pressable(onPressed: null, child: Text('плитка'))),
        ),
      ),
    );

    final gesture = await tester.startGesture(tester.getCenter(find.text('плитка')));
    await tester.pump();

    expect(tester.widget<AnimatedScale>(find.byType(AnimatedScale)).scale, 1);
    await gesture.up();
  });
}
