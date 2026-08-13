import 'dart:async';

import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';
import '../theme/brand_mark.dart';
import '../reviews/club_reviews_sheet.dart';
import 'club_card.dart';
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
  });

  final OrganizationDirectory directory;
  final ValueChanged<Organization> onSelected;

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
          _View.list => ListView.separated(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
              itemCount: clubs.length,
              separatorBuilder: (_, _) => const SizedBox(height: 14),
              itemBuilder: (context, index) => ClubCard(
                club: clubs[index],
                onTap: () => widget.onSelected(clubs[index]),
                onOpenReviews: () => _openReviews(clubs[index]),
              ),
            ),
          _View.map => Padding(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
              child: ClubMap(clubs: clubs, onSelected: widget.onSelected),
            ),
        },
    };
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
