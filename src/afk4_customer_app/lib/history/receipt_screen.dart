import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../format/date_time.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';

/// Чек одного визита: время, покупки и итог.
class ReceiptScreen extends StatefulWidget {
  const ReceiptScreen({
    super.key,
    required this.api,
    required this.sessionId,
    this.clock = DateTime.now,
  });

  final PlayerApiClient api;
  final String sessionId;
  final DateTime Function() clock;

  @override
  State<ReceiptScreen> createState() => _ReceiptScreenState();
}

enum _Load { loading, missing, failed, ready }

class _ReceiptScreenState extends State<ReceiptScreen> {
  _Load _state = _Load.loading;
  VisitReceipt? _receipt;

  @override
  void initState() {
    super.initState();
    _fetch();
  }

  Future<void> _fetch() async {
    try {
      final receipt = await widget.api.getVisitReceipt(widget.sessionId);
      if (!mounted) return;
      setState(() {
        _receipt = receipt;
        _state = _Load.ready;
      });
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      // «Чека нет» и «не смогли загрузить» — разные новости: первое означает, что смотреть
      // нечего, второе — что стоит повторить позже.
      setState(() => _state = error.statusCode == 404 ? _Load.missing : _Load.failed);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: Text(l.customerReceiptTotal)),
      body: switch (_state) {
        _Load.loading => Semantics(
            label: l.a11yLoadingReceipt,
            child: const Center(child: CircularProgressIndicator()),
          ),
        _Load.missing => Center(
            child: Text(
              l.customerReceiptNotFound,
              style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
          ),
        _Load.failed => Center(
            child: Text(l.customerReceiptLoadError, style: TextStyle(color: theme.colorScheme.error)),
          ),
        _Load.ready => _ReceiptBody(receipt: _receipt!, now: widget.clock()),
      },
    );
  }
}

class _ReceiptBody extends StatelessWidget {
  const _ReceiptBody({required this.receipt, required this.now});

  final VisitReceipt receipt;
  final DateTime now;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;
    String money(int minorUnits) => formatMoney(minorUnits, receipt.currencyCode, locale: locale);

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(receipt.receiptNumber, style: theme.textTheme.titleMedium),
                    Text(
                      formatDateTime(receipt.createdAtUtc, locale),
                      style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                    ),
                  ],
                ),
                const SizedBox(height: 4),
                Text(
                  '${receipt.seatName} · '
                  '${formatVisitDuration(l, receipt.startedAtUtc, receipt.endedAtUtc, now: now)}',
                  style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                ),
                const Divider(height: 24),
                _Row(label: l.customerReceiptTime, value: money(receipt.timeChargeMinorUnits)),
                for (final line in receipt.lines)
                  _Row(
                    label: '${line.productName} × ${line.quantity}',
                    value: money(line.lineTotalMinorUnits),
                  ),
                const Divider(height: 24),
                _Row(
                  label: l.customerReceiptTotal,
                  value: money(receipt.grandTotalMinorUnits),
                  emphasized: true,
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _Row extends StatelessWidget {
  const _Row({required this.label, required this.value, this.emphasized = false});

  final String label;
  final String value;
  final bool emphasized;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final style = emphasized ? theme.textTheme.titleMedium : theme.textTheme.bodyMedium;

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Expanded(
            child: Text(
              label,
              style: emphasized
                  ? style
                  : style?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
          ),
          Text(value, style: style),
        ],
      ),
    );
  }
}
