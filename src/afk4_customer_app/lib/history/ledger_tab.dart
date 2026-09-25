import 'package:flutter/material.dart';

import '../api/contracts.dart';
import '../api/player_api_client.dart';
import '../format/date_time.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import '../theme/app_palette.dart';
import '../theme/app_theme.dart';
import 'cursor_list.dart';
import 'cursor_list_view.dart';
import 'receipt_screen.dart';

/// Движения по кошельку: откуда деньги пришли и куда ушли.
///
/// Соседние ленты построены не на деньгах — визиты берутся из сессий, покупки из чеков, — и
/// пополнение, кешбэк, бонус за друга, ручная правка оператора и погашение долга не видны там
/// нигде. Человек видел, за что списали, и не видел, откуда пришло: кошелёк у него не сходился.
class LedgerTab extends StatefulWidget {
  const LedgerTab({super.key, required this.api, this.clock = DateTime.now});

  final PlayerApiClient api;
  final DateTime Function() clock;

  @override
  State<LedgerTab> createState() => _LedgerTabState();
}

class _LedgerTabState extends State<LedgerTab> {
  late final CursorListController<PlayerLedgerEntryDto> _list =
      CursorListController(
        (cursor) => widget.api.getWalletLedger(cursor: cursor),
      );

  @override
  void dispose() {
    _list.dispose();
    super.dispose();
  }

  void _openReceipt(String sessionId) {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => ReceiptScreen(
          api: widget.api,
          sessionId: sessionId,
          clock: widget.clock,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    return CursorListView<PlayerLedgerEntryDto>(
      controller: _list,
      loadingLabel: l.a11yLoadingLedger,
      errorText: l.customerWalletLedgerError,
      emptyText: l.customerWalletLedgerEmpty,
      itemBuilder: (context, entry) => _LedgerRow(
        entry: entry,
        // Строка про визит ведёт в его чек: сумма без состава — это половина ответа на
        // вопрос «за что», а сам чек рядом и давно работает.
        onOpenReceipt: entry.receiptSessionId == null
            ? null
            : () => _openReceipt(entry.receiptSessionId!),
      ),
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
  'reservation_hold' => l.ledgerTypeReservationHold,
  'reservation_no_show_fee' => l.ledgerTypeReservationNoShowFee,
  'tournament_entry_fee' => l.ledgerTypeTournamentEntryFee,
  'tournament_entry_refund' => l.ledgerTypeTournamentEntryRefund,
  'tip' => l.ledgerTypeTip,
  // Незнакомый тип показывается как есть: сырой код честнее выдуманного названия, а появиться
  // он может только у клиента старше сервера.
  _ => entryType,
};

/// Название строки выписки. У снятия удержания вместо «Отмена операции» — почему вернулись
/// деньги: иначе человек видел бы «+15» и не знал, что именно отменили.
String ledgerEntryLabel(PlayerLedgerEntryDto entry, L l) =>
    switch (entry.holdReleaseCause) {
      'seated' => l.ledgerHoldReleaseSeated,
      'cancelled' => l.ledgerHoldReleaseCancelled,
      'rejected' => l.ledgerHoldReleaseRejected,
      'request_expired' => l.ledgerHoldReleaseRequestExpired,
      'no_show' => l.ledgerHoldReleaseNoShow,
      'moved' => l.ledgerHoldReleaseMoved,
      _ => ledgerTypeLabel(entry.entryType, l),
    };

class _LedgerRow extends StatelessWidget {
  const _LedgerRow({required this.entry, this.onOpenReceipt});

  final PlayerLedgerEntryDto entry;

  /// Открыть чек визита, которым объясняется эта сумма. null — объяснять нечем.
  final VoidCallback? onOpenReceipt;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;
    final income = entry.amount.minorUnits > 0;

    return Card(
      child: InkWell(
        onTap: onOpenReceipt,
        borderRadius: BorderRadius.circular(AppTheme.radiusCard),
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
                    Text(
                      ledgerEntryLabel(entry, l),
                      style: theme.textTheme.bodyLarge,
                    ),
                    Text(
                      // Сколько времени за этим движением, если оно про время. Сервер присылал
                      // это всегда, а строка молчала: две «Списание за игру» за один вечер
                      // ничем не различались, и сойтись с кошельком было нечем.
                      entry.quantitySeconds >= 60
                          ? '${formatDateTime(l, entry.createdAtUtc, locale)} · ${formatDurationMinutes(l, entry.quantitySeconds ~/ 60)}'
                          : formatDateTime(l, entry.createdAtUtc, locale),
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: theme.colorScheme.onSurfaceVariant,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 12),
              // Знак перед суммой — главное в строке: человек листает выписку, чтобы понять, где
              // прибыло, а где убыло, и цвет тут помогает, но решает именно знак.
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Text(
                    '${income ? '+' : '−'}${formatMoney(entry.amount.minorUnits.abs(), entry.amount.currencyCode, locale: locale)}',
                    style: theme.textTheme.titleMedium?.copyWith(
                      color: income ? AppPalette.of(context).income : null,
                    ),
                  ),
                  // Остаток после строки — чтобы выписка сходилась с балансом наверху без
                  // подсчёта в уме. У строк про время остатка нет: кошелёк они не двигают.
                  if (entry.walletBalanceAfter case final balance?)
                    Text(
                      l.ledgerBalanceAfter(
                        formatMoney(balance.minorUnits, balance.currencyCode, locale: locale),
                      ),
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: theme.colorScheme.onSurfaceVariant,
                      ),
                    ),
                ],
              ),
              if (onOpenReceipt != null) ...[
                const SizedBox(width: 4),
                Icon(
                  Icons.chevron_right,
                  size: 20,
                  color: theme.colorScheme.onSurfaceVariant,
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
