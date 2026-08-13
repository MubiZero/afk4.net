import 'dart:async';

import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import '../theme/app_theme.dart';
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

    final dark = theme.brightness == Brightness.dark;
    // Идущая сессия — единственное, что на экране живёт прямо сейчас, и выглядит она так же:
    // подсвеченная карточка с бегущими цифрами. На исходе оплаченного времени свет меняется
    // на тревожный — игрока не должно выбрасывать из-за компьютера при спокойном экране.
    final signal = alarming ? theme.colorScheme.error : AppTheme.emerald;

    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppTheme.radiusCard),
        border: Border.all(color: alarming ? signal : theme.colorScheme.outline),
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: dark
              ? [signal.withValues(alpha: 0.18), const Color(0xFF121B19)]
              : [signal.withValues(alpha: 0.10), Colors.white],
        ),
        boxShadow: dark ? AppTheme.accentGlow(signal.withValues(alpha: 0.30)) : null,
      ),
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                _Pulse(
                  color: signal,
                  label: fixed
                      ? l.customerDashboardSessionRemaining
                      : l.customerDashboardSessionActive,
                ),
                // Место — опознавательный знак сессии, поэтому набрано как ярлык, а не строкой
                // в общем ряду: игрок ищет его глазами, когда возвращается к экрану.
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                  decoration: BoxDecoration(
                    color: theme.colorScheme.onSurface.withValues(alpha: 0.08),
                    borderRadius: BorderRadius.circular(999),
                  ),
                  child: Text(session.seatName, style: theme.textTheme.labelLarge),
                ),
              ],
            ),
            const SizedBox(height: 10),
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
                  style: theme.textTheme.displaySmall?.copyWith(
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

/// Подпись состояния со светящейся точкой — знак того, что цифры рядом живые.
///
/// Точка НЕ мигает намеренно: бесконечная анимация никогда не даёт дереву успокоиться, и
/// `pumpAndSettle` виснет в каждом тесте, где на экране есть сессия. Секунды на часах и так
/// тикают — движения на экране достаточно.
class _Pulse extends StatelessWidget {
  const _Pulse({required this.color, required this.label});

  final Color color;
  final String label;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 8,
          height: 8,
          decoration: BoxDecoration(
            color: color,
            shape: BoxShape.circle,
            boxShadow: [BoxShadow(color: color, blurRadius: 10)],
          ),
        ),
        const SizedBox(width: 8),
        Text(
          label,
          style: theme.textTheme.labelLarge?.copyWith(color: theme.colorScheme.onSurfaceVariant),
        ),
      ],
    );
  }
}
