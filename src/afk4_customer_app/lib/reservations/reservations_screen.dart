import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../format/date_time.dart';
import '../l10n/app_localizations.dart';
import '../phone/phone_verification_sheet.dart';
import 'new_reservation_sheet.dart';

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
    this.onPhoneVerified,
    this.clock = DateTime.now,
  });

  final PlayerApiClient api;
  final bool phoneVerified;

  /// Номер подтвердили прямо из гейта — оболочке пора считать игрока подтверждённым.
  final VoidCallback? onPhoneVerified;
  final DateTime Function() clock;

  @override
  State<ReservationsScreen> createState() => _ReservationsScreenState();
}

enum _Load { loading, failed, ready }

class _ReservationsScreenState extends State<ReservationsScreen> {
  _Load _state = _Load.loading;
  List<PlayerReservation> _reservations = const [];

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

  /// Форма живёт в листе снизу: раздел открывают, чтобы посмотреть свои брони, а не
  /// бронировать заново — развёрнутая форма занимала первый экран у всех.
  Future<void> _openForm() async {
    final l = L.of(context);
    final created = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (_) => NewReservationSheet(api: widget.api, clock: widget.clock),
    );
    if (created != true || !mounted) return;

    _say(l.customerReservationsCreated);
    await _refresh();
  }

  Future<void> _cancel(PlayerReservation reservation) async {
    final l = L.of(context);
    final locale = Localizations.localeOf(context).languageCode;
    final confirmed = await showDialog<bool>(
      context: context,
      // Обе кнопки называют своё действие целиком. Пара «Отменить» / «Назад» читалась как
      // два способа закрыть диалог, и промах стоил игроку брони.
      builder: (context) => AlertDialog(
        title: Text(l.customerReservationsCancelConfirm),
        // Какую именно бронь отменяем: при двух бронях безымянный вопрос ничего не значит.
        content: Text(
          '${reservation.seatName ?? l.customerReservationsNoSeat}\n'
          '${formatTimeRange(l, reservation.startsAtUtc, reservation.endsAtUtc, locale, now: widget.clock())}',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(l.customerReservationsCancelKeep),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            style: FilledButton.styleFrom(
              backgroundColor: Theme.of(context).colorScheme.error,
              foregroundColor: Theme.of(context).colorScheme.onError,
            ),
            child: Text(l.customerReservationsCancelAction),
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
      // Бронировать можно только с подтверждённым телефоном; без него кнопка не появляется,
      // а объяснение стоит на месте списка.
      floatingActionButton: widget.phoneVerified
          ? FloatingActionButton.extended(
              onPressed: _openForm,
              icon: const Icon(Icons.add),
              label: Text(l.customerReservationsCreate),
            )
          : null,
      // Потянуть вниз обновляет список — тот же жест, что на главной и в истории.
      body: RefreshIndicator(
        onRefresh: _refresh,
        child: ListView(
          padding: const EdgeInsets.all(16),
          physics: const AlwaysScrollableScrollPhysics(),
          children: [
            if (!widget.phoneVerified) ...[
              _gate(l, theme),
              const SizedBox(height: 16),
            ],
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
                          now: widget.clock(),
                          onCancel: () => _cancel(reservation),
                        ),
                      ),
                  ],
                ),
            },
          ],
        ),
      ),
    );
  }

  /// Гейт с выходом: объяснение и кнопка, а не тупик с отсылкой к администратору.
  Widget _gate(L l, ThemeData theme) => Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                l.customerReservationsGate,
                style:
                    theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
              const SizedBox(height: 8),
              OutlinedButton(
                onPressed: _verifyPhone,
                child: Text(l.customerWalletGateAction),
              ),
            ],
          ),
        ),
      );

  Future<void> _verifyPhone() async {
    final l = L.of(context);
    final confirmed = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (_) => PhoneVerificationSheet(api: widget.api),
    );
    if (confirmed != true || !mounted) return;

    _say(l.customerPhoneDone);
    widget.onPhoneVerified?.call();
  }
}

class _ReservationCard extends StatelessWidget {
  const _ReservationCard({
    required this.reservation,
    required this.now,
    required this.onCancel,
  });

  final PlayerReservation reservation;
  final DateTime now;
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
              formatTimeRange(l, reservation.startsAtUtc, reservation.endsAtUtc, locale, now: now),
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
