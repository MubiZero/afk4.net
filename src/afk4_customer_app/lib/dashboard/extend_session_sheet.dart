import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../api/contracts.dart';
import '../api/idempotency.dart';
import '../api/player_api_client.dart';
import '../format/date_time.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import '../shell/actions.dart';
import '../shell/app_sheet.dart';
import '../shell/load_failure.dart';
import '../shell/skeleton.dart';
import '../theme/space.dart';

/// Предвыбранный вариант. Пустой выбор заставил бы игрока принимать решение с нуля, а час —
/// то, что берут чаще всего; переключить его — одно касание.
const int defaultExtendMinutes = 60;

/// Продолжительность словами: до часа — в минутах, дальше — в часах.
///
/// Оба сообщения плюральные целиком, а не «число + слово рядом»: в русском «1 час»,
/// «2 часа», «5 часов» — три разные формы, и склейка их не даёт.
String extendDurationLabel(L l, int minutes) =>
    minutes < 60 ? l.customerSessionExtendMinutes(minutes) : l.customerSessionExtendHours(minutes ~/ 60);

/// Лист продления идущей сессии.
///
/// Варианты и суммы — с сервера (`extend-offers`), как на экране ПК: раньше лист умножал цену
/// часа на минуты и честно писал «примерно», а минимум, шаг округления и окна тарифа знает только
/// сервер. Возвращает выбранные минуты, когда продление прошло, и null, когда игрок закрыл лист.
/// Сам он ничего не рассказывает об успехе: сообщение показывает главный экран, который после
/// этого перечитывает баланс и остаток.
class ExtendSessionSheet extends StatefulWidget {
  const ExtendSessionSheet({super.key, required this.api, required this.sessionId, this.onTopUp});

  final PlayerApiClient api;
  final String sessionId;

  /// Открыть пополнение, когда на продление не хватает. Раньше лист отвечал «пополните на
  /// главной» — и человек, уже стоявший на главной, не находил, где это. null — пополнить отсюда
  /// нельзя (клуб не принимает онлайн, номер не подтверждён): тогда лист только объясняет.
  final VoidCallback? onTopUp;

  @override
  State<ExtendSessionSheet> createState() => _ExtendSessionSheetState();
}

class _ExtendSessionSheetState extends State<ExtendSessionSheet> {
  PlayerExtendOffersDto? _offers;
  bool _loadFailed = false;
  PlayerDurationOfferDto? _choice;
  bool _pending = false;
  String? _error;

  /// Отказ сервера был про деньги — лист предложит пополнить баланс.
  bool _shortOfMoney = false;

  /// Ключ попытки: повтор после обрыва обязан прийти с тем же, иначе сервер спишет деньги
  /// второй раз. Смена числа минут — уже другая попытка, и ключ ей нужен свой.
  final AttemptKey _attempt = AttemptKey();

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _loadFailed = false);
    try {
      final offers = await widget.api.getExtendOffers(widget.sessionId);
      if (!mounted) return;
      setState(() {
        _offers = offers;
        _choice = _preselect(offers.options, _choice?.minutes ?? defaultExtendMinutes);
      });
    } catch (_) {
      if (mounted) setState(() => _loadFailed = true);
    }
  }

  /// Тот же вариант, что был выбран, — если на него хватает; иначе первый, на который хватает.
  static PlayerDurationOfferDto? _preselect(List<PlayerDurationOfferDto> options, int minutes) {
    final affordable = options.where((option) => option.affordable).toList();
    for (final option in affordable) {
      if (option.minutes == minutes) return option;
    }
    return affordable.isEmpty ? null : affordable.first;
  }

  Future<void> _submit(PlayerDurationOfferDto choice) async {
    final l = L.of(context);
    setState(() {
      _pending = true;
      _error = null;
      _shortOfMoney = false;
    });

    try {
      await widget.api.extendSession(
        sessionId: widget.sessionId,
        additionalMinutes: choice.minutes,
        // Ключ рождается здесь, а не в клиенте API: повтор той же попытки должен нести тот
        // же ключ, иначе идемпотентность не спасёт от двойного списания.
        idempotencyKey: _attempt.forSubject('${widget.sessionId}:${choice.minutes}'),
      );
      _attempt.done();
      if (mounted) Navigator.of(context).pop(choice.minutes);
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _pending = false;
        _shortOfMoney = error.message == 'insufficient_balance';
        _error = switch ((error.statusCode, error.message)) {
          // Раньше любой 409 объявлялся нехваткой денег, хотя сервер кладёт в тело свою причину:
          // тариф кончился, столько времени взять нельзя, сессию уже нельзя продлить.
          (_, _) when error.isOffline => l.customerErrorOffline,
          (_, 'insufficient_balance') => l.customerSessionExtendErrBalance,
          (_, 'invalid_tariff') => l.customerTariffGone,
          (_, 'tariff_outside_its_hours') => l.customerTariffOutsideHours,
          (_, 'invalid_duration') => l.customerSessionErrDuration,
          (404, _) => l.customerSessionExtendErrGone,
          _ => l.customerSessionExtendErrGeneric,
        };
      });
      // Отказ обычно значит, что цены или баланс уже не те: варианты перечитываются.
      if (!error.isOffline && error.statusCode != 404) await _load();
    } catch (_) {
      // Любой другой сбой тоже обязан вернуть кнопку в рабочее состояние: незакрытое
      // «Продлеваем…» выглядит как зависшее списание, и игрок либо ждёт зря, либо жмёт ещё раз.
      if (!mounted) return;
      setState(() {
        _pending = false;
        _error = l.customerSessionExtendErrGeneric;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;
    final offers = _offers;
    final choice = _choice;
    String money(MoneyDto value) => formatMoney(value.minorUnits, value.currencyCode, locale: locale);

    final unavailable = switch (offers?.unavailableReason) {
      PlayerOfferUnavailableReasonNames.packageSession => l.customerSessionExtendUnavailablePackage,
      PlayerOfferUnavailableReasonNames.notPrepaid => l.customerSessionExtendUnavailableCounter,
      _ => null,
    };

    final ready = offers != null && unavailable == null;
    final topUp = widget.onTopUp;
    final offerTopUp = topUp != null && ready && (choice == null || _shortOfMoney);

    return AppSheet(
      title: l.customerSessionExtendTitle,
      content: [
        if (offers == null && !_loadFailed)
          Semantics(
            label: l.customerCommonLoading,
            child: const Row(
              children: [
                Expanded(child: SkeletonBox(height: 48)),
                SizedBox(width: Space.s2),
                Expanded(child: SkeletonBox(height: 48)),
                SizedBox(width: Space.s2),
                Expanded(child: SkeletonBox(height: 48)),
              ],
            ),
          )
        else if (offers == null)
          LoadFailure(message: l.customerSessionExtendLoadFailed, onRetry: _load)
        else if (unavailable != null)
          Text(unavailable, style: theme.textTheme.bodyMedium)
        else ...[
          Wrap(
            spacing: Space.s2,
            runSpacing: Space.s2,
            children: [
              for (final option in offers.options)
                ChoiceChip(
                  label: Text(extendDurationLabel(l, option.minutes)),
                  selected: choice?.minutes == option.minutes,
                  // На что не хватает — видно, но не нажать: отказ после нажатия хуже.
                  onSelected: _pending || !option.affordable
                      ? null
                      : (_) => setState(() {
                            _choice = option;
                            _error = null;
                          }),
                ),
            ],
          ),
          const SizedBox(height: Space.s4),
          if (choice != null)
            // Сколько именно спишется и что останется — до нажатия, словами сервера.
            Text(
              l.customerSessionExtendSummary(
                DateFormat.Hm(dateLocale(locale)).format(choice.endsAtUtc.toLocal()),
                money(choice.amount),
                money(choice.balanceAfter),
              ),
              style: theme.textTheme.bodyMedium,
            )
          else
            Text(l.customerSessionExtendNothingAffordable, style: theme.textTheme.bodyMedium),
        ],
        const SizedBox(height: Space.s3),
        // Откуда возьмутся деньги — сказано до нажатия, а не после списания.
        Text(
          l.customerSessionExtendHint,
          style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
        ),
      ],
      // Ошибка живёт рядом с кнопкой, а не всплывашкой поверх: игрок должен видеть причину и
      // сразу выбрать другой вариант, а не ловить исчезающую подсказку.
      actions: ready || _error != null
          ? ActionStack(
              error: _error,
              secondary: offerTopUp
                  ? AppAction(l.customerWalletTopUp, () {
                      Navigator.of(context).pop();
                      topUp();
                    })
                  : null,
              primary: ready
                  ? AppAction(
                      _pending
                          ? l.customerSessionExtendPending
                          : l.customerSessionExtendConfirm(
                              extendDurationLabel(l, choice?.minutes ?? defaultExtendMinutes)),
                      _pending || choice == null ? null : () => _submit(choice),
                    )
                  : null,
            )
          : null,
    );
  }
}
