import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../format/date_time.dart';
import '../l10n/app_localizations.dart';
import 'date_time_field.dart';

/// Что не так со временем — до отправки. Сервер проверяет то же самое, но отвечает общей
/// ошибкой, а игроку нужно знать, какое из двух полей чинить. null — всё в порядке.
String? reservationTimeProblem(L l, DateTime? start, DateTime? end, {required DateTime now}) {
  if (start == null || end == null) return l.customerReservationsTimeError;
  if (!start.isAfter(now)) return l.customerReservationsStartInPast;
  if (!end.isAfter(start)) return l.customerReservationsEndBeforeStart;
  return null;
}

/// Брони игрока: что уже забронировано и как забронировать ещё.
class ReservationsScreen extends StatefulWidget {
  const ReservationsScreen({
    super.key,
    required this.api,
    required this.phoneVerified,
    this.clock = DateTime.now,
  });

  final PlayerApiClient api;
  final bool phoneVerified;
  final DateTime Function() clock;

  @override
  State<ReservationsScreen> createState() => _ReservationsScreenState();
}

enum _Load { loading, failed, ready }

class _ReservationsScreenState extends State<ReservationsScreen> {
  _Load _state = _Load.loading;
  List<PlayerReservation> _reservations = const [];
  DateTime? _startsAt;
  DateTime? _endsAt;
  bool _pending = false;

  @override
  void initState() {
    super.initState();
    _refresh();
  }

  Future<void> _refresh() async {
    setState(() => _state = _state == _Load.ready ? _Load.ready : _Load.loading);
    try {
      final reservations = await widget.api.getReservations();
      if (!mounted) return;
      setState(() {
        _reservations = reservations;
        _state = _Load.ready;
      });
    } on PlayerApiException {
      if (!mounted) return;
      // Пустой список вместо ошибки — враньё: «броней нет» и «мы их не увидели» это разные
      // вещи, и на первом игрок спокойно уйдёт мимо своей брони.
      setState(() => _state = _Load.failed);
    }
  }

  Future<void> _create() async {
    final l = L.of(context);
    final problem = reservationTimeProblem(l, _startsAt, _endsAt, now: widget.clock());
    if (problem != null) {
      _say(problem);
      return;
    }

    setState(() => _pending = true);
    try {
      await widget.api.createReservation(startsAtUtc: _startsAt!, endsAtUtc: _endsAt!);
      if (!mounted) return;
      setState(() {
        _startsAt = null;
        _endsAt = null;
      });
      _say(l.customerReservationsCreated);
      await _refresh();
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      // 409 — время занято. Это не сбой, а нормальный ответ, и звучать он должен иначе.
      _say(error.statusCode == 409
          ? l.customerReservationsConflict
          : l.customerReservationsCreateError);
    } finally {
      if (mounted) setState(() => _pending = false);
    }
  }

  Future<void> _cancel(PlayerReservation reservation) async {
    final l = L.of(context);
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        content: Text(l.customerReservationsCancelConfirm),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(l.customerCommonBack),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(l.customerReservationsCancel),
          ),
        ],
      ),
    );
    if (confirmed != true) return;

    try {
      await widget.api.cancelReservation(reservation.reservationId);
      if (!mounted) return;
      _say(l.customerReservationsCancelled);
      await _refresh();
    } on PlayerApiException {
      if (mounted) _say(l.customerReservationsCancelError);
    }
  }

  void _say(String message) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: Text(l.customerReservationsTitle)),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          if (widget.phoneVerified) _form(l) else _gate(l, theme),
          const SizedBox(height: 16),
          switch (_state) {
            _Load.loading => Semantics(
                label: l.a11yLoadingReservations,
                child: const Center(child: Padding(
                  padding: EdgeInsets.all(32),
                  child: CircularProgressIndicator(),
                )),
              ),
            _Load.failed => Center(
                child: Column(
                  children: [
                    Text(l.customerReservationsLoadError,
                        style: TextStyle(color: theme.colorScheme.error)),
                    const SizedBox(height: 8),
                    TextButton(onPressed: _refresh, child: Text(l.customerCommonRetry)),
                  ],
                ),
              ),
            _Load.ready when _reservations.isEmpty => Center(
                child: Padding(
                  padding: const EdgeInsets.all(32),
                  child: Text(
                    l.customerReservationsNone,
                    style: theme.textTheme.bodyMedium
                        ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                  ),
                ),
              ),
            _Load.ready => Column(
                children: [
                  for (final reservation in _reservations)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: _ReservationCard(
                        reservation: reservation,
                        onCancel: () => _cancel(reservation),
                      ),
                    ),
                ],
              ),
          },
        ],
      ),
    );
  }

  Widget _form(L l) => Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              DateTimeField(
                label: l.customerReservationsStart,
                value: _startsAt,
                firstAllowed: widget.clock(),
                onChanged: (value) => setState(() => _startsAt = value),
              ),
              const SizedBox(height: 12),
              DateTimeField(
                label: l.customerReservationsEnd,
                value: _endsAt,
                firstAllowed: _startsAt ?? widget.clock(),
                onChanged: (value) => setState(() => _endsAt = value),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: _pending ? null : _create,
                child: Text(_pending
                    ? l.customerReservationsCreating
                    : l.customerReservationsCreate),
              ),
            ],
          ),
        ),
      );

  Widget _gate(L l, ThemeData theme) => Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Text(
            l.customerReservationsGate,
            style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
        ),
      );
}

class _ReservationCard extends StatelessWidget {
  const _ReservationCard({required this.reservation, required this.onCancel});

  final PlayerReservation reservation;
  final VoidCallback onCancel;

  /// Состояние словами. Незнакомое приходит с сервера как есть — лучше сырой код, чем
  /// уверенное враньё про «подтверждена».
  String _stateLabel(L l) => switch (reservation.state) {
        'pending' => l.customerReservationsStatePending,
        'confirmed' => l.customerReservationsStateConfirmed,
        'seated' => l.customerReservationsStateSeated,
        'cancelled' => l.customerReservationsStateCancelled,
        _ => reservation.state,
      };

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
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(reservation.seatName ?? l.customerReservationsNoSeat,
                    style: theme.textTheme.titleMedium),
                Text(
                  _stateLabel(l),
                  style: theme.textTheme.bodyMedium
                      ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                ),
              ],
            ),
            const SizedBox(height: 4),
            Text(
              '${formatDateTime(reservation.startsAtUtc, locale)} — '
              '${formatDateTime(reservation.endsAtUtc, locale)}',
              style: theme.textTheme.bodyMedium
                  ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            if (reservation.isCancellable)
              Align(
                alignment: Alignment.centerLeft,
                child: TextButton(
                  onPressed: onCancel,
                  style: TextButton.styleFrom(foregroundColor: theme.colorScheme.error),
                  child: Text(l.customerReservationsCancel),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
