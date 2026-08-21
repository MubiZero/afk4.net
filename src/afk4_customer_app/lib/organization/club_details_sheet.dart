import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';
import '../money/money.dart';
import '../theme/app_theme.dart';
import 'opening_hours.dart';
import 'organization.dart';

/// Подробности клуба: описание, залы сети с их адресами, зонами и расписанием на неделю.
///
/// Карточка в списке отвечает на вопросы, которые задают при беглом просмотре («где, почём,
/// открыт ли»). Этот лист — для того, кто уже присматривается: он сравнивает железо и смотрит,
/// работает ли клуб в субботу. Выбрать клуб можно прямо отсюда, не возвращаясь в список.
///
/// Лист открывается до входа, когда зал ещё не выбран и выбирать его рано: человек только
/// решает, ехать ли вообще. Поэтому сеть из нескольких залов показывается сетью — со своим
/// адресом, часами и зонами у каждого зала. Раньше здесь стоял первый зал сети без имени, и
/// у сети из одного зала это случайно совпадало с правдой, а у остальных — врало: игрок читал
/// адрес одного зала, часы другого и ехал по тому, что запомнил.
class ClubDetailsSheet extends StatelessWidget {
  const ClubDetailsSheet({
    super.key,
    required this.club,
    required this.onChoose,
    this.clock = DateTime.now,
  });

  final Organization club;
  final VoidCallback onChoose;

  /// Часы залов сверяются с этим временем. Отдельный параметр — ради тестов: «открыто
  /// сейчас» иначе проверялось бы в час, когда идёт прогон.
  final DateTime Function() clock;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;
    final halls = club.places;
    // Единственный зал сети — это и есть клуб. Список из одной строки и вопрос «в каком
    // зале» над ним были бы выбором без выбора, поэтому такой клуб читается как раньше.
    final only = halls.length == 1 ? halls.single : null;
    final subtitle = _subtitle(l, halls, only);

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 20),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(club.name, style: theme.textTheme.headlineSmall),
            if (subtitle.isNotEmpty) ...[
              const SizedBox(height: 4),
              Text(
                subtitle,
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
                  // У сети оно своё у каждого зала и живёт внутри зала, а не над всеми сразу.
                  if (only?.description != null && only!.description!.isNotEmpty) ...[
                    Text(only.description!, style: theme.textTheme.bodyMedium),
                    const SizedBox(height: 16),
                  ],
                  // Цена — единственное, что у сети действительно общее: тарифы заводятся на
                  // сеть, и «от» считается по ним.
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
                  if (only != null) _HallDetails(hall: only),
                  if (halls.length > 1) ...[
                    const SizedBox(height: 16),
                    Text(l.customerClubDetailsHalls, style: theme.textTheme.titleMedium),
                    const SizedBox(height: 8),
                    for (final hall in halls) _HallTile(hall: hall, clock: clock),
                  ],
                  // Клуб без единого зала бывает у только что подключившейся сети. Пустое
                  // место под названием читается как сбой загрузки, а не как «ещё не заполнил».
                  if (halls.isEmpty) ...[
                    const SizedBox(height: 16),
                    _Missing(l.customerClubDetailsNoHalls),
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

  /// Строка под названием клуба: у одного зала — его адрес, у сети — сколько залов и в каких
  /// городах. Адрес одного зала на этом месте выдавал бы себя за адрес всей сети.
  String _subtitle(L l, List<ClubPlace> halls, ClubPlace? only) {
    if (only != null) return only.fullAddress;
    if (halls.isEmpty) return '';

    final cities = <String>[];
    for (final hall in halls) {
      if (hall.city.isNotEmpty && !cities.contains(hall.city)) cities.add(hall.city);
    }
    return l.customerClubDetailsHallsIn(halls.length, cities.join(', '));
  }
}

/// Зал сети свёрнутой строкой: название, адрес и ответ на «открыт ли он сейчас». Зоны и
/// расписание на неделю разворачиваются по нажатию — это то, за чем сюда приходят вторым
/// вопросом, а первым сравнивают залы между собой.
class _HallTile extends StatelessWidget {
  const _HallTile({required this.hall, required this.clock});

  final ClubPlace hall;
  final DateTime Function() clock;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final radius = BorderRadius.circular(AppTheme.radiusCard);
    final address = hall.addressUnderName;
    final now = openingNowLabel(l, hall.workingHours, clock());

    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: DecoratedBox(
        decoration: BoxDecoration(
          borderRadius: radius,
          border: Border.all(color: theme.colorScheme.outline),
        ),
        child: ClipRRect(
          borderRadius: radius,
          child: ExpansionTile(
            // Рамка у карточки уже есть — линии самого ExpansionTile её бы удвоили.
            shape: const Border(),
            collapsedShape: const Border(),
            tilePadding: const EdgeInsets.symmetric(horizontal: 12),
            childrenPadding: const EdgeInsets.fromLTRB(12, 0, 12, 12),
            expandedCrossAxisAlignment: CrossAxisAlignment.start,
            title: Text(hall.displayName, style: theme.textTheme.titleSmall),
            subtitle: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                if (address.isNotEmpty)
                  Text(
                    address,
                    style: theme.textTheme.bodySmall
                        ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                  ),
                if (now != null)
                  Text(
                    now.text,
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: now.open
                          ? theme.colorScheme.primary
                          : theme.colorScheme.onSurfaceVariant,
                    ),
                  ),
              ],
            ),
            children: [
              if (hall.description != null && hall.description!.isNotEmpty)
                Align(
                  alignment: Alignment.centerLeft,
                  child: Text(hall.description!, style: theme.textTheme.bodyMedium),
                ),
              _HallDetails(hall: hall),
            ],
          ),
        ),
      ),
    );
  }
}

/// Зоны и расписание одного зала. Пустые списки объясняются словами: незаполненная витрина
/// клуба и сбой загрузки не должны выглядеть одинаково.
class _HallDetails extends StatelessWidget {
  const _HallDetails({required this.hall});

  final ClubPlace hall;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        const SizedBox(height: 16),
        Text(l.customerClubDetailsZones, style: theme.textTheme.titleMedium),
        const SizedBox(height: 8),
        if (hall.zones.isEmpty)
          _Missing(l.customerClubDetailsZonesUnknown)
        else
          for (final zone in hall.zones) _ZoneRow(zone: zone),
        const SizedBox(height: 16),
        Text(l.customerClubDetailsHours, style: theme.textTheme.titleMedium),
        const SizedBox(height: 8),
        if (hall.workingHours.isEmpty)
          _Missing(l.customerClubDetailsHoursUnknown)
        else
          _Schedule(days: hall.workingHours),
      ],
    );
  }
}

/// То, чего клуб про себя не рассказал. Отдельный вид, а не обычный текст: игрок должен
/// видеть, что это пробел у клуба, а не сломавшийся экран.
class _Missing extends StatelessWidget {
  const _Missing(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Text(
      text,
      style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
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

/// Зона зала: сколько мест и на чём играют. Железо — то, по чему клубы и сравнивают: «сорок
/// мест» ничего не говорит о том, пойдёт ли на них игра.
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
                  l.customerClubDetailsZoneSeats(zone.seatCount),
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
