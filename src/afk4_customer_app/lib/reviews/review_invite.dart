import 'package:flutter/material.dart';

import '../api/contracts.dart';
import '../l10n/app_localizations.dart';
import '../theme/app_theme.dart';
import '../theme/space.dart';

/// Приглашение оценить последний визит.
///
/// Стоит на главной и только когда есть о чём спросить: сервер предлагает лишь свежий
/// неоценённый визит. «Не сейчас» убирает карточку до следующего запуска — отказ должен
/// работать сразу, иначе это не отказ, а кнопка без последствий.
class ReviewInvite extends StatelessWidget {
  const ReviewInvite({
    super.key,
    required this.visit,
    required this.onRate,
    required this.onDismiss,
  });

  final PendingClubReviewDto visit;
  final VoidCallback onRate;
  final VoidCallback onDismiss;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Container(
      padding: const EdgeInsets.all(Space.s4),
      decoration: BoxDecoration(
        color: theme.colorScheme.secondaryContainer,
        borderRadius: BorderRadius.circular(AppTheme.radiusCard),
        border: Border.all(color: theme.colorScheme.outline),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(Icons.star_rounded, color: theme.colorScheme.onSecondaryContainer),
              const SizedBox(width: Space.s2),
              Expanded(
                child: Text(
                  l.customerReviewInviteTitle,
                  style: theme.textTheme.titleMedium
                      ?.copyWith(color: theme.colorScheme.onSecondaryContainer),
                ),
              ),
            ],
          ),
          const SizedBox(height: Space.s1),
          Text(
            l.customerReviewInviteBody(visit.seatName, visit.branchName),
            style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
          const SizedBox(height: Space.s3),
          Row(
            children: [
              FilledButton(onPressed: onRate, child: Text(l.customerReviewInviteAction)),
              const SizedBox(width: Space.s2),
              TextButton(onPressed: onDismiss, child: Text(l.customerReviewInviteDismiss)),
            ],
          ),
        ],
      ),
    );
  }
}
