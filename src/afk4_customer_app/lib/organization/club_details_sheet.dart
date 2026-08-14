import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';
import '../money/money.dart';
import '../theme/app_theme.dart';
import 'opening_hours.dart';
import 'organization.dart';

/// Подробности клуба: описание зала, залы с железом, расписание на неделю и адрес.
///
/// Карточка в списке отвечает на вопросы, которые задают при беглом просмотре («где, почём,
/// открыт ли»). Этот лист — для того, кто уже присматривается: он сравнивает железо и смотрит,
/// работает ли клуб в субботу. Выбрать клуб можно прямо отсюда, не возвращаясь в список.
class ClubDetailsSheet extends StatelessWidget {
  const ClubDetailsSheet({super.key, required this.club, required this.onChoose});

  final Organization club;
  final VoidCallback onChoose;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;
    final place = club.places.isEmpty ? null : club.places.first;

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 20),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(club.name, style: theme.textTheme.headlineSmall),
            if (place != null) ...[
              const SizedBox(height: 4),
              Text(
                [place.city, place.address ?? ''].where((part) => part.isNotEmpty).join(', '),
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: theme.colorScheme.onSurfaceVariant,
                ),
              ),
            ],
            Flexible(
              child: ListView(
                shrinkWrap: true,
                padding: const EdgeInsets.only(top: 16),
                children: [
                  // Описание клуба словами владельца: то, чего не расскажут ни цена, ни адрес.
                  if (place?.description != null && place!.description!.isNotEmpty) ...[
                    Text(place.description!, style: theme.textTheme.bodyMedium),
                    const SizedBox(height: 16),
                  ],
                  if (club.pricePerHourFromMinorUnits != null)
                    _Line(
                      icon: Icons.payments_outlined,
                      text: l.customerClubPickerPriceFrom(formatMoney(
                        club.pricePerHourFromMinorUnits!,
                        club.currencyCode ?? 'TJS',
                        locale: locale,
                      )),
                      accent: true,
                    ),
                  if (place != null && place.zones.isNotEmpty) ...[
                    const SizedBox(height: 16),
                    Text(l.customerClubDetailsHalls, style: theme.textTheme.titleMedium),
                    const SizedBox(height: 8),
                    for (final zone in place.zones) _ZoneRow(zone: zone),
                  ],
                  if (place != null && place.workingHours.isNotEmpty) ...[
                    const SizedBox(height: 16),
                    Text(l.customerClubDetailsHours, style: theme.textTheme.titleMedium),
                    const SizedBox(height: 8),
                    _Schedule(days: place.workingHours),
                  ],
                ],
              ),
            ),
            const SizedBox(height: 16),
            SizedBox(
              width: double.infinity,
              height: AppTheme.primaryButtonHeight,
              child: FilledButton(onPressed: onChoose, child: Text(l.customerClubDetailsChoose)),
            ),
          ],
        ),
      ),
    );
  }
}

class _Line extends StatelessWidget {
  const _Line({required this.icon, required this.text, this.accent = false});

  final IconData icon;
  final String text;
  final bool accent;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final color = accent ? theme.colorScheme.primary : theme.colorScheme.onSurfaceVariant;

    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 16, color: color),
          const SizedBox(width: 8),
          Expanded(child: Text(text, style: theme.textTheme.bodyMedium?.copyWith(color: color))),
        ],
      ),
    );
  }
}

/// Зал: сколько мест и на чём играют. Железо — то, по чему клубы и сравнивают: «сорок мест»
/// ничего не говорит о том, пойдёт ли на них игра.
class _ZoneRow extends StatelessWidget {
  const _ZoneRow({required this.zone});

  final ClubZone zone;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(child: Text(zone.name, style: theme.textTheme.titleSmall)),
              if (zone.seatCount > 0)
                Text(
                  l.customerClubDetailsHallSeats(zone.seatCount),
                  style: theme.textTheme.labelMedium
                      ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                ),
            ],
          ),
          if (zone.hardwareSummary != null && zone.hardwareSummary!.isNotEmpty)
            Text(
              zone.hardwareSummary!,
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.primary),
            ),
        ],
      ),
    );
  }
}

/// Неделя целиком: сегодняшний день выделен — с него и читают.
class _Schedule extends StatelessWidget {
  const _Schedule({required this.days});

  final List<OpeningDay> days;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final today = DateTime.now().weekday;

    return Column(
      children: [
        for (final day in days)
          Padding(
            padding: const EdgeInsets.only(bottom: 4),
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    _weekdayName(l, day.dayOfWeek),
                    style: theme.textTheme.bodyMedium?.copyWith(
                      color: day.dayOfWeek == today
                          ? theme.colorScheme.onSurface
                          : theme.colorScheme.onSurfaceVariant,
                      fontWeight: day.dayOfWeek == today ? FontWeight.w600 : null,
                    ),
                  ),
                ),
                Text(
                  day.isClosed
                      ? l.customerClubDetailsDayOff
                      : '${day.openTime ?? '—'} – ${day.closeTime ?? '—'}',
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: day.isClosed
                        ? theme.colorScheme.onSurfaceVariant
                        : theme.colorScheme.onSurface,
                    fontWeight: day.dayOfWeek == today ? FontWeight.w600 : null,
                  ),
                ),
              ],
            ),
          ),
      ],
    );
  }

  static String _weekdayName(L l, int dayOfWeek) => switch (dayOfWeek) {
        1 => l.customerWeekday1,
        2 => l.customerWeekday2,
        3 => l.customerWeekday3,
        4 => l.customerWeekday4,
        5 => l.customerWeekday5,
        6 => l.customerWeekday6,
        _ => l.customerWeekday7,
      };
}
