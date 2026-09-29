import 'package:flutter/material.dart';

import '../theme/app_palette.dart';
import '../theme/space.dart';

/// Что значит состояние — не какой у него цвет.
enum StatusTone {
  /// Справка: закрыто, отменено, истекло.
  neutral,

  /// Ждёт чужого решения: заявка на рассмотрении, заказ готовят.
  waiting,

  /// Всё в порядке: подтверждена, зачислено, принесли.
  positive,

  /// Плохо для игрока: отклонена, не пришёл.
  negative,
}

/// Состояние строки одним словом на подложке своего цвета.
///
/// Состояние было в трёх видах: серой строкой справа, цветным заголовком, цветным словом в
/// строке. Бейдж узнаётся взглядом раньше, чем читается, и одинаков у брони, заказа и заявки.
class StatusBadge extends StatelessWidget {
  const StatusBadge({super.key, required this.label, this.tone = StatusTone.neutral});

  final String label;
  final StatusTone tone;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final color = switch (tone) {
      StatusTone.neutral => theme.colorScheme.onSurfaceVariant,
      StatusTone.waiting => theme.colorScheme.primary,
      StatusTone.positive => AppPalette.of(context).success,
      StatusTone.negative => theme.colorScheme.error,
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: Space.s3, vertical: Space.s1),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(label, style: theme.textTheme.labelMedium?.copyWith(color: color)),
    );
  }
}
