import 'dart:async';

import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import 'live_session.dart';

/// Текущая сессия: где игрок сидит и сколько времени идёт или осталось.
class LiveSessionCard extends StatefulWidget {
  const LiveSessionCard({
    super.key,
    required this.session,
    required this.fetchedAt,
    this.clock = DateTime.now,
  });

  final ActiveSession session;

  /// Момент ответа сервера. От него отсчитывается остаток оплаченной сессии.
  final DateTime fetchedAt;

  /// Подменяется тестом, чтобы часы не зависели от настоящего времени.
  final DateTime Function() clock;

  @override
  State<LiveSessionCard> createState() => _LiveSessionCardState();
}

class _LiveSessionCardState extends State<LiveSessionCard> {
  Timer? _ticker;

  @override
  void initState() {
    super.initState();
    _ticker = Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted) setState(() {});
    });
  }

  @override
  void dispose() {
    _ticker?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final session = widget.session;
    final now = widget.clock();
    final fixed = session.durationMode == SessionDurationMode.fixed;

    final remaining = fixed
        ? projectRemainingSeconds(session.remainingSeconds ?? 0, widget.fetchedAt, now: now)
        : null;
    final clock = formatClock(remaining ?? elapsedSeconds(session.startedAtUtc, now: now));
    final urgency = remaining == null ? RemainingUrgency.calm : remainingUrgency(remaining);
    final alarming = urgency != RemainingUrgency.calm;

    final warning = switch (urgency) {
      RemainingUrgency.ended => l.customerDashboardSessionEnded,
      RemainingUrgency.endingSoon => l.customerDashboardSessionEndingSoon,
      RemainingUrgency.calm => null,
    };

    return Card(
      // На исходе оплаченного времени карточка перестаёт быть спокойной: игрока не должно
      // выбрасывать из-за компьютера в тот момент, когда экран выглядел как обычно.
      shape: alarming
          ? RoundedRectangleBorder(
              side: BorderSide(color: theme.colorScheme.error),
              borderRadius: BorderRadius.circular(12),
            )
          : null,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  fixed ? l.customerDashboardSessionRemaining : l.customerDashboardSessionActive,
                  style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                ),
                Text(session.seatName, style: theme.textTheme.bodyMedium),
              ],
            ),
            const SizedBox(height: 6),
            // Читалке отдаётся подпись со смыслом, а само поле цифр скрыто: иначе она
            // проговаривает время каждую секунду и перебивает всё остальное.
            Semantics(
              label: [
                fixed ? l.a11ySessionRemaining : l.a11ySessionElapsed,
                clock,
                ?warning,
              ].join(' '),
              child: ExcludeSemantics(
                child: Text(
                  clock,
                  style: theme.textTheme.headlineMedium?.copyWith(
                    color: alarming ? theme.colorScheme.error : null,
                  ),
                ),
              ),
            ),
            if (warning != null)
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text(
                  warning,
                  style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.error),
                ),
              ),
            // Оплаченная наперёд сессия не набегает по секундам — бегущая стоимость только
            // у открытой.
            if (!fixed && session.accruedCostMinorUnits != null)
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text(
                  '≈ ${formatMoney(session.accruedCostMinorUnits!, session.currencyCode, locale: Localizations.localeOf(context).languageCode)} '
                  '${l.customerDashboardAccrued}',
                  style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.primary),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
