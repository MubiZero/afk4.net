import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../api/contracts.dart';
import '../api/dto_rules.dart';
import '../api/idempotency.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import '../reservations/tariff_picker.dart';
import '../shell/load_failure.dart';
import '../profile/pin_sheet.dart';
import '../shell/app_sheet.dart';
import '../shell/actions.dart';
import '../shell/empty_state.dart';
import '../shell/group_header.dart';
import '../shell/skeleton.dart';
import '../theme/space.dart';
import '../shell/app_scaffold.dart';

/// Сколько играть. Три ходовых варианта вместо ввода минут: игрок стоит посреди зала с
/// телефоном в руке, и лишний выбор здесь стоит ему времени, а клубу — очереди на стойке.
const List<int> playDurationsMinutes = [60, 120, 180];

/// Чем кончился шаг выбора времени.
sealed class SitDownOutcome {
  const SitDownOutcome();
}

/// Сессия началась с телефона — на этом ПК.
final class SessionStarted extends SitDownOutcome {
  const SessionStarted(this.seatName);

  final String seatName;
}

/// Время игрок выберет на самом ПК: телефон только впускает его.
final class ChooseOnPc extends SitDownOutcome {
  const ChooseOnPc();
}

/// Второй шаг «Сесть за ПК»: ПК уже назван кодом с его монитора, осталось выбрать тариф и время
/// и начать играть с баланса.
///
/// Это та операция, ради которой обычно ищут оператора. Пока он занят с другим гостем, игрок
/// ждёт; здесь он не ждёт вообще. Выбирать здесь не обязательно: тихая кнопка внизу отдаёт выбор
/// самому ПК — там тот же прайс, и кому-то удобнее решать, уже сев.
class StartSessionScreen extends StatefulWidget {
  const StartSessionScreen({
    super.key,
    required this.api,
    required this.branchId,
    required this.seatingCode,
    this.pinSet,
  });

  final PlayerApiClient api;

  /// Код с монитора, прочитанный на первом шаге. Раньше здесь лежал выбранный из списка ПК — и
  /// занять машину можно было не приходя в клуб.
  final String seatingCode;

  /// Задан ли ПИН-код для посадки за ПК. null — неизвестно (профиль не прочитан): тогда молчим,
  /// потому что пугать человека предупреждением о том, чего мы не знаем, хуже молчания.
  final bool? pinSet;

  /// Филиал игрока — из его профиля. Без него спрашивать нечего: и места, и тарифы у клуба свои.
  final String branchId;

  @override
  State<StartSessionScreen> createState() => _StartSessionScreenState();
}

class _StartSessionScreenState extends State<StartSessionScreen> {
  List<PlayerSeatDto>? _seats;
  List<TariffOptionDto> _tariffs = const [];
  bool _loadFailed = false;

  String? _tariffId;
  int _minutes = playDurationsMinutes.first;

  ReservationQuoteDto? _quote;
  int _quoteRequest = 0;

  bool _starting = false;

  /// ПИН задали прямо здесь: предупреждение уходит, не дожидаясь перечитывания профиля.
  bool _pinJustSet = false;
  String? _error;

  /// Ключ попытки: повтор после обрыва обязан прийти с тем же, иначе сессия начнётся дважды
  /// и деньги спишутся дважды. Другое место, тариф или время — другая попытка со своим ключом.
  final AttemptKey _attempt = AttemptKey();

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final seats = await widget.api.getSeats(widget.branchId);
      final tariffs = await widget.api.getTariffs(widget.branchId);
      if (!mounted) return;
      setState(() {
        _seats = seats;
        _tariffs = tariffs;
        _loadFailed = false;
        // Единственный тариф выбирать не из чего, а свободное место чаще всего одно и то же —
        // предвыбор экономит два касания в ситуации, где игрок торопится.
        _tariffId ??= tariffs.length == 1 ? tariffs.single.tariffVersionId : _tariffId;
      });
      _refreshQuote();
    } on PlayerApiException {
      if (mounted) setState(() => _loadFailed = _seats == null);
    }
  }

  /// Стоимость выбранного времени. Спрашивается у сервера тем же расчётом, что и для брони:
  /// правила минимума и округления одни и те же, а вторая арифметика в приложении разошлась бы
  /// с настоящим списанием.
  Future<void> _refreshQuote() async {
    final tariffId = _tariffId;
    if (tariffId == null) {
      setState(() => _quote = null);
      return;
    }

    final request = ++_quoteRequest;
    final now = DateTime.now();
    try {
      final quote = await widget.api.quoteReservation(
        tariffVersionId: tariffId,
        startsAtUtc: now,
        endsAtUtc: now.add(Duration(minutes: _minutes)),
      );
      if (!mounted || request != _quoteRequest) return;
      setState(() => _quote = quote);
    } on PlayerApiException {
      if (!mounted || request != _quoteRequest) return;
      // Цена — подсказка: без неё сессию всё равно можно начать, сумму скажет сервер отказом.
      setState(() => _quote = null);
    }
  }

  Future<void> _start() async {
    final l = L.of(context);
    final tariffId = _tariffId;
    final code = widget.seatingCode;
    if (tariffId == null) return;

    setState(() {
      _starting = true;
      _error = null;
    });

    try {
      final seatId = await widget.api.startSession(
        seatingCode: code,
        tariffRuleVersionId: tariffId,
        durationMinutes: _minutes,
        idempotencyKey: _attempt.forSubject('$code:$tariffId:$_minutes'),
      );
      _attempt.done();
      if (!mounted) return;
      unawaited(HapticFeedback.mediumImpact());
      // Имя места подтверждает, что человек не ошибся монитором: код он набрал с одного экрана,
      // а сессия началась именно там, где он стоит.
      final seatName = _seats
          ?.where((seat) => seat.seatId == seatId)
          .firstOrNull
          ?.seatName;
      Navigator.of(context).pop(SessionStarted(seatName ?? l.customerPlayTitle));
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _starting = false;
        _error = switch ((error.statusCode, error.message)) {
          // Платформа закрыла человеку сеть: «попробуйте ещё раз» звало бы повторять то, что не
          // выйдет ни с какого раза. Подробности — в полосе поверх разделов.
          (_, _) when error.isOffline => l.customerErrorOffline,
          (_, 'network_banned') => l.customerBanTitle,
          (_, 'seating_code_invalid') => l.customerPlayErrCode,
          (_, 'insufficient_balance') => l.customerPlayErrFunds,
          // ПК показывает код, но к платформе не привязан: сам игрок этого не исправит, а
          // «попробуйте ещё раз» отправило бы его набирать те же цифры до бесконечности.
          (_, 'device_not_assigned') => l.customerPlayErrDeviceGone,
          // ПК сверх бесплатного тарифа клуба: место свободно, но сессию на нём не начать.
          (_, 'device_outside_plan') => l.customerPlayErrOutsidePlan,
          // Клуб сам закрыл этот ПК: «место заняли» звало бы выбрать другое в спешке, а тут
          // спешить некуда — ПК вернут в зал, когда закончат.
          (_, 'device_in_maintenance') => l.customerPlayErrMaintenance,
          (_, 'invalid_tariff') => l.customerTariffGone,
          (_, 'tariff_outside_its_hours') => l.customerTariffOutsideHours,
          (_, 'invalid_duration') => l.customerSessionErrDuration,
          (_, 'club_account_closed') => l.customerClubErrClosed,
          (_, 'branch_required') => l.customerBranchErrRequired,
          (_, 'branch_not_found') => l.customerBranchErrGone,
          (_, 'FeatureDisabled') => l.customerErrorFeatureOff,
          (409, _) => l.customerPlayErrTaken,
          _ => l.customerPlayErrGeneric,
        };
      });
      // Место могли занять за те секунды, что игрок выбирал: список перечитывается, чтобы он
      // выбирал из действительно свободного.
      await _load();
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _starting = false;
        _error = l.customerPlayErrGeneric;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);

    return Scaffold(
      appBar: nestedAppBar(context, title: l.customerPlayTitle),
      body: _body(l),
      bottomNavigationBar: (_seats?.any((seat) => seat.isAvailable) ?? false) && _tariffs.isNotEmpty
          ? _footer(l)
          : null,
    );
  }

  Future<void> _setPin() async {
    final saved = await showAppSheet<bool>(
      context,
      (_) => PinSheet(api: widget.api, pinSet: false),
    );
    if (saved != true || !mounted) return;
    setState(() => _pinJustSet = true);
  }

  Widget _body(L l) {
    final theme = Theme.of(context);
    final seats = _seats;

    if (seats == null) {
      return _loadFailed
          ? LoadFailure(message: l.customerPlayLoadError, onRetry: _load)
          : ListSkeleton(label: l.customerCommonLoading);
    }

    if (!seats.any((seat) => seat.isAvailable)) {
      return EmptyState(
        icon: Icons.event_seat_outlined,
        title: l.customerPlayNoSeats,
        hint: l.customerPlayNoSeatsHint,
      );
    }

    // Без тарифа сессию не начать, и выбирать на этом экране больше нечего. Раньше блок
    // тарифов просто исчезал, а кнопка оставалась серой — игрок видел неработающий экран
    // и не понимал, он что-то сделал не так или клуб.
    if (_tariffs.isEmpty) {
      return EmptyState(
        icon: Icons.payments_outlined,
        title: l.customerPlayNoTariffs,
        hint: l.customerPlayNoTariffsHint,
        // Цены на телефоне не заведены, но ПК, может быть, пустит: вход на нём не требует выбора
        // здесь.
        action: SecondaryButton(action: AppAction(l.customerPlayChooseOnPc, _chooseOnPc)),
      );
    }

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(Space.s4),
        children: [
          // Про ПИН человек узнавал, только дойдя до ПК: экран лаунчера просит номер и код,
          // которого нет, — и обещание «начните игру без оператора» кончалось дорогой к стойке.
          if (widget.pinSet == false && !_pinJustSet) ...[
            Card(
              child: Padding(
                padding: const EdgeInsets.all(Space.s4),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      l.customerPlayPinNeeded,
                      style: theme.textTheme.bodyMedium,
                    ),
                    const SizedBox(height: Space.s3),
                    SecondaryButton(action: AppAction(l.customerPinSet, _setPin)),
                  ],
                ),
              ),
            ),
            const SizedBox(height: Space.s4),
          ],
          // Места — справкой: «есть ли вообще куда сесть». Выбирать из списка нечего — машину
          // назвал тот ПК, перед которым человек стоит.
          Text(
            l.customerPlaySeatsFree(seats.where((seat) => seat.isAvailable).length.toString()),
            style: theme.textTheme.bodyMedium
                ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
          if (_tariffs.isNotEmpty) ...[
            const SizedBox(height: Space.s4),
            GroupHeader(l.customerReservationsTariff),
            Wrap(
              spacing: Space.s2,
              runSpacing: Space.s2,
              children: [
                // Играют сию секунду — значит и тариф нужен действующий сию секунду. Тариф вне
                // своих часов не прячется: пропавший из списка «Утренний» читается как сбой, а
                // названный со своими часами объясняет и себя, и почему сейчас его не взять.
                // Выбрать его нельзя — иначе человек жмёт «Начать» и получает отказ сервера,
                // так и не поняв, при чём тут утро.
                for (final tariff in _tariffs)
                  ChoiceChip(
                    label: Text([
                      tariff.name,
                      ?tariffScheduleLabel(tariff, l),
                      if (!tariff.isAvailableNow) l.customerTariffUnavailableNow,
                    ].join(' · ')),
                    selected: tariff.tariffVersionId == _tariffId,
                    onSelected: tariff.isAvailableNow
                        ? (_) {
                            setState(() => _tariffId = tariff.tariffVersionId);
                            _refreshQuote();
                          }
                        : null,
                  ),
              ],
            ),
          ],
          const SizedBox(height: Space.s4),
          GroupHeader(l.customerPlayDuration),
          Wrap(
            spacing: Space.s2,
            runSpacing: Space.s2,
            children: [
              for (final minutes in playDurationsMinutes)
                ChoiceChip(
                  label: Text(l.customerSessionExtendHours(minutes ~/ 60)),
                  selected: minutes == _minutes,
                  onSelected: (_) {
                    setState(() => _minutes = minutes);
                    _refreshQuote();
                  },
                ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _footer(L l) {
    final locale = Localizations.localeOf(context).languageCode;
    final quote = _quote;

    return PinnedActions(
      child: ActionStack(
        error: _error,
        // Чего ждёт выключенная кнопка — строкой над ней, а не вместо её названия.
        hint: _starting || _tariffId != null ? null : l.customerPlayPickTariff,
        primary: AppAction(
          switch ((_starting, _tariffId == null ? null : quote)) {
            (true, _) => l.customerPlayStarting,
            (_, final ready?) =>
              l.customerPlayConfirm(formatMoney(ready.amountMinorUnits, ready.currencyCode, locale: locale)),
            _ => l.customerPlayTitle,
          },
          _starting || _tariffId == null ? null : _start,
        ),
        tertiary: AppAction(l.customerPlayChooseOnPc, _starting ? null : _chooseOnPc),
      ),
    );
  }

  void _chooseOnPc() => Navigator.of(context).pop(const ChooseOnPc());
}
