import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../format/date_time.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import 'cursor_list.dart';
import 'cursor_list_view.dart';
import 'receipt_screen.dart';

/// Визиты игрока — где сидел, сколько пробыл, сколько заплатил.
class VisitsTab extends StatefulWidget {
  const VisitsTab({super.key, required this.api, this.clock = DateTime.now});

  final PlayerApiClient api;
  final DateTime Function() clock;

  @override
  State<VisitsTab> createState() => _VisitsTabState();
}

class _VisitsTabState extends State<VisitsTab> {
  late final CursorListController<PlayerVisit> _list =
      CursorListController((cursor) => widget.api.getVisits(cursor: cursor));

  @override
  void dispose() {
    _list.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    return CursorListView<PlayerVisit>(
      controller: _list,
      loadingLabel: l.a11yLoadingVisits,
      errorText: l.customerHistoryLoadError,
      emptyText: l.customerHistoryNoVisits,
      itemBuilder: (context, visit) => _VisitCard(
        visit: visit,
        now: widget.clock(),
        onOpenReceipt: () => _openReceipt(visit.sessionId),
      ),
    );
  }

  void _openReceipt(String sessionId) {
    Navigator.of(context).push(MaterialPageRoute<void>(
      builder: (_) => ReceiptScreen(api: widget.api, sessionId: sessionId, clock: widget.clock),
    ));
  }
}

class _VisitCard extends StatelessWidget {
  const _VisitCard({required this.visit, required this.now, required this.onOpenReceipt});

  final PlayerVisit visit;
  final DateTime now;
  final VoidCallback onOpenReceipt;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(visit.seatName, style: theme.textTheme.titleMedium),
                Text(
                  formatMoney(visit.grandTotalMinorUnits, visit.currencyCode, locale: locale),
                  style: theme.textTheme.titleMedium,
                ),
              ],
            ),
            const SizedBox(height: 4),
            Text(
              '${formatDateTime(visit.startedAtUtc, locale)} · '
              '${formatVisitDuration(l, visit.startedAtUtc, visit.endedAtUtc, now: now)}',
              style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            // Кнопка появляется только там, где чек действительно есть: иначе игрок жмёт
            // и упирается в «чек не найден».
            if (visit.hasReceipt)
              Align(
                alignment: Alignment.centerLeft,
                child: TextButton(onPressed: onOpenReceipt, child: Text(l.customerReceiptOpenLink)),
              ),
          ],
        ),
      ),
    );
  }
}
