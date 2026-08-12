import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';

/// Потолок одной заявки. Нужен не против богатых, а против ввода вроде `1e308`: он
/// превращается в бесконечность, а та уезжает на сервер как `null`.
const double maxTopUpMajor = 1000000;

/// Пополнение кошелька: игрок оставляет заявку, клуб зачисляет.
class WalletPanel extends StatefulWidget {
  const WalletPanel({
    super.key,
    required this.api,
    required this.phoneVerified,
    required this.features,
    required this.currencyCode,
  });

  final PlayerApiClient api;
  final bool phoneVerified;

  /// Валюта кошелька игрока. Берётся из баланса, а не из константы: клуб на другой валюте
  /// получал бы заявку в чужих деньгах.
  final String currencyCode;

  /// null — список возможностей не загрузился. Тогда пополнение считается включённым:
  /// панель прячет кнопку для удобства, а право на запись всё равно проверяет сервер.
  /// Спрятать её из-за сетевого сбоя значит соврать игроку, что возможности нет.
  final List<String>? features;

  @override
  State<WalletPanel> createState() => _WalletPanelState();
}

class _WalletPanelState extends State<WalletPanel> {
  final TextEditingController _amount = TextEditingController();
  List<TopUpIntent> _intents = const [];
  bool _pending = false;

  @override
  void initState() {
    super.initState();
    _refreshIntents();
  }

  @override
  void dispose() {
    _amount.dispose();
    super.dispose();
  }

  bool get _topUpEnabled => widget.features == null || widget.features!.contains('online_topup');

  Future<void> _refreshIntents() async {
    try {
      final intents = await widget.api.getTopUpIntents();
      if (mounted) setState(() => _intents = intents);
    } on PlayerApiException {
      // Список заявок — справка, а не действие: без него панель работает.
    }
  }

  Future<void> _submit() async {
    final l = L.of(context);
    final major = double.tryParse(_amount.text.trim().replaceAll(',', '.'));
    if (major == null || !major.isFinite || major <= 0 || major > maxTopUpMajor) {
      _say(l.customerWalletAmountError);
      return;
    }

    setState(() => _pending = true);
    try {
      await widget.api.createTopUpIntent(
        amountMinorUnits: majorToMinor(major),
        currencyCode: widget.currencyCode,
      );
      if (!mounted) return;
      _amount.clear();
      _say(l.customerWalletSent);
      await _refreshIntents();
    } on PlayerApiException {
      if (mounted) _say(l.customerWalletSendError);
    } finally {
      if (mounted) setState(() => _pending = false);
    }
  }

  void _say(String message) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  String _stateLabel(L l, TopUpIntent intent) {
    if (intent.state == 'fulfilled') return l.customerWalletStateFulfilled;
    if (intent.isExpired) return l.customerWalletStateExpired;
    return l.customerWalletStatePending;
  }

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
            Text(l.customerWalletTitle, style: theme.textTheme.titleMedium),
            if (_topUpEnabled)
              if (widget.phoneVerified)
                Padding(
                  padding: const EdgeInsets.only(top: 12),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                        child: TextField(
                          controller: _amount,
                          keyboardType: const TextInputType.numberWithOptions(decimal: true),
                          decoration: InputDecoration(labelText: l.customerWalletAmount),
                          onSubmitted: (_) => _pending ? null : _submit(),
                        ),
                      ),
                      const SizedBox(width: 8),
                      FilledButton(
                        onPressed: _pending ? null : _submit,
                        child: Text(_pending ? l.customerWalletRequesting : l.customerWalletRequest),
                      ),
                    ],
                  ),
                )
              else
                Padding(
                  padding: const EdgeInsets.only(top: 12),
                  child: Text(
                    l.customerWalletGate,
                    style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                  ),
                ),
            for (final intent in _intents)
              Padding(
                padding: const EdgeInsets.only(top: 8),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(formatMoney(intent.amountMinorUnits, intent.currencyCode, locale: locale)),
                    Text(
                      _stateLabel(l, intent),
                      style: TextStyle(
                        color: intent.state == 'fulfilled'
                            ? theme.colorScheme.primary
                            : theme.colorScheme.onSurfaceVariant,
                      ),
                    ),
                  ],
                ),
              ),
          ],
        ),
      ),
    );
  }
}
