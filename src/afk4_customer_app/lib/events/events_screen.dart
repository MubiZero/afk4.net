import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../format/date_time.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import '../theme/app_theme.dart';

/// События клуба: турнир по пятницам, ночь игры, чемпионат зала.
///
/// Клуб заполняет ими будни, поэтому здесь ходят деньги — взнос за участие. Всё, что игрок
/// должен понять до нажатия: когда, во что играют, сколько стоит, сколько мест осталось и
/// вернутся ли деньги, если передумает.
class EventsScreen extends StatefulWidget {
  const EventsScreen({
    super.key,
    required this.api,
    required this.branchId,
    this.clock = DateTime.now,
  });

  final PlayerApiClient api;
  final String branchId;
  final DateTime Function() clock;

  @override
  State<EventsScreen> createState() => _EventsScreenState();
}

class _EventsScreenState extends State<EventsScreen> {
  List<ClubEvent>? _events;
  bool _failed = false;
  String? _busyId;
  String? _error;

  /// Взнос списался или вернулся — главной пора перечитать баланс, а не оставлять прежний.
  bool _walletChanged = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final events = await widget.api.getEvents(widget.branchId);
      if (!mounted) return;
      setState(() {
        _events = events;
        _failed = false;
      });
    } on PlayerApiException {
      if (!mounted) return;
      setState(() => _failed = _events == null);
    }
  }

  Future<void> _register(ClubEvent event) async {
    final l = L.of(context);
    final locale = Localizations.localeOf(context).languageCode;
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(l.customerEventsConfirmTitle(event.title)),
        content: Text(event.isFree
            ? l.customerEventsConfirmFree
            : l.customerEventsConfirmPaid(
                formatMoney(event.entryFeeMinorUnits, event.currencyCode, locale: locale))),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: Text(l.customerEventsDismiss),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: Text(l.customerEventsRegister),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;

    await _run(event, () => widget.api.registerForEvent(event.tournamentId),
        toast: l.customerEventsRegisteredToast(event.title));
  }

  Future<void> _cancel(ClubEvent event) async {
    final l = L.of(context);
    final locale = Localizations.localeOf(context).languageCode;
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(l.customerEventsCancelConfirmTitle),
        content: Text(event.isFree
            ? l.customerEventsCancelConfirmFree
            : l.customerEventsCancelConfirmPaid(
                formatMoney(event.entryFeeMinorUnits, event.currencyCode, locale: locale))),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: Text(l.customerEventsDismiss),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: Text(l.customerEventsCancel),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;

    await _run(event, () => widget.api.cancelEventRegistration(event.tournamentId),
        toast: l.customerEventsCancelledToast(event.title));
  }

  /// Общий ход записи и снятия: обе кнопки трогают деньги, обе обязаны вернуться в рабочее
  /// состояние при любом сбое — висящая кнопка читается как зависшее списание.
  Future<void> _run(ClubEvent event, Future<ClubEvent> Function() action, {required String toast}) async {
    final l = L.of(context);
    setState(() {
      _busyId = event.tournamentId;
      _error = null;
    });

    try {
      await action();
      if (!mounted) return;
      unawaited(HapticFeedback.lightImpact());
      setState(() {
        _busyId = null;
        _walletChanged = _walletChanged || !event.isFree;
      });
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(toast)));
      await _load();
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _busyId = null;
        _error = switch (error.message) {
          'insufficient_funds' => l.customerEventsErrFunds,
          'tournament_full' => l.customerEventsErrFull,
          'tournament_already_started' => l.customerEventsErrStarted,
          'tournament_cancelled' => l.customerEventsErrCancelled,
          _ => l.customerEventsErrGeneric,
        };
      });
      // Отказ почти всегда значит, что список устарел: мест не осталось или событие отменили.
      await _load();
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _busyId = null;
        _error = l.customerEventsErrGeneric;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);

    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) Navigator.of(context).pop(_walletChanged);
      },
      child: Scaffold(
        appBar: AppBar(title: Text(l.customerEventsTitle)),
        body: RefreshIndicator(onRefresh: _load, child: _body(l)),
      ),
    );
  }

  Widget _body(L l) {
    final theme = Theme.of(context);
    final events = _events;

    if (events == null) {
      return _failed
          ? ListView(
              padding: const EdgeInsets.all(24),
              children: [
                Text(l.customerEventsLoadError, style: TextStyle(color: theme.colorScheme.error)),
              ],
            )
          : const Center(child: CircularProgressIndicator());
    }

    final locale = Localizations.localeOf(context).languageCode;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        if (_error != null) ...[
          Text(_error!, style: TextStyle(color: theme.colorScheme.error)),
          const SizedBox(height: 16),
        ],
        if (events.isEmpty)
          Text(
            l.customerEventsNone,
            style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          )
        else
          for (final event in events)
            Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: EventCard(
                event: event,
                locale: locale,
                busy: _busyId == event.tournamentId,
                clock: widget.clock,
                onRegister: () => _register(event),
                onCancel: () => _cancel(event),
              ),
            ),
      ],
    );
  }
}

/// Карточка события. Отдельный виджет — ради тестов: правила «что показать» проверяются на
/// ней, а не через весь экран с сетью.
class EventCard extends StatelessWidget {
  const EventCard({
    super.key,
    required this.event,
    required this.locale,
    required this.onRegister,
    required this.onCancel,
    this.busy = false,
    this.clock = DateTime.now,
  });

  final ClubEvent event;
  final String locale;
  final VoidCallback onRegister;
  final VoidCallback onCancel;
  final bool busy;
  final DateTime Function() clock;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final spots = event.freeSpots;
    final full = spots == 0 && !event.isRegistered;

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: theme.colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(AppTheme.radiusCard),
        border: Border.all(color: theme.colorScheme.outline),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(event.title, style: theme.textTheme.titleMedium),
          const SizedBox(height: 4),
          Text(
            formatDateTime(l, event.startsAtUtc, locale, now: clock()),
            style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.primary),
          ),
          if (event.discipline.isNotEmpty) ...[
            const SizedBox(height: 4),
            Text(
              event.discipline,
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
          ],
          if (event.description.isNotEmpty) ...[
            const SizedBox(height: 8),
            Text(event.description, style: theme.textTheme.bodyMedium),
          ],
          const SizedBox(height: 12),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              _Tag(
                text: event.isFree
                    ? l.customerEventsFree
                    : l.customerEventsFee(
                        formatMoney(event.entryFeeMinorUnits, event.currencyCode, locale: locale)),
                accent: !event.isFree,
              ),
              // «Осталось N мест» — только когда потолок есть: у события без ограничения такая
              // строка была бы выдумкой.
              if (spots != null)
                _Tag(text: spots == 0 ? l.customerEventsNoSpots : l.customerEventsSpotsLeft(spots))
              else if (event.registeredCount > 0)
                _Tag(text: l.customerEventsGoing(event.registeredCount)),
            ],
          ),
          const SizedBox(height: 12),
          if (event.isCancelled)
            // Отменённое событие остаётся в списке у того, кто на него шёл, — вместе с причиной.
            // Молча убрать его значит оставить человека собираться на вечер, которого не будет.
            Text(
              event.cancelReason.isEmpty
                  ? l.customerEventsCancelledByClub
                  : '${l.customerEventsCancelledByClub}: ${event.cancelReason}',
              style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.error),
            )
          else if (event.isRegistered)
            Row(
              children: [
                Icon(Icons.check_circle_outline, size: 18, color: theme.colorScheme.primary),
                const SizedBox(width: 6),
                Expanded(
                  child: Text(
                    l.customerEventsRegistered,
                    style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.primary),
                  ),
                ),
                TextButton(
                  onPressed: busy ? null : onCancel,
                  child: Text(l.customerEventsCancel),
                ),
              ],
            )
          else
            SizedBox(
              width: double.infinity,
              height: AppTheme.primaryButtonHeight,
              child: FilledButton(
                onPressed: busy || full ? null : onRegister,
                child: Text(full ? l.customerEventsNoSpots : l.customerEventsRegister),
              ),
            ),
        ],
      ),
    );
  }
}

class _Tag extends StatelessWidget {
  const _Tag({required this.text, this.accent = false});

  final String text;
  final bool accent;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final color = accent ? theme.colorScheme.primary : theme.colorScheme.onSurfaceVariant;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: theme.colorScheme.surface,
        borderRadius: BorderRadius.circular(AppTheme.radiusControl),
        border: Border.all(color: theme.colorScheme.outline),
      ),
      child: Text(text, style: theme.textTheme.labelMedium?.copyWith(color: color)),
    );
  }
}
