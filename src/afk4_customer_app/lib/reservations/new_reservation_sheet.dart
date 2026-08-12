import 'package:flutter/material.dart';

import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import 'date_time_field.dart';
import 'reservations_screen.dart';

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
      await widget.api.createReservation(startsAtUtc: _startsAt!, endsAtUtc: _endsAt!);
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
              onChanged: (value) => setState(() {
                _startsAt = value;
                _problem = null;
              }),
            ),
            const SizedBox(height: 12),
            DateTimeField(
              label: l.customerReservationsEnd,
              value: _endsAt,
              firstAllowed: _startsAt ?? widget.clock(),
              onChanged: (value) => setState(() {
                _endsAt = value;
                _problem = null;
              }),
            ),
            if (_problem != null) ...[
              const SizedBox(height: 8),
              Text(_problem!, style: TextStyle(color: theme.colorScheme.error)),
            ],
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
