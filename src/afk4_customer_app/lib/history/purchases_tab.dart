import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../format/date_time.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import 'cursor_list.dart';
import 'cursor_list_view.dart';

/// Покупки в баре: когда, что и на сколько.
class PurchasesTab extends StatefulWidget {
  const PurchasesTab({super.key, required this.api});

  final PlayerApiClient api;

  @override
  State<PurchasesTab> createState() => _PurchasesTabState();
}

class _PurchasesTabState extends State<PurchasesTab> {
  late final CursorListController<PlayerPurchase> _list =
      CursorListController((cursor) => widget.api.getPurchases(cursor: cursor));

  @override
  void dispose() {
    _list.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    return CursorListView<PlayerPurchase>(
      controller: _list,
      loadingLabel: l.a11yLoadingPurchases,
      errorText: l.customerHistoryPurchasesError,
      emptyText: l.customerHistoryNoPurchases,
      itemBuilder: (context, purchase) => _PurchaseCard(purchase: purchase),
    );
  }
}

class _PurchaseCard extends StatelessWidget {
  const _PurchaseCard({required this.purchase});

  final PlayerPurchase purchase;

  @override
  Widget build(BuildContext context) {
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
                Text(
                  formatDateTime(purchase.createdAtUtc, locale),
                  style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                ),
                Text(
                  formatMoney(purchase.totalMinorUnits, purchase.currencyCode, locale: locale),
                  style: theme.textTheme.titleMedium,
                ),
              ],
            ),
            const SizedBox(height: 8),
            for (final line in purchase.lines)
              Text('${line.productName} × ${line.quantity}', style: theme.textTheme.bodyMedium),
          ],
        ),
      ),
    );
  }
}
