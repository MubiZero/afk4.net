import 'package:flutter/material.dart';

import '../theme/space.dart';

/// Состояние экрана по центру доступного места — и с прокруткой, если места не хватает.
///
/// Пусто и ошибка встают и в низкую вкладку, и в список без границ. На двукратном шрифте текст
/// с кнопкой в низкую вкладку не помещался и вылезал за край; прокрутка появляется только там,
/// где высота ограничена, — в списке без границ она не нужна и сломала бы его.
Widget centeredState(Widget child) => LayoutBuilder(
      builder: (context, constraints) => constraints.hasBoundedHeight
          ? SingleChildScrollView(
              child: ConstrainedBox(
                constraints: BoxConstraints(minHeight: constraints.maxHeight),
                child: Center(child: child),
              ),
            )
          : Center(child: child),
    );

/// Пусто — и что с этим делать.
///
/// Пустота жила в трёх видах и четырёх копиях: значок с заголовком по центру, серая строка без
/// следующего шага, текст с кнопкой под ним. Один вид на всё приложение: значок, что здесь будет,
/// строка о том, откуда оно берётся, и — если есть — одна кнопка следующего шага.
class EmptyState extends StatelessWidget {
  const EmptyState({super.key, required this.icon, required this.title, this.hint, this.action});

  final IconData icon;
  final String title;
  final String? hint;

  /// Обычно [PrimaryButton] или [SecondaryButton]: одна дверь, а не две.
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return centeredState(
      Padding(
        padding: const EdgeInsets.symmetric(horizontal: Space.s6, vertical: Space.s8),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 40, color: theme.colorScheme.onSurfaceVariant),
            const SizedBox(height: Space.s3),
            Text(title, textAlign: TextAlign.center, style: theme.textTheme.titleMedium),
            if (hint case final text?) ...[
              const SizedBox(height: Space.s1),
              Text(
                text,
                textAlign: TextAlign.center,
                style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
            ],
            if (action case final button?) ...[
              const SizedBox(height: Space.s4),
              button,
            ],
          ],
        ),
      ),
    );
  }
}
