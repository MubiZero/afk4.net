import 'package:flutter/material.dart';

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

/// Пополнение кошелька: игрок оставляет заявку, клуб зачисляет.
///
/// Живёт в листе снизу, а не на главной: пополняют раз в неделю, а смотрят на баланс и
/// сессию каждый визит — поле ввода не должно занимать главный экран.
class TopUpSheet extends StatefulWidget {
  const TopUpSheet({
    super.key,
    required this.api,
    required this.currencyCode,
    required this.intents,
    this.branch = const BranchChoice(),
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

  @override
  State<TopUpSheet> createState() => _TopUpSheetState();
}

class _TopUpSheetState extends State<TopUpSheet> {
  final TextEditingController _amount = TextEditingController();
  bool _pending = false;

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
  }

  @override
  void dispose() {
    _amount.dispose();
    super.dispose();
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
        branchId: _branchId,
      );
      if (!mounted) return;
      // Лист закрывается сам: заявка отправлена, делать здесь больше нечего, а сводку с
      // ожидающей заявкой игрок увидит на главной.
      Navigator.of(context).pop(true);
    } on PlayerApiException catch (error) {
      if (mounted) {
        setState(() => _pending = false);
        // Клуб закрыл счёт — это решение клуба, а не сбой отправки: «попробуйте ещё раз» звало бы
        // человека повторять то, что не выйдет ни с какого раза.
        _say(error.message == 'club_account_closed'
            ? l.customerClubErrClosed
            : l.customerWalletSendError);
      }
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
            FilledButton(
              // Пока зал не назван, зачислять некуда: сервер ответит отказом, из-за которого
              // этот вопрос и появился.
              onPressed: _pending || _choice.unanswered ? null : _submit,
              child: Text(_pending ? l.customerWalletRequesting : l.customerWalletRequest),
            ),
            const SizedBox(height: 8),
            // Что будет дальше. Без этого «заявка отправлена» оставляет игрока ждать
            // неизвестно чего.
            Text(
              l.customerWalletNote,
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
