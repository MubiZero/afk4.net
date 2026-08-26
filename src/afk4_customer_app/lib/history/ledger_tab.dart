import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../format/date_time.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import 'cursor_list.dart';
import 'cursor_list_view.dart';

/// Движения по кошельку: откуда деньги пришли и куда ушли.
///
/// Соседние ленты построены не на деньгах — визиты берутся из сессий, покупки из чеков, — и
/// пополнение, кешбэк, бонус за друга, ручная правка оператора и погашение долга не видны там
/// нигде. Человек видел, за что списали, и не видел, откуда пришло: кошелёк у него не сходился.
class LedgerTab extends StatefulWidget {
  const LedgerTab({super.key, required this.api});

  final PlayerApiClient api;

  @override
  State<LedgerTab> createState() => _LedgerTabState();
}

class _LedgerTabState extends State<LedgerTab> {
  late final CursorListController<PlayerLedgerEntry> _list =
      CursorListController((cursor) => widget.api.getWalletLedger(cursor: cursor));

  @override
  void dispose() {
    _list.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    return CursorListView<PlayerLedgerEntry>(
      controller: _list,
      loadingLabel: l.a11yLoadingLedger,
      errorText: l.customerWalletLedgerError,
      emptyText: l.customerWalletLedgerEmpty,
      itemBuilder: (context, entry) => _LedgerRow(entry: entry),
    );
  }
}

/// Название типа движения словами. Каталог общий со стойкой: два перевода одного и того же
/// однажды разъехались бы, и клуб с игроком читали бы про одно событие разное.
String ledgerTypeLabel(String entryType, L l) => switch (entryType) {
      'top_up' => l.ledgerTypeTopUp,
      'gameplay_charge' => l.ledgerTypeGameplayCharge,
      'package_purchase' => l.ledgerTypePackagePurchase,
      'package_consumption' => l.ledgerTypePackageConsumption,
      'bonus_grant' => l.ledgerTypeBonusGrant,
      'bonus_consumption' => l.ledgerTypeBonusConsumption,
      'refund' => l.ledgerTypeRefund,
      'manual_correction' => l.ledgerTypeManualCorrection,
      'postpaid_debt' => l.ledgerTypePostpaidDebt,
      'debt_payment' => l.ledgerTypeDebtPayment,
      'wallet_payment' => l.ledgerTypeWalletPayment,
      'reversal' => l.ledgerTypeReversal,
      'cashback' => l.ledgerTypeCashback,
      'referral_bonus' => l.ledgerTypeReferralBonus,
      'reservation_no_show_fee' => l.ledgerTypeReservationNoShowFee,
      'tournament_entry_fee' => l.ledgerTypeTournamentEntryFee,
      'tournament_entry_refund' => l.ledgerTypeTournamentEntryRefund,
      // Незнакомый тип показывается как есть: сырой код честнее выдуманного названия, а появиться
      // он может только у клиента старше сервера.
      _ => entryType,
    };

class _LedgerRow extends StatelessWidget {
  const _LedgerRow({required this.entry});

  final PlayerLedgerEntry entry;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;
    final income = entry.amountMinorUnits > 0;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(ledgerTypeLabel(entry.entryType, l), style: theme.textTheme.bodyLarge),
                  Text(
                    formatDateTime(l, entry.createdAtUtc, locale),
                    style: theme.textTheme.bodyMedium
                        ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 12),
            // Знак перед суммой — главное в строке: человек листает выписку, чтобы понять, где
            // прибыло, а где убыло, и цвет тут помогает, но решает именно знак.
            Text(
              '${income ? '+' : '−'}${formatMoney(entry.amountMinorUnits.abs(), entry.currencyCode, locale: locale)}',
              style: theme.textTheme.titleMedium?.copyWith(
                color: income ? theme.colorScheme.primary : null,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
