import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import '../phone/phone_verification_sheet.dart';
import 'top_up_sheet.dart';

/// Деньги игрока одним блоком: сколько на кошельке, есть ли долг и что с пополнением.
///
/// Раньше баланс и пополнение были двумя карточками, и форма ввода суммы — самый тяжёлый
/// элемент экрана — стояла выше живой сессии. Пополняют редко, смотрят баланс каждый визит.
class WalletCard extends StatefulWidget {
  const WalletCard({
    super.key,
    required this.api,
    required this.walletBalance,
    required this.debtBalance,
    required this.phoneVerified,
    required this.features,
    this.onPhoneVerified,
  });

  final PlayerApiClient api;
  final Money walletBalance;
  final Money debtBalance;
  final bool phoneVerified;

  /// null — список возможностей не загрузился. Тогда пополнение считается включённым:
  /// карточка прячет кнопку для удобства, а право на запись всё равно проверяет сервер.
  /// Спрятать её из-за сетевого сбоя значит соврать игроку, что возможности нет.
  final List<String>? features;

  /// Номер подтверждён — экрану выше нужно перечитать себя: подтверждение открывает
  /// пополнение и брони сразу, без повторного входа.
  final VoidCallback? onPhoneVerified;

  @override
  State<WalletCard> createState() => _WalletCardState();
}

class _WalletCardState extends State<WalletCard> {
  List<TopUpIntent> _intents = const [];

  @override
  void initState() {
    super.initState();
    _refreshIntents();
  }

  bool get _topUpEnabled => widget.features == null || widget.features!.contains('online_topup');

  /// Заявка, которой игрок ещё ждёт. Именно она отвечает на вопрос «я же пополнял».
  TopUpIntent? get _awaiting => _intents
      .where((intent) => intent.state != 'fulfilled' && !intent.isExpired)
      .firstOrNull;

  Future<void> _refreshIntents() async {
    try {
      final intents = await widget.api.getTopUpIntents();
      if (mounted) setState(() => _intents = intents);
    } on PlayerApiException {
      // Список заявок — справка, а не действие: без него карточка работает.
    }
  }

  Future<void> _openTopUp() async {
    final l = L.of(context);
    final sent = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (_) => TopUpSheet(
        api: widget.api,
        currencyCode: widget.walletBalance.currencyCode,
        intents: _intents,
      ),
    );
    if (sent != true || !mounted) return;

    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(l.customerWalletSent)));
    await _refreshIntents();
  }

  Future<void> _verifyPhone() async {
    final l = L.of(context);
    final confirmed = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (_) => PhoneVerificationSheet(api: widget.api),
    );
    if (confirmed != true || !mounted) return;

    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(l.customerPhoneDone)));
    widget.onPhoneVerified?.call();
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;
    final awaiting = _awaiting;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              l.customerDashboardBalance,
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 4),
            Text(
              formatMoney(widget.walletBalance.minorUnits, widget.walletBalance.currencyCode,
                  locale: locale),
              style: theme.textTheme.headlineMedium,
            ),
            // Долг показывается, только когда он есть: строка «Долг: 0» на главной пугает зря.
            // Гасится он на кассе, поэтому карточка отправляет к стойке, а не обещает, что
            // пополнение кошелька его закроет — оно не закрывает.
            if (widget.debtBalance.minorUnits > 0) ...[
              const SizedBox(height: 4),
              Text(
                '${l.customerDashboardDebt}: '
                '${formatMoney(widget.debtBalance.minorUnits, widget.debtBalance.currencyCode, locale: locale)}',
                style: TextStyle(color: theme.colorScheme.error),
              ),
              Text(
                l.customerDashboardDebtNote,
                style:
                    theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
            ],
            if (awaiting != null) ...[
              const SizedBox(height: 8),
              Text(
                l.customerWalletPendingHint(
                  formatMoney(awaiting.amountMinorUnits, awaiting.currencyCode, locale: locale),
                ),
                style:
                    theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
            ],
            if (_topUpEnabled) ...[
              const SizedBox(height: 12),
              if (widget.phoneVerified)
                Align(
                  alignment: Alignment.centerLeft,
                  child: FilledButton(
                    onPressed: _openTopUp,
                    child: Text(l.customerWalletTopUp),
                  ),
                )
              else ...[
                // Гейт перестал быть тупиком: раньше он отправлял к администратору клуба,
                // у которого возможности подтвердить номер тоже не было.
                Text(
                  l.customerWalletGate,
                  style: theme.textTheme.bodyMedium
                      ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                ),
                const SizedBox(height: 8),
                Align(
                  alignment: Alignment.centerLeft,
                  child: OutlinedButton(
                    onPressed: _verifyPhone,
                    child: Text(l.customerWalletGateAction),
                  ),
                ),
              ],
            ],
          ],
        ),
      ),
    );
  }
}
