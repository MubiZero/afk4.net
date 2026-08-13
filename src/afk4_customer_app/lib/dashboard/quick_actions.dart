import 'package:flutter/material.dart';

import '../shell/pressable.dart';
import '../theme/app_theme.dart';

/// Одно быстрое действие главной.
class QuickAction {
  const QuickAction({required this.icon, required this.label, required this.onOpen});

  final IconData icon;
  final String label;
  final VoidCallback onOpen;
}

/// Сетка быстрых действий.
///
/// Раньше пополнение, заказ и кешбэк стояли на главной тремя одинаковыми строками одна под
/// другой, и найти нужную можно было только чтением: строки различались лишь текстом. Сетка
/// из плиток читается взглядом за один заход — иконка и короткое слово вместо абзаца, — и
/// занимает вдвое меньше высоты.
///
/// Действий бывает от двух до четырёх: клуб может не принимать брони, не иметь меню или
/// выключить кешбэк. Плитки просто не появляются — заглушек «недоступно» здесь нет, звать
/// в невозможное хуже, чем не звать.
class QuickActions extends StatelessWidget {
  const QuickActions({super.key, required this.actions});

  final List<QuickAction> actions;

  @override
  Widget build(BuildContext context) {
    if (actions.isEmpty) return const SizedBox.shrink();

    // По две в ряд: плитка шире половины экрана перестаёт быть плиткой, уже — не оставляет
    // места подписи на длинных языках.
    final rows = <List<QuickAction>>[];
    for (var index = 0; index < actions.length; index += 2) {
      rows.add(actions.sublist(index, (index + 2).clamp(0, actions.length)));
    }

    return Column(
      children: [
        for (final row in rows)
          Padding(
            padding: EdgeInsets.only(bottom: row == rows.last ? 0 : 12),
            // Соседние плитки одной высоты: на длинных языках подпись переносится на вторую
            // строку, и без выравнивания ряд получается ступенькой.
            child: IntrinsicHeight(
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  for (final action in row) ...[
                    Expanded(child: _Tile(action: action)),
                    if (action != row.last) const SizedBox(width: 12),
                  ],
                  // Нечётное последнее действие занимает свою половину, а не растягивается на
                  // весь ряд: иначе одна плитка выглядит как отдельный, более важный блок.
                  if (row.length == 1) ...[
                    const SizedBox(width: 12),
                    const Expanded(child: SizedBox.shrink()),
                  ],
                ],
              ),
            ),
          ),
      ],
    );
  }
}

class _Tile extends StatelessWidget {
  const _Tile({required this.action});

  final QuickAction action;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final radius = BorderRadius.circular(AppTheme.radiusControl);

    return Pressable(
      onPressed: action.onOpen,
      child: Container(
        // Высота держит зону касания заведомо больше минимума: в плитку целятся большим
        // пальцем на ходу.
        constraints: const BoxConstraints(minHeight: 88),
        decoration: BoxDecoration(
          color: theme.colorScheme.surfaceContainerHighest,
          borderRadius: radius,
          border: Border.all(color: theme.colorScheme.outline),
        ),
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Container(
              width: 36,
              height: 36,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: theme.colorScheme.primary.withValues(alpha: 0.14),
              ),
              child: Icon(action.icon, size: 20, color: theme.colorScheme.primary),
            ),
            const SizedBox(height: 10),
            Text(
              action.label,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: theme.textTheme.titleSmall,
            ),
          ],
        ),
      ),
    );
  }
}
