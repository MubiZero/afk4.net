import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import 'date_time_field.dart';
import 'reservations_screen.dart';
import 'tariff_picker.dart';

/// Новая бронь: когда игрок хочет прийти.
///
/// Место выбирает клуб, поэтому лист об этом прямо говорит — иначе игрок не понимает,
/// получит ли он вообще машину, а в киберклубе это половина смысла брони.
class NewReservationSheet extends StatefulWidget {
  const NewReservationSheet({super.key, required this.api, required this.clock});

  final PlayerApiClient api;
  final DateTime Function() clock;

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

  List<TariffOption> _tariffs = const [];
  String? _tariffId;
  ReservationQuote? _quote;
  bool _quoting = false;
  String? _priceProblem;

  /// Номер последнего запроса цены. Игрок успевает перещёлкать тарифы и время быстрее, чем
  /// отвечает сеть, и без этого счётчика на экран мог бы лечь ответ на предыдущий выбор.
  int _quoteRequest = 0;

  @override
  void initState() {
    super.initState();
    _loadTariffs();
  }

  Future<void> _loadTariffs() async {
    try {
      final profile = await widget.api.getProfile();
      final branchId = profile.homeBranchId;
      if (branchId == null) return;
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
      await widget.api.createReservation(
        startsAtUtc: _startsAt!,
        endsAtUtc: _endsAt!,
        tariffVersionId: _tariffId,
      );
      if (mounted) Navigator.of(context).pop(true);
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _pending = false;
        // 409 — время занято. Это не сбой, а нормальный ответ, и звучать он должен иначе.
        _problem = error.statusCode == 409
            ? l.customerReservationsConflict
            : l.customerReservationsCreateError;
      });
    }
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
            const SizedBox(height: 12),
            Text(
              l.customerReservationsSeatNote,
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: _pending ? null : _create,
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
