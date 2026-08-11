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

    final clock = fixed
        ? formatClock(projectRemainingSeconds(session.remainingSeconds ?? 0, widget.fetchedAt, now: now))
        : formatClock(elapsedSeconds(session.startedAtUtc, now: now));

    return Card(
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
              label: '${fixed ? l.a11ySessionRemaining : l.a11ySessionElapsed} $clock',
              child: ExcludeSemantics(
                child: Text(clock, style: theme.textTheme.headlineMedium),
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
