import 'dart:async';

import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import '../organization/branch_choice.dart';

/// Потолок одной заявки. Нужен не против богатых, а против ввода вроде `1e308`: он
/// превращается в бесконечность, а та уезжает на сервер как `null`.
const double maxTopUpMajor = 1000000;

/// Суммы, которые набирают чаще всего. Набирать их пальцем на телефоне — лишнее трение
/// там, где хватает одного касания.
const List<int> quickTopUpMajor = [50, 100, 200];

/// Пополнение кошелька: онлайн-оплатой из приложения банка или заявкой на стойку.
///
/// Живёт в листе снизу, а не на главной: пополняют раз в неделю, а смотрят на баланс и
/// сессию каждый визит — поле ввода не должно занимать главный экран.
///
/// Онлайн-оплата предлагается, только если клуб её принимает: способы спрашиваются у сервера
/// при открытии листа. Кнопка, которая откажет, хуже отсутствующей кнопки.
class TopUpSheet extends StatefulWidget {
  const TopUpSheet({
    super.key,
    required this.api,
    required this.currencyCode,
    required this.intents,
    this.branch = const BranchChoice(),
    this.openLink,
  });

  final PlayerApiClient api;

  /// Валюта кошелька игрока. Берётся из баланса, а не из константы: клуб на другой валюте
  /// получал бы заявку в чужих деньгах.
  final String currencyCode;

  /// Уже оставленные заявки — чтобы игрок не отправлял вторую, забыв про первую.
  final List<TopUpIntent> intents;

  /// Зал, в котором открыть счёт. Нужен первому пополнению в сети из нескольких залов: до
  /// него счёта в клубе нет, и деньги некуда зачислять.
  final BranchChoice branch;

  /// Чем открывать ссылку в приложение банка. В бою — `url_launcher`; тесты подставляют свой,
  /// иначе каждый тест упирался бы в платформенный канал, которого на тестовой машине нет.
  final Future<bool> Function(Uri)? openLink;

  @override
  State<TopUpSheet> createState() => _TopUpSheetState();
}

class _TopUpSheetState extends State<TopUpSheet> with WidgetsBindingObserver {
  /// Сколько ждём ответа банка, прежде чем отпустить человека. Дольше пяти минут держать
  /// экран незачем: деньги, ушедшие позже, всё равно зачислит вебхук банка.
  static const Duration _bankWait = Duration(minutes: 5);
  static const Duration _pollEvery = Duration(seconds: 3);

  final TextEditingController _amount = TextEditingController();
  bool _pending = false;

  /// Чем клуб принимает деньги. Null — ещё не спросили; до ответа онлайн не предлагаем.
  TopUpMethods? _methods;

  /// Заявка, за которой сейчас следим, и время, до которого ждём банк.
  TopUpIntent? _awaiting;
  DateTime? _deadline;
  Timer? _poll;

  /// Зал, который назвал игрок. Ответ виден в той же форме, где его дали, и уходит наверх —
  /// чтобы бронь не спросила то же самое второй раз.
  String? _branchId;

  BranchChoice get _choice => BranchChoice(
        halls: widget.branch.halls,
        chosenId: _branchId,
        onChosen: (branchId) {
          setState(() => _branchId = branchId);
          widget.branch.onChosen?.call(branchId);
        },
      );

  @override
  void initState() {
    super.initState();
    _branchId = widget.branch.branchId;
    WidgetsBinding.instance.addObserver(this);
    _loadMethods();
  }

  @override
  void dispose() {
    _poll?.cancel();
    WidgetsBinding.instance.removeObserver(this);
    _amount.dispose();
    super.dispose();
  }

  /// Человек вернулся из приложения банка — спрашиваем сразу, не дожидаясь своей секунды.
  /// Именно в этот момент он и смотрит на экран.
  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed && _awaiting != null) {
      unawaited(_checkPayment());
    }
  }

  Future<void> _loadMethods() async {
    try {
      final methods = await widget.api.topUpMethods();
      if (mounted) setState(() => _methods = methods);
    } on PlayerApiException {
      // Не узнали — предлагаем стойку: она работает всегда, и это честнее, чем показать
      // онлайн-оплату, о которой мы ничего не знаем.
      if (mounted) setState(() => _methods = const TopUpMethods(counter: true, online: false));
    }
  }

  Future<void> _submit({bool online = false}) async {
    final l = L.of(context);
    final major = double.tryParse(_amount.text.trim().replaceAll(',', '.'));
    if (major == null || !major.isFinite || major <= 0 || major > maxTopUpMajor) {
      _say(l.customerWalletAmountError);
      return;
    }

    setState(() => _pending = true);
    try {
      final intent = await widget.api.createTopUpIntent(
        amountMinorUnits: majorToMinor(major),
        currencyCode: widget.currencyCode,
        branchId: _branchId,
        method: online ? 'eskhata' : 'counter',
      );
      if (!mounted) return;
      if (online) {
        await _payInBank(intent);
        return;
      }
      // Лист закрывается сам: заявка отправлена, делать здесь больше нечего, а сводку с
      // ожидающей заявкой игрок увидит на главной.
      Navigator.of(context).pop(true);
    } on PlayerApiException catch (error) {
      if (mounted) {
        setState(() => _pending = false);
        // Клуб закрыл счёт — это решение клуба, а не сбой отправки: «попробуйте ещё раз» звало бы
        // человека повторять то, что не выйдет ни с какого раза.
        _say(switch (error.message) {
          'club_account_closed' => l.customerClubErrClosed,
          'network_banned' => l.customerBanTitle,
          // Клуб перестал принимать онлайн, пока лист был открыт: стойка при этом работает.
          'online_payment_unavailable' => l.customerWalletOnlineUnavailable,
          _ => l.customerWalletSendError,
        });
      }
    }
  }

  /// Уводит в приложение банка и остаётся ждать ответа. Ссылка в приложение не открылась —
  /// открываем страницу оплаты в браузере: приложения банка на телефоне может не быть, и
  /// упереться в тишину человек не должен.
  Future<void> _payInBank(TopUpIntent intent) async {
    final open = widget.openLink ?? (uri) => launchUrl(uri, mode: LaunchMode.externalApplication);
    var opened = false;
    if (intent.deepLink != null) {
      opened = await open(Uri.parse(intent.deepLink!));
    }
    if (!opened && intent.payUrl != null) {
      opened = await open(Uri.parse(intent.payUrl!));
    }

    if (!mounted) return;
    if (!opened) {
      setState(() => _pending = false);
      _say(L.of(context).customerWalletOnlineNoApp);
      return;
    }

    setState(() {
      _awaiting = intent;
      _deadline = DateTime.now().add(_bankWait);
    });
    _poll = Timer.periodic(_pollEvery, (_) => unawaited(_checkPayment()));
  }

  /// Спрашивает банк об одной заявке. «Оплачено» приходит только после того, как деньги
  /// зачислены в кошелёк, — поэтому по нему можно закрывать лист.
  Future<void> _checkPayment() async {
    final intent = _awaiting;
    if (intent == null) return;

    String payment;
    try {
      payment = await widget.api.eskhataPaymentStatus(intent.paymentIntentId);
    } on PlayerApiException {
      payment = 'pending';
    }
    if (!mounted) return;

    final l = L.of(context);
    if (payment == 'paid') {
      _stopWaiting();
      Navigator.of(context).pop(true);
      return;
    }
    if (payment == 'failed') {
      _stopWaiting();
      setState(() => _pending = false);
      _say(l.customerWalletOnlineFailed);
      return;
    }
    if (_deadline != null && DateTime.now().isAfter(_deadline!)) {
      // Ждать дольше незачем: ушедшие деньги зачислит вебхук банка, и человеку об этом
      // говорят прямо, а не оставляют гадать.
      _stopWaiting();
      setState(() => _pending = false);
      _say(l.customerWalletOnlineSlow);
    }
  }

  void _stopWaiting() {
    _poll?.cancel();
    _poll = null;
    _awaiting = null;
    _deadline = null;
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

    return Padding(
      // Клавиатура не должна закрывать поле ввода и кнопку.
      padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(l.customerWalletTitle, style: theme.textTheme.titleLarge),
            // Зал идёт до суммы: он решает, где заведётся кошелёк, а сумма — сколько на нём
            // будет. Вопрос о деньгах вперёд вопроса о месте читался бы как мелочь под ним.
            if (_choice.asks) ...[
              const SizedBox(height: 16),
              BranchPicker(choice: _choice),
            ],
            const SizedBox(height: 16),
            TextField(
              controller: _amount,
              autofocus: true,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              decoration: InputDecoration(labelText: l.customerWalletAmount),
              onSubmitted: (_) => _pending ? null : _submit(),
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              children: [
                for (final amount in quickTopUpMajor)
                  ActionChip(
                    label: Text(formatMoney(amount * 100, widget.currencyCode, locale: locale)),
                    onPressed: _pending ? null : () => _amount.text = '$amount',
                  ),
              ],
            ),
            const SizedBox(height: 16),
            if (_awaiting != null) ...[
              // Человек в приложении банка или только что из него вернулся. Экран говорит,
              // чего ждёт, и не даёт нажать оплату второй раз.
              Row(
                children: [
                  const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  ),
                  const SizedBox(width: 12),
                  Expanded(child: Text(l.customerWalletOnlineWaiting)),
                ],
              ),
            ] else ...[
              // Онлайн-оплата стоит первой, когда клуб её принимает: это деньги, которые
              // доходят сами, без очереди к стойке.
              if (_methods?.online ?? false) ...[
                FilledButton(
                  onPressed: _pending || _choice.unanswered ? null : () => _submit(online: true),
                  child: Text(_pending ? l.customerWalletRequesting : l.customerWalletOnlinePay),
                ),
                const SizedBox(height: 8),
                OutlinedButton(
                  // Пока зал не назван, зачислять некуда: сервер ответит отказом, из-за
                  // которого этот вопрос и появился.
                  onPressed: _pending || _choice.unanswered ? null : () => _submit(),
                  child: Text(l.customerWalletRequest),
                ),
              ] else
                FilledButton(
                  onPressed: _pending || _choice.unanswered ? null : () => _submit(),
                  child: Text(_pending ? l.customerWalletRequesting : l.customerWalletRequest),
                ),
            ],
            const SizedBox(height: 8),
            // Что будет дальше. Без этого «заявка отправлена» оставляет игрока ждать
            // неизвестно чего.
            Text(
              (_methods?.online ?? false) ? l.customerWalletOnlineNote : l.customerWalletNote,
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            if (widget.intents.isNotEmpty) ...[
              const Divider(height: 32),
              Text(l.customerWalletIntents, style: theme.textTheme.titleMedium),
              for (final intent in widget.intents)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: _IntentRow(intent: intent, stateLabel: _stateLabel(l, intent)),
                ),
            ],
          ],
        ),
      ),
    );
  }
}

class _IntentRow extends StatelessWidget {
  const _IntentRow({required this.intent, required this.stateLabel});

  final TopUpIntent intent;
  final String stateLabel;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;

    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(formatMoney(intent.amountMinorUnits, intent.currencyCode, locale: locale)),
        Text(
          stateLabel,
          style: TextStyle(
            color: intent.state == 'fulfilled'
                ? theme.colorScheme.primary
                : theme.colorScheme.onSurfaceVariant,
          ),
        ),
      ],
    );
  }
}
