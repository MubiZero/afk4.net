import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';
import '../theme/app_theme.dart';
import 'organization.dart';

/// Зал сети, в который придёт игрок.
///
/// Спрашивается один раз и только до первого действия в клубе: счёт открывается бронью или
/// пополнением, и у сети с несколькими залами сервер не гадает, в каком именно, — иначе
/// человек пришёл бы в один зал, а его кошелёк и история оказались бы в другом. У игрока со
/// счётом зал уже записан, и названный заново его не переписывает: там спрашивать нечего.
///
/// Залы берутся из каталога клубов (`GET /api/public/organizations`) — того же ответа, из
/// которого игрок выбирал сам клуб. Отдельного запроса за ними не нужно.
class BranchChoice {
  const BranchChoice({this.halls = const [], this.chosenId, this.onChosen});

  /// Залы, из которых есть что выбрать. Пусто — вопроса нет: либо счёт уже открыт, либо сеть
  /// своих залов не назвала. Тогда всё работает как раньше — зал в запросе не упоминается.
  final List<ClubPlace> halls;

  /// Что ответил игрок. null — ещё не отвечал.
  final String? chosenId;

  /// Куда сообщить ответ. Зал нужен и броням, и пополнению, поэтому помнит его оболочка:
  /// спрашивать одно и то же в каждом листе — плохая цена за один вопрос.
  final ValueChanged<String>? onChosen;

  /// Зал, который поедет на сервер. Единственный зал сети — сам себе ответ, спрашивать про
  /// него нечего. null — назвать нечего, запрос уходит как раньше.
  String? get branchId => chosenId ?? (halls.length == 1 ? halls.single.branchId : null);

  /// Спрашивать ли игрока. Один зал в сети — не выбор, а данность.
  bool get asks => halls.length > 1;

  /// Вопрос задан и ещё не отвечен: действие сейчас обернулось бы отказом сервера.
  bool get unanswered => asks && chosenId == null;
}

/// Выбор зала: название и адрес каждого — по ним игрок и узнаёт своё место.
///
/// Ничего не рисует, когда выбирать не из чего: пустой заголовок «В какой зал вы придёте?»
/// над единственным залом — вопрос без вопроса.
class BranchPicker extends StatelessWidget {
  const BranchPicker({super.key, required this.choice});

  final BranchChoice choice;

  @override
  Widget build(BuildContext context) {
    if (!choice.asks) return const SizedBox.shrink();

    final l = L.of(context);
    final theme = Theme.of(context);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(l.customerBranchTitle, style: theme.textTheme.titleSmall),
        const SizedBox(height: 4),
        Text(
          l.customerBranchHint,
          style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
        ),
        const SizedBox(height: 8),
        for (final hall in choice.halls)
          Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: _HallOption(
              hall: hall,
              selected: hall.branchId == choice.chosenId,
              onSelected: () => choice.onChosen?.call(hall.branchId),
            ),
          ),
      ],
    );
  }
}

class _HallOption extends StatelessWidget {
  const _HallOption({required this.hall, required this.selected, required this.onSelected});

  final ClubPlace hall;
  final bool selected;
  final VoidCallback onSelected;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final radius = BorderRadius.circular(AppTheme.radiusCard);

    // Название зала — то, как его зовут в самом клубе. Пусто оно бывает у клуба, который его
    // не задал: тогда за название работает город, а безымянной строки на экране не остаётся.
    final title = hall.name.isNotEmpty ? hall.name : hall.city;
    final address = [
      if (hall.name.isNotEmpty) hall.city,
      hall.address ?? '',
    ].where((part) => part.isNotEmpty).join(', ');

    return Semantics(
      selected: selected,
      child: Material(
        color: selected
            ? theme.colorScheme.primary.withValues(alpha: 0.12)
            : Colors.transparent,
        borderRadius: radius,
        child: InkWell(
          onTap: onSelected,
          borderRadius: radius,
          child: Container(
            // Палец, а не курсор: строка обязана держать минимальную цель касания даже с
            // одной строкой текста.
            constraints: const BoxConstraints(minHeight: 48),
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            decoration: BoxDecoration(
              borderRadius: radius,
              border: Border.all(
                color: selected ? theme.colorScheme.primary : theme.colorScheme.outline,
              ),
            ),
            child: Row(
              children: [
                Icon(
                  selected ? Icons.check_circle : Icons.circle_outlined,
                  size: 20,
                  color: selected
                      ? theme.colorScheme.primary
                      : theme.colorScheme.onSurfaceVariant,
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(title, style: theme.textTheme.titleSmall),
                      if (address.isNotEmpty)
                        Text(
                          address,
                          style: theme.textTheme.bodySmall
                              ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                        ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
