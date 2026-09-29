import 'package:flutter/material.dart';

import '../theme/space.dart';

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

    return Center(
      child: Padding(
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
