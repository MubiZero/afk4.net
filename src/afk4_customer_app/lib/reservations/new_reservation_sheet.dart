import 'dart:async';

import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../organization/branch_choice.dart';
import 'date_time_field.dart';
import 'reservations_screen.dart';
import 'tariff_picker.dart';

/// Новая бронь: когда игрок хочет прийти.
///
/// Место выбирает клуб, поэтому лист об этом прямо говорит — иначе игрок не понимает,
/// получит ли он вообще машину, а в киберклубе это половина смысла брони.
class NewReservationSheet extends StatefulWidget {
  const NewReservationSheet({
    super.key,
    required this.api,
    required this.clock,
    this.accountOpen = true,
    this.branch = const BranchChoice(),
  });

  final PlayerApiClient api;
  final DateTime Function() clock;

  /// Есть ли у игрока счёт в этом клубе. Пока нет, прайс спрашивать не по чему: он идёт по
  /// профилю, а профиль появляется вместе со счётом. Правила приёма теперь спрашиваются и
  /// до него — по названному залу.
  final bool accountOpen;

  /// Зал первой брони. Со счётом в клубе не нужен: зал в нём уже записан.
  final BranchChoice branch;

  @override
  State<NewReservationSheet> createState() => _NewReservationSheetState();
}

class _NewReservationSheetState extends State<NewReservationSheet> {
  DateTime? _startsAt;
  DateTime? _endsAt;
  bool _pending = false;

  /// Что не так со временем. Ошибка живёт под полями, а не в снекбаре внизу экрана:
  /// исправлять её нужно здесь же, а снекбар исчезает вместе с объяснением.
  String? _problem;

  /// Сколько мест бронируется. Один — обычная бронь, больше — компания: сервер заведёт их
  /// одной группой и заморозит деньги за всех сразу.
  int _seats = 1;

  /// Столько же, сколько разрешает сервер: больше восьми человек в клубе договариваются
  /// голосом, а не через форму.
  static const int _maxSeats = 8;

  List<TariffOption> _tariffs = const [];

  /// Правила приёма заявок у филиала — то, чем лист объясняет «так решил клуб». null —
  /// не спросились; тогда лист молчит, а не выдумывает за клуб.
  PlayerBookingRules? _rules;
  String? _tariffId;
  ReservationQuote? _quote;
  bool _quoting = false;
  String? _priceProblem;

  /// Номер последнего запроса цены. Игрок успевает перещёлкать тарифы и время быстрее, чем
  /// отвечает сеть, и без этого счётчика на экран мог бы лечь ответ на предыдущий выбор.
  int _quoteRequest = 0;

  /// Зал, в который придёт игрок. Живёт в листе, а не только наверху: ответ должен быть виден
  /// в той же форме, где его дали.
  String? _branchId;

  /// Выбор зала в том виде, в каком его показывают и отправляют. Со счётом в клубе выбора
  /// нет: зал записан в счёте, и названный заново его не переписывает.
  BranchChoice get _choice => widget.accountOpen
      ? const BranchChoice()
      : BranchChoice(halls: widget.branch.halls, chosenId: _branchId, onChosen: _chooseBranch);

  @override
  void initState() {
    super.initState();
    _branchId = widget.branch.branchId;
    _loadTariffs();
    // Правила приёма — по названному залу, ещё до счёта: предоплата и ручной приём нужнее
    // всего тому, кто бронирует здесь впервые.
    final branchId = _branchId;
    if (!widget.accountOpen && branchId != null) unawaited(_loadRules(branchId));
  }

  /// Игрок назвал зал. Правила у каждого свои, поэтому спрашиваются заново, а наверх ответ
  /// уходит затем, чтобы пополнение не спросило то же самое второй раз.
  void _chooseBranch(String branchId) {
    setState(() {
      _branchId = branchId;
      _rules = null;
    });
    widget.branch.onChosen?.call(branchId);
    unawaited(_loadRules(branchId));
  }

  Future<void> _loadTariffs() async {
    if (!widget.accountOpen) return;
    try {
      final profile = await widget.api.getProfile();
      final branchId = profile.homeBranchId;
      if (branchId == null) return;
      unawaited(_loadRules(branchId));
      final tariffs = await widget.api.getTariffs(branchId);
      if (!mounted) return;
      setState(() {
        _tariffs = tariffs;
        // Единственный тариф выбирать не за что — он и так выбран. Клубу с одним прайсом
        // это экономит касание, а игроку показывает цену сразу.
        if (tariffs.length == 1) _tariffId = tariffs.single.tariffVersionId;
      });
      _refreshQuote();
    } on PlayerApiException {
      // Прайса нет — бронь всё равно можно поставить, её посчитают на стойке.
    }
  }

  /// Правила клуба. Не спросились — лист работает как работал: отказ всё равно объяснит
  /// сервер, а выдуманное правило хуже отсутствующего.
  Future<void> _loadRules(String branchId) async {
    try {
      final rules = await widget.api.getBookingRules(branchId);
      if (mounted) setState(() => _rules = rules);
    } on PlayerApiException {
      // Молчим: правила — объяснение, а не условие брони.
    }
  }

  /// Пересчитывает стоимость под текущий выбор. Зовётся при смене тарифа и времени: цена
  /// зависит и от того, и от другого.
  Future<void> _refreshQuote() async {
    final tariffId = _tariffId;
    final start = _startsAt;
    final end = _endsAt;
    if (tariffId == null || start == null || end == null || !end.isAfter(start)) {
      setState(() {
        _quote = null;
        _priceProblem = null;
      });
      return;
    }

    final request = ++_quoteRequest;
    setState(() {
      _quoting = true;
      _priceProblem = null;
    });

    try {
      final quote = await widget.api.quoteReservation(
        tariffVersionId: tariffId,
        startsAtUtc: start,
        endsAtUtc: end,
        seatCount: _seats,
      );
      if (!mounted || request != _quoteRequest) return;
      setState(() {
        _quote = quote;
        _quoting = false;
      });
    } on PlayerApiException catch (error) {
      if (!mounted || request != _quoteRequest) return;
      final l = L.of(context);
      setState(() {
        _quoting = false;
        _quote = null;
        _priceProblem = error.statusCode == 404
            ? l.customerReservationsTariffGone
            : l.customerReservationsPriceFailed;
      });
    }
  }

  Future<void> _create() async {
    final l = L.of(context);
    final problem = reservationTimeProblem(l, _startsAt, _endsAt, now: widget.clock());
    if (problem != null) {
      setState(() => _problem = problem);
      return;
    }

    setState(() {
      _problem = null;
      _pending = true;
    });
    try {
      // Зал называется только там, где его ещё некому знать: у игрока со счётом он записан
      // в самом счёте.
      final branchId = widget.accountOpen ? null : _branchId;
      if (_seats > 1) {
        await widget.api.createReservationGroup(
          seatCount: _seats,
          startsAtUtc: _startsAt!,
          endsAtUtc: _endsAt!,
          tariffVersionId: _tariffId,
          branchId: branchId,
        );
      } else {
        await widget.api.createReservation(
          startsAtUtc: _startsAt!,
          endsAtUtc: _endsAt!,
          tariffVersionId: _tariffId,
          branchId: branchId,
        );
      }
      if (mounted) Navigator.of(context).pop(true);
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _pending = false;
        // Занятое время и нехватка денег — не сбои, а нормальные ответы сервера, и звучать
        // они должны каждый по-своему: из первого выход — другое время, из второго — пополнить
        // кошелёк.
        _problem = switch ((error.statusCode, error.message)) {
          // Причина важнее кода: «нет денег на всю компанию» — это другой ответ, чем «время занято».
          (_, 'insufficient_funds') => _seats > 1
              ? l.customerReservationsGroupNoFunds
              : l.customerReservationsNoFunds,
          (_, 'invalid_seat_count') => l.customerReservationsGroupTooMany('$_maxSeats'),
          // «Мест нет» — это не «время занято»: время свободно, кончились машины. Компании
          // добавляется выход, которого у одиночной брони нет, — взять меньше мест.
          (_, 'no_seats_available') => _seats > 1
              ? l.customerReservationsGroupNoSeats
              : l.customerReservationsNoSeats,
          // Тариф с расписанием на выбранный час не действует. Выход отсюда — другой тариф или
          // другое время, и общая «не удалось» не подсказывает ни того, ни другого.
          (_, 'tariff_outside_its_hours') => l.customerReservationsTariffOutsideHours,
          // Решения клуба, а не сбои: у каждого свой выход — позвонить, пополнить,
          // дождаться своей брони.
          (_, 'booking_disabled') => l.customerReservationsErrDisabled,
          (_, 'prepayment_required') => l.customerReservationsErrPrepay,
          (_, 'active_reservation_limit') => l.customerReservationsErrLimit,
          // Зал не назван или его в сети уже нет. Оба ответа с выходом: назвать зал в форме
          // выше — он тут же, над временем.
          (_, 'branch_required') => l.customerBranchErrRequired,
          (_, 'branch_not_found') => l.customerBranchErrGone,
          // Клуб закрыл счёт: это не «время занято» и не сбой. Общий ответ здесь врал бы про
          // причину и звал попробовать другое время, которого не хватит.
          (_, 'club_account_closed') => l.customerClubErrClosed,
          (409, _) => l.customerReservationsConflict,
          _ => l.customerReservationsCreateError,
        };
      });
    }
  }

  /// Что клуб решил насчёт заявок — до того, как игрок заполнит форму и получит отказ.
  /// Молчим, когда решать нечего: клуб подтверждает сам и предоплаты с этого игрока не берёт.
  List<Widget> _clubRules(L l, ThemeData theme) {
    final rules = _rules;
    if (rules == null) return const [];

    final notes = <String>[
      if (rules.bookingOff) l.customerReservationsRuleOff,
      if (rules.reviewedByStaff)
        l.customerReservationsRuleManual('${rules.respondWithinMinutes}'),
      if (rules.prepaymentRequired) l.customerReservationsRulePrepay,
    ];
    if (notes.isEmpty) return const [];

    return [
      const SizedBox(height: 8),
      for (final note in notes)
        Padding(
          padding: const EdgeInsets.only(top: 4),
          child: Text(
            note,
            style: theme.textTheme.bodySmall?.copyWith(
              color: rules.bookingOff
                  ? theme.colorScheme.error
                  : theme.colorScheme.onSurfaceVariant,
            ),
          ),
        ),
    ];
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(l.customerReservationsNewTitle, style: theme.textTheme.titleLarge),
            // Зал идёт первым: от него зависят и правила приёма под ним, и сама возможность
            // забронировать. Спрашивать его после заполненной формы — значит отменять
            // ответы, которые игрок уже дал.
            if (_choice.asks) ...[
              const SizedBox(height: 16),
              BranchPicker(choice: _choice),
            ],
            ..._clubRules(l, theme),
            const SizedBox(height: 16),
            DateTimeField(
              label: l.customerReservationsStart,
              value: _startsAt,
              firstAllowed: widget.clock(),
              onChanged: (value) {
                setState(() {
                  _startsAt = value;
                  _problem = null;
                });
                _refreshQuote();
              },
            ),
            const SizedBox(height: 12),
            DateTimeField(
              label: l.customerReservationsEnd,
              value: _endsAt,
              firstAllowed: _startsAt ?? widget.clock(),
              onChanged: (value) {
                setState(() {
                  _endsAt = value;
                  _problem = null;
                });
                _refreshQuote();
              },
            ),
            if (_problem != null) ...[
              const SizedBox(height: 8),
              Text(_problem!, style: TextStyle(color: theme.colorScheme.error)),
            ],
            const SizedBox(height: 16),
            // Тариф идёт после времени: цена зависит от длительности, и до выбора времени
            // показывать её нечем.
            TariffPicker(
              tariffs: _tariffs,
              selectedId: _tariffId,
              quote: _quote,
              quoting: _quoting,
              problem: _priceProblem,
              onSelected: (id) {
                setState(() => _tariffId = id);
                _refreshQuote();
              },
            ),
            const SizedBox(height: 16),
            _SeatCountField(
              seats: _seats,
              maxSeats: _maxSeats,
              onChanged: (value) {
                setState(() {
                  _seats = value;
                  _problem = null;
                });
                _refreshQuote();
              },
            ),
            const SizedBox(height: 12),
            Text(
              _seats > 1 ? l.customerReservationsGroupSeatNote : l.customerReservationsSeatNote,
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 16),
            FilledButton(
              // Пока зал не назван, бронировать нечем: сервер ответил бы тем самым отказом,
              // из-за которого этот вопрос и появился. Граница видна сразу — кнопка гаснет,
              // а не отвечает отказом после нажатия.
              onPressed: _pending || _choice.unanswered ? null : _create,
              child: Text(
                _pending ? l.customerReservationsCreating : l.customerReservationsCreate,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Сколько мест бронируем.
///
/// Шаговый выбор, а не поле ввода: компания в киберклубе — это два-три-четыре человека, и
/// клавиатура ради однозначного числа лишняя. Границы видны сразу — кнопка гаснет, а не
/// отвечает отказом после нажатия.
class _SeatCountField extends StatelessWidget {
  const _SeatCountField({
    required this.seats,
    required this.maxSeats,
    required this.onChanged,
  });

  final int seats;
  final int maxSeats;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Row(
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(l.customerReservationsSeats, style: theme.textTheme.titleSmall),
              Text(
                seats > 1 ? l.customerReservationsSeatsCompany : l.customerReservationsSeatsAlone,
                style:
                    theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
            ],
          ),
        ),
        IconButton.outlined(
          onPressed: seats > 1 ? () => onChanged(seats - 1) : null,
          icon: const Icon(Icons.remove),
          tooltip: l.customerReservationsSeatsFewer,
        ),
        SizedBox(
          width: 44,
          child: Text(
            '$seats',
            textAlign: TextAlign.center,
            style: theme.textTheme.titleLarge,
          ),
        ),
        IconButton.outlined(
          onPressed: seats < maxSeats ? () => onChanged(seats + 1) : null,
          icon: const Icon(Icons.add),
          tooltip: l.customerReservationsSeatsMore,
        ),
      ],
    );
  }
}
