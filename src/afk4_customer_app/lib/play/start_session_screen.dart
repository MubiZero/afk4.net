import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../api/dto.dart';
import '../api/idempotency.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import '../theme/app_theme.dart';

/// Сколько играть. Три ходовых варианта вместо ввода минут: игрок стоит посреди зала с
/// телефоном в руке, и лишний выбор здесь стоит ему времени, а клубу — очереди на стойке.
const List<int> playDurationsMinutes = [60, 120, 180];

/// Самостоятельная посадка: выбрать свободный ПК, тариф и время — и начать играть.
///
/// Это та операция, ради которой обычно ищут оператора. Пока он занят с другим гостем, игрок
/// ждёт; здесь он не ждёт вообще.
class StartSessionScreen extends StatefulWidget {
  const StartSessionScreen({super.key, required this.api, required this.branchId});

  final PlayerApiClient api;

  /// Филиал игрока — из его профиля. Без него спрашивать нечего: и места, и тарифы у клуба свои.
  final String branchId;

  @override
  State<StartSessionScreen> createState() => _StartSessionScreenState();
}

class _StartSessionScreenState extends State<StartSessionScreen> {
  List<PlayerSeat>? _seats;
  List<TariffOption> _tariffs = const [];
  bool _loadFailed = false;

  String? _seatId;
  String? _tariffId;
  int _minutes = playDurationsMinutes.first;

  ReservationQuote? _quote;
  int _quoteRequest = 0;

  bool _starting = false;
  String? _error;

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
        _seatId ??= seats.where((seat) => seat.isAvailable).firstOrNull?.seatId;
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
    final seat = _seats?.where((candidate) => candidate.seatId == _seatId).firstOrNull;
    final tariffId = _tariffId;
    if (seat == null || tariffId == null) return;

    setState(() {
      _starting = true;
      _error = null;
    });

    try {
      await widget.api.startSession(
        deviceId: seat.deviceId,
        tariffRuleVersionId: tariffId,
        durationMinutes: _minutes,
        idempotencyKey: newIdempotencyKey(),
      );
      if (!mounted) return;
      unawaited(HapticFeedback.mediumImpact());
      Navigator.of(context).pop(seat.seatName);
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _starting = false;
        _error = switch ((error.statusCode, error.message)) {
          (_, 'insufficient_balance') => l.customerPlayErrFunds,
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
      appBar: AppBar(title: Text(l.customerPlayTitle)),
      body: _body(l),
      bottomNavigationBar: (_seats?.any((seat) => seat.isAvailable) ?? false) && _tariffs.isNotEmpty
          ? _footer(l)
          : null,
    );
  }

  Widget _body(L l) {
    final theme = Theme.of(context);
    final seats = _seats;

    if (seats == null) {
      return _loadFailed
          ? Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Text(l.customerPlayLoadError, style: TextStyle(color: theme.colorScheme.error)),
              ),
            )
          : const Center(child: CircularProgressIndicator());
    }

    if (!seats.any((seat) => seat.isAvailable)) return _NoSeats();

    // Без тарифа сессию не начать, и выбирать на этом экране больше нечего. Раньше блок
    // тарифов просто исчезал, а кнопка оставалась серой — игрок видел неработающий экран
    // и не понимал, он что-то сделал не так или клуб.
    if (_tariffs.isEmpty) return _NoTariffs();

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text(l.customerPlaySeat, style: theme.textTheme.titleSmall),
          const SizedBox(height: 8),
          for (final seat in seats)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: _SeatTile(
                seat: seat,
                selected: seat.seatId == _seatId,
                onTap: seat.isAvailable ? () => setState(() => _seatId = seat.seatId) : null,
              ),
            ),
          if (_tariffs.isNotEmpty) ...[
            const SizedBox(height: 16),
            Text(l.customerReservationsTariff, style: theme.textTheme.titleSmall),
            const SizedBox(height: 8),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                for (final tariff in _tariffs)
                  ChoiceChip(
                    label: Text(tariff.name),
                    selected: tariff.tariffVersionId == _tariffId,
                    onSelected: (_) {
                      setState(() => _tariffId = tariff.tariffVersionId);
                      _refreshQuote();
                    },
                  ),
              ],
            ),
          ],
          const SizedBox(height: 16),
          Text(l.customerPlayDuration, style: theme.textTheme.titleSmall),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
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
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;
    final quote = _quote;
    final ready = _seatId != null && _tariffId != null;

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (_error != null) ...[
              Text(_error!, style: TextStyle(color: theme.colorScheme.error)),
              const SizedBox(height: 8),
            ],
            FilledButton(
              onPressed: _starting || !ready ? null : _start,
              // Выключенная кнопка обязана говорить, чего ждёт: без этого игрок жмёт по ней и
              // считает, что приложение сломалось.
              child: Text(
                switch ((_starting, _seatId, _tariffId, quote)) {
                  (true, _, _, _) => l.customerPlayStarting,
                  (_, null, _, _) => l.customerPlayPickSeat,
                  (_, _, null, _) => l.customerPlayPickTariff,
                  (_, _, _, null) => l.customerPlayTitle,
                  (_, _, _, final ready) => l.customerPlayConfirm(
                      formatMoney(ready!.amountMinorUnits, ready.currencyCode, locale: locale)),
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _SeatTile extends StatelessWidget {
  const _SeatTile({required this.seat, required this.selected, required this.onTap});

  final PlayerSeat seat;
  final bool selected;
  final VoidCallback? onTap;

  String _reason(L l) => switch (seat.unavailableReason) {
        'session' => l.customerPlayBusySession,
        'reservation' => l.customerPlayBusyReservation,
        _ => l.customerPlayBusyOffline,
      };

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final free = seat.isAvailable;

    return Material(
      color: selected
          ? theme.colorScheme.secondaryContainer
          : theme.colorScheme.surfaceContainerHighest,
      borderRadius: BorderRadius.circular(AppTheme.radiusControl),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppTheme.radiusControl),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              Icon(
                free ? Icons.desktop_windows_outlined : Icons.lock_outline,
                // Занятое место видно и без цвета — иконкой и подписью: цвет один читать нельзя.
                color: free ? theme.colorScheme.primary : theme.colorScheme.onSurfaceVariant,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(seat.seatName, style: theme.textTheme.titleMedium),
                    if (seat.zoneName.isNotEmpty)
                      Text(
                        seat.zoneName,
                        style: theme.textTheme.bodySmall
                            ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                      ),
                  ],
                ),
              ),
              if (!free)
                Text(
                  _reason(l),
                  style: theme.textTheme.bodySmall
                      ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                )
              else if (selected)
                Icon(Icons.check_circle, color: theme.colorScheme.primary),
            ],
          ),
        ),
      ),
    );
  }
}

/// Клуб не завёл цены. Игроку тут делать нечего, и сказать об этом надо прямо: серая кнопка
/// без объяснения выглядит поломкой приложения, хотя дело в настройках клуба.
class _NoTariffs extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.payments_outlined, size: 40, color: theme.colorScheme.onSurfaceVariant),
            const SizedBox(height: 12),
            Text(l.customerPlayNoTariffs, style: theme.textTheme.titleMedium),
            const SizedBox(height: 4),
            Text(
              l.customerPlayNoTariffsHint,
              textAlign: TextAlign.center,
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
          ],
        ),
      ),
    );
  }
}

class _NoSeats extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.event_seat_outlined, size: 40, color: theme.colorScheme.onSurfaceVariant),
            const SizedBox(height: 12),
            Text(l.customerPlayNoSeats, style: theme.textTheme.titleMedium),
            const SizedBox(height: 4),
            Text(
              l.customerPlayNoSeatsHint,
              textAlign: TextAlign.center,
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
          ],
        ),
      ),
    );
  }
}
