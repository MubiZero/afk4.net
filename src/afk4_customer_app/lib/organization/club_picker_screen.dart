import 'dart:async';

import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';
import '../theme/app_theme.dart';
import '../theme/brand_mark.dart';
import 'organization.dart';
import 'organization_directory.dart';

/// Выбор клуба — первый экран приложения. У мобильной сборки нет поддомена, из которого веб
/// берёт организацию, а войти без неё нельзя: игрок опознаётся парой организация + телефон.
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

class _ClubPickerScreenState extends State<ClubPickerScreen> {
  /// Пауза перед запросом: набор «Аре…» иначе шлёт три запроса вместо одного.
  static const Duration _typingPause = Duration(milliseconds: 300);

  _Load _load = const _Loading();
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

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const SizedBox(height: 12),
              const BrandMark(),
              const SizedBox(height: 20),
              Text(l.customerClubPickerTitle, style: Theme.of(context).textTheme.headlineLarge),
              const SizedBox(height: 6),
              Text(
                l.customerClubPickerSubtitle,
                style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                      color: Theme.of(context).colorScheme.onSurfaceVariant,
                    ),
              ),
              const SizedBox(height: 20),
              TextField(
                decoration: InputDecoration(
                  labelText: l.customerClubPickerSearch,
                  prefixIcon: const Icon(Icons.search),
                ),
                textInputAction: TextInputAction.search,
                onChanged: _onQueryChanged,
              ),
              const SizedBox(height: 16),
              Expanded(child: _buildBody(l)),
            ],
          ),
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
      // Клуб выбирают глазами, а не читают списком: каждый — отдельная плитка со знаком.
      // Строки через разделитель выглядели как список настроек, а не как витрина.
      _Ready(clubs: final clubs) => ListView.separated(
          itemCount: clubs.length,
          separatorBuilder: (_, _) => const SizedBox(height: 12),
          itemBuilder: (context, index) => _ClubTile(
            club: clubs[index],
            onTap: () => widget.onSelected(clubs[index]),
          ),
        ),
    };
  }
}

/// Плитка клуба: знак, название и стрелка.
class _ClubTile extends StatelessWidget {
  const _ClubTile({required this.club, required this.onTap});

  final Organization club;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Material(
      color: theme.colorScheme.surfaceContainerHighest,
      borderRadius: BorderRadius.circular(AppTheme.radiusCard),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppTheme.radiusCard),
        child: Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppTheme.radiusCard),
            border: Border.all(color: theme.colorScheme.outline),
          ),
          child: Row(
            children: [
              // Логотипа может не быть — тогда первая буква названия вместо пустой дыры.
              Container(
                width: 48,
                height: 48,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(14),
                  color: AppTheme.emerald.withValues(alpha: 0.16),
                  image: club.logoUrl == null
                      ? null
                      : DecorationImage(image: NetworkImage(club.logoUrl!), fit: BoxFit.cover),
                ),
                child: club.logoUrl != null
                    ? null
                    : Text(
                        club.name.characters.first.toUpperCase(),
                        style: theme.textTheme.titleLarge?.copyWith(color: AppTheme.emeraldBright),
                      ),
              ),
              const SizedBox(width: 14),
              Expanded(child: Text(club.name, style: theme.textTheme.titleMedium)),
              Icon(Icons.chevron_right, color: theme.colorScheme.onSurfaceVariant),
            ],
          ),
        ),
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
