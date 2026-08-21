import 'dart:async';

import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import '../theme/brand_mark.dart';
import '../reviews/club_reviews_sheet.dart';
import 'club_card.dart';
import 'club_details_sheet.dart';
import 'club_map.dart';
import 'organization.dart';
import 'organization_directory.dart';

/// Выбор клуба — первый экран приложения. У мобильной сборки нет поддомена, из которого веб
/// берёт организацию, а войти без неё нельзя: игрок опознаётся парой организация + телефон.
///
/// Это витрина, а не список настроек: клуб выбирают по тому, где он, сколько стоит час и как
/// выглядит зал. Списком удобно сравнивать, картой — понять, что рядом; поэтому и то, и другое.
class ClubPickerScreen extends StatefulWidget {
  const ClubPickerScreen({
    super.key,
    required this.directory,
    required this.onSelected,
    this.myClubs = const [],
    this.selectedOrganizationId,
  });

  final OrganizationDirectory directory;
  final ValueChanged<Organization> onSelected;

  /// Клубы, в которых у игрока уже есть счёт. Они идут первыми и со своими деньгами: аккаунт
  /// один на всю сеть, а кошелёк у каждого клуба свой, и это первое, что надо видеть.
  final List<MyClub> myClubs;

  /// Клуб, открытый прямо сейчас. Нужен, чтобы не звать переходить туда, где игрок уже есть.
  final String? selectedOrganizationId;

  @override
  State<ClubPickerScreen> createState() => _ClubPickerScreenState();
}

sealed class _Load {
  const _Load();
}

class _Loading extends _Load {
  const _Loading();
}

class _Failed extends _Load {
  const _Failed();
}

class _Ready extends _Load {
  const _Ready(this.clubs);

  final List<Organization> clubs;
}

enum _View { list, map }

class _ClubPickerScreenState extends State<ClubPickerScreen> {
  /// Пауза перед запросом: набор «Аре…» иначе шлёт три запроса вместо одного.
  static const Duration _typingPause = Duration(milliseconds: 300);

  _Load _load = const _Loading();
  _View _view = _View.list;
  Timer? _debounce;
  String _query = '';
  int _requestSeq = 0;

  @override
  void initState() {
    super.initState();
    _fetch();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    super.dispose();
  }

  Future<void> _fetch() async {
    // Порядковый номер, а не флаг «идёт загрузка»: медленный ответ на «а» не должен
    // перетереть быстрый ответ на «арена», набранное следом.
    final seq = ++_requestSeq;
    setState(() => _load = const _Loading());
    try {
      final clubs = await widget.directory.search(query: _query);
      if (!mounted || seq != _requestSeq) return;
      setState(() => _load = _Ready(clubs));
    } on OrganizationDirectoryException {
      if (!mounted || seq != _requestSeq) return;
      setState(() => _load = const _Failed());
    }
  }

  void _onQueryChanged(String value) {
    _query = value;
    _debounce?.cancel();
    _debounce = Timer(_typingPause, _fetch);
  }

  void _openReviews(Organization club) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (_) => ClubReviewsSheet(directory: widget.directory, club: club),
    );
  }

  void _openDetails(Organization club) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (sheetContext) => ClubDetailsSheet(
        club: club,
        onChoose: () {
          Navigator.of(sheetContext).pop();
          widget.onSelected(club);
        },
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Scaffold(
      body: SafeArea(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const BrandMark(),
                  const SizedBox(height: 18),
                  Text(l.customerClubPickerTitle, style: theme.textTheme.headlineMedium),
                  const SizedBox(height: 4),
                  Text(
                    l.customerClubPickerSubtitle,
                    style: theme.textTheme.bodyMedium?.copyWith(
                      color: theme.colorScheme.onSurfaceVariant,
                    ),
                  ),
                  const SizedBox(height: 16),
                  TextField(
                    decoration: InputDecoration(
                      labelText: l.customerClubPickerSearch,
                      prefixIcon: const Icon(Icons.search),
                    ),
                    textInputAction: TextInputAction.search,
                    onChanged: _onQueryChanged,
                  ),
                  const SizedBox(height: 12),
                  // Список и карта — два взгляда на один и тот же каталог: поиск сверху
                  // относится к обоим, поэтому переключатель стоит под ним, а не над.
                  SegmentedButton<_View>(
                    segments: [
                      ButtonSegment(
                        value: _View.list,
                        icon: const Icon(Icons.view_agenda_outlined),
                        label: Text(l.customerClubPickerTabList),
                      ),
                      ButtonSegment(
                        value: _View.map,
                        icon: const Icon(Icons.map_outlined),
                        label: Text(l.customerClubPickerTabMap),
                      ),
                    ],
                    selected: {_view},
                    showSelectedIcon: false,
                    onSelectionChanged: (selection) => setState(() => _view = selection.first),
                  ),
                  const SizedBox(height: 12),
                ],
              ),
            ),
            Expanded(child: _buildBody(l)),
          ],
        ),
      ),
    );
  }

  /// «Ваши клубы» — свои заведения первыми и с деньгами каждого. Показывается только когда
  /// игрок не ищет: в результатах поиска отдельный список поверх найденного сбивает с толку.
  List<Widget> _myClubsSection(L l, List<Organization> catalogue) {
    if (widget.myClubs.isEmpty || _query.trim().isNotEmpty) return const [];

    final rows = <Widget>[];
    for (final mine in widget.myClubs) {
      final club = catalogue
          .where((candidate) => candidate.organizationId == mine.organizationId)
          .firstOrNull;
      // Клуба нет в каталоге — он закрылся или снялся с витрины. Строка, ведущая в никуда,
      // хуже её отсутствия.
      if (club == null) continue;
      rows.add(_MyClubRow(
        club: mine,
        here: mine.organizationId == widget.selectedOrganizationId,
        onOpen: () => widget.onSelected(club),
      ));
    }
    if (rows.isEmpty) return const [];

    return [
      _SectionTitle(l.customerClubsMine),
      ...rows,
      const SizedBox(height: 8),
      _SectionTitle(l.customerClubsAll),
    ];
  }

  Widget _buildBody(L l) {
    return switch (_load) {
      _Loading() => const Center(child: CircularProgressIndicator()),
      _Failed() => _Message(
          text: l.customerClubPickerError,
          actionLabel: l.customerCommonRetry,
          onAction: _fetch,
        ),
      _Ready(clubs: final clubs) when clubs.isEmpty => _Message(text: l.customerClubPickerEmpty),
      _Ready(clubs: final clubs) => switch (_view) {
          _View.list => ListView(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
              children: [
                ..._myClubsSection(l, clubs),
                for (final club in clubs) ...[
                  ClubCard(
                    club: club,
                    onTap: () => widget.onSelected(club),
                    onOpenReviews: () => _openReviews(club),
                    onOpenDetails: () => _openDetails(club),
                  ),
                  const SizedBox(height: 14),
                ],
              ],
            ),
          _View.map => Padding(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
              child: ClubMap(clubs: clubs, onSelected: widget.onSelected),
            ),
        },
    };
  }
}

/// Заголовок группы в списке клубов.
class _SectionTitle extends StatelessWidget {
  const _SectionTitle(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 10),
        child: Text(text, style: Theme.of(context).textTheme.titleSmall),
      );
}

/// Свой клуб строкой: название, остаток кошелька и переход. Придержанное показывается
/// отдельно — иначе игрок не поймёт, почему остаток меньше, чем он помнит.
class _MyClubRow extends StatelessWidget {
  const _MyClubRow({required this.club, required this.here, required this.onOpen});

  final MyClub club;
  final bool here;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;

    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      child: ListTile(
        title: Text(club.organizationName),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(formatMoney(club.walletBalance.minorUnits, club.walletBalance.currencyCode,
                locale: locale)),
            if (club.heldBalance.minorUnits > 0)
              Text(
                '${l.customerWalletHeld}: '
                '${formatMoney(club.heldBalance.minorUnits, club.heldBalance.currencyCode, locale: locale)}',
                style: theme.textTheme.bodySmall
                    ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
          ],
        ),
        trailing: here
            ? Text(l.customerClubsHere,
                style: theme.textTheme.labelLarge?.copyWith(color: theme.colorScheme.primary))
            : TextButton(onPressed: onOpen, child: Text(l.customerClubsOpen)),
        // Нажимается и текущий клуб — это и есть дорога назад для того, кто передумал
        // переходить.
        onTap: onOpen,
      ),
    );
  }
}

class _Message extends StatelessWidget {
  const _Message({required this.text, this.actionLabel, this.onAction});

  final String text;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(text, textAlign: TextAlign.center),
          if (actionLabel != null) ...[
            const SizedBox(height: 12),
            FilledButton(onPressed: onAction, child: Text(actionLabel!)),
          ],
        ],
      ),
    );
  }
}
