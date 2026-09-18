import 'package:flutter/material.dart';

import '../api/contracts.dart';
import '../api/idempotency.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import '../phone/phone_verification_sheet.dart';
import '../theme/app_theme.dart';
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
    required this.heldBalance,
    required this.debtBalance,
    required this.phoneVerified,
    required this.features,
    this.onPhoneVerified,
    this.onToppedUp,
  });

  final PlayerApiClient api;
  final MoneyDto walletBalance;

  /// Придержанное под брони. Из остатка оно уже вычтено — карточка объясняет, куда делась
  /// часть денег, а не показывает вторую копилку.
  final MoneyDto heldBalance;
  final MoneyDto debtBalance;
  final bool phoneVerified;

  /// null — список возможностей не загрузился. Тогда пополнение считается включённым:
  /// карточка прячет кнопку для удобства, а право на запись всё равно проверяет сервер.
  /// Спрятать её из-за сетевого сбоя значит соврать игроку, что возможности нет.
  final List<String>? features;

  /// Номер подтверждён — экрану выше нужно перечитать себя: подтверждение открывает
  /// пополнение и брони сразу, без повторного входа.
  final VoidCallback? onPhoneVerified;

  /// Заявка на пополнение ушла. Ею открывается счёт в клубе, где его ещё не было, — и об
  /// этом надо сказать наверх, иначе приложение продолжит считать клуб чужим.
  final Future<void> Function()? onToppedUp;

  @override
  State<WalletCard> createState() => _WalletCardState();
}

class _WalletCardState extends State<WalletCard> {
  List<PlayerTopUpIntentDto> _intents = const [];

  @override
  void initState() {
    super.initState();
    _refreshIntents();
  }

  bool get _topUpEnabled => widget.features == null || widget.features!.contains('online_topup');

  /// Заявка, которой игрок ещё ждёт. Именно она отвечает на вопрос «я же пополнял».
  ///
  /// Ждущей считается только незавершённая. Раньше здесь стояло «любая, кроме исполненной», и
  /// отменённая на стойке заявка ещё сутки висела у игрока как ожидающая — до тех пор, пока её
  /// не признавали просроченной по времени создания.
  PlayerTopUpIntentDto? get _awaiting =>
      _intents.where((intent) => intent.state == 'pending' && !intent.isExpired).firstOrNull;

  bool _cancellingIntent = false;
  bool _payingDebt = false;

  /// Ключ попытки погасить долг — один на попытку, чтобы повтор не списал сумму дважды.
  final AttemptKey _debtAttempt = AttemptKey();

  /// Сколько можно закрыть прямо сейчас: весь долг, если денег хватает, иначе весь остаток.
  /// Частичное гашение — не поблажка: долг в две тысячи при тысяче на кошельке иначе нельзя
  /// тронуть вовсе.
  int get _debtPaymentMinorUnits =>
      widget.debtBalance.minorUnits < widget.walletBalance.minorUnits
          ? widget.debtBalance.minorUnits
          : widget.walletBalance.minorUnits;

  bool get _canPayDebt =>
      widget.debtBalance.minorUnits > 0 && _debtPaymentMinorUnits > 0;

  Future<void> _payDebt() async {
    final l = L.of(context);
    setState(() => _payingDebt = true);
    try {
      final amount = _debtPaymentMinorUnits;
      await widget.api.payDebtFromWallet(
        amountMinorUnits: amount,
        currencyCode: widget.debtBalance.currencyCode,
        // Повтор после обрыва обязан нести тот же ключ: иначе с кошелька спишется вторая
        // такая же сумма, а долг закроется только на первую.
        idempotencyKey: _debtAttempt.forSubject('$amount'),
      );
      _debtAttempt.done();
      if (!mounted) return;
      await widget.onToppedUp?.call();
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(l.customerWalletDebtPaid)),
      );
    } on PlayerApiException {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(l.customerWalletDebtPayError)),
      );
    } finally {
      if (mounted) setState(() => _payingDebt = false);
    }
  }

  Future<void> _cancelIntent(PlayerTopUpIntentDto intent) async {
    final l = L.of(context);
    setState(() => _cancellingIntent = true);
    try {
      await widget.api.cancelTopUpIntent(intent.paymentIntentId);
      if (!mounted) return;
      await _refreshIntents();
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(l.customerWalletPendingCancelled)),
      );
    } on PlayerApiException {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(l.customerWalletPendingCancelError)),
      );
    } finally {
      if (mounted) setState(() => _cancellingIntent = false);
    }
  }

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
    await widget.onToppedUp?.call();
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

    final dark = theme.brightness == Brightness.dark;
    final accent = theme.colorScheme.primary;

    // Кошелёк — единственная карточка с собственным светом: баланс должен читаться первым,
    // а не соревноваться с соседними блоками за внимание.
    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppTheme.radiusCard),
        border: Border.all(color: theme.colorScheme.outline),
        // Подложка и свечение замешаны на акценте темы, а не на фирменном зелёном: у клуба
        // может быть свой цвет, и главная карточка приложения — первое место, где его ждут.
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: dark
              ? [
                  Color.alphaBlend(accent.withValues(alpha: 0.18), const Color(0xFF121B19)),
                  const Color(0xFF121B19),
                ]
              : [Color.alphaBlend(accent.withValues(alpha: 0.14), Colors.white), Colors.white],
        ),
        boxShadow: dark ? AppTheme.accentGlow(accent.withValues(alpha: 0.35)) : null,
      ),
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(Icons.account_balance_wallet_outlined,
                    size: 18, color: theme.colorScheme.primary),
                const SizedBox(width: 8),
                Text(
                  l.customerDashboardBalance,
                  style: theme.textTheme.labelLarge
                      ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                ),
              ],
            ),
            const SizedBox(height: 10),
            Text(
              formatMoney(widget.walletBalance.minorUnits, widget.walletBalance.currencyCode,
                  locale: locale),
              style: theme.textTheme.displaySmall,
            ),
            // Придержанное показывается, только когда оно есть: строка «придержано 0» на
            // главной — шум. Когда есть, объясняет, почему остаток меньше ожидаемого.
            if (widget.heldBalance.minorUnits > 0) ...[
              const SizedBox(height: 4),
              Text(
                '${l.customerWalletHeld}: '
                '${formatMoney(widget.heldBalance.minorUnits, widget.heldBalance.currencyCode, locale: locale)}',
                style: theme.textTheme.bodyMedium
                    ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
              Text(
                l.customerWalletHeldNote,
                style:
                    theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
            ],
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
                _canPayDebt ? l.customerWalletDebtPayNote : l.customerDashboardDebtNote,
                style:
                    theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
              // Деньги на кошельке есть, а долг закрыть было нечем: надпись отправляла на стойку
              // клуба, хотя платить можно было отсюда.
              if (_canPayDebt)
                Align(
                  alignment: Alignment.centerLeft,
                  child: OutlinedButton(
                    onPressed: _payingDebt ? null : _payDebt,
                    child: Text(l.customerWalletDebtPay(
                      formatMoney(_debtPaymentMinorUnits, widget.debtBalance.currencyCode,
                          locale: locale),
                    )),
                  ),
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
              // Передумать было нельзя: заявка висела сутки и отвечала «да» на вопрос «я же
              // пополнял», пока её не признавали просроченной.
              Align(
                alignment: Alignment.centerLeft,
                child: TextButton(
                  onPressed: _cancellingIntent ? null : () => _cancelIntent(awaiting),
                  child: Text(l.customerWalletPendingCancel),
                ),
              ),
            ],
            if (_topUpEnabled) ...[
              const SizedBox(height: 16),
              if (widget.phoneVerified)
                Align(
                  alignment: Alignment.centerLeft,
                  child: FilledButton.icon(
                    onPressed: _openTopUp,
                    icon: const Icon(Icons.add, size: 20),
                    label: Text(l.customerWalletTopUp),
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
