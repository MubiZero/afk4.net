import 'package:flutter/material.dart';

import '../format/date_time.dart';
import '../l10n/app_localizations.dart';

/// Выбор даты и времени одним нажатием: сначала день, следом час.
///
/// На телефоне это системные диалоги, а не поле ввода: набирать дату руками неудобно и
/// легко ошибиться форматом. Прошедшие дни недоступны — бронь на вчера смысла не имеет.
class DateTimeField extends StatelessWidget {
  const DateTimeField({
    super.key,
    required this.label,
    required this.value,
    required this.firstAllowed,
    required this.onChanged,
  });

  final String label;
  final DateTime? value;

  /// Раньше этого момента выбирать нечего.
  final DateTime firstAllowed;
  final ValueChanged<DateTime> onChanged;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;
    final chosen = value;

    return InkWell(
      onTap: () => _pick(context),
      child: InputDecorator(
        decoration: InputDecoration(labelText: label),
        child: Text(
          chosen == null ? l.customerReservationsNotChosen : formatDateTime(chosen, locale),
          style: theme.textTheme.bodyLarge,
        ),
      ),
    );
  }

  /// Что предложить, когда игрок ещё ничего не выбрал: ближайший целый час, а не текущую
  /// минуту. Бронь «начиная прямо сейчас» сервер всё равно не примет — предлагать её значит
  /// готовить отказ.
  DateTime get _suggested {
    final rounded = DateTime(
      firstAllowed.year,
      firstAllowed.month,
      firstAllowed.day,
      firstAllowed.hour,
    );
    return rounded.add(const Duration(hours: 1));
  }

  Future<void> _pick(BuildContext context) async {
    final initial = value ?? _suggested;
    final day = await showDatePicker(
      context: context,
      initialDate: initial.isBefore(firstAllowed) ? firstAllowed : initial,
      firstDate: DateTime(firstAllowed.year, firstAllowed.month, firstAllowed.day),
      lastDate: firstAllowed.add(const Duration(days: 365)),
    );
    if (day == null || !context.mounted) return;

    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(initial),
    );
    if (time == null) return;

    onChanged(DateTime(day.year, day.month, day.day, time.hour, time.minute));
  }
}
