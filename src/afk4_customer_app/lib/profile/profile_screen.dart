import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';

/// Профиль: кто вошёл, на каком языке говорить, и выходы — из аккаунта и из клуба.
class ProfileScreen extends StatefulWidget {
  const ProfileScreen({
    super.key,
    required this.api,
    required this.onSignOut,
    required this.onChangeClub,
    required this.onLocaleChanged,
  });

  final PlayerApiClient api;
  final VoidCallback onSignOut;
  final VoidCallback onChangeClub;
  final ValueChanged<Locale> onLocaleChanged;

  @override
  State<ProfileScreen> createState() => _ProfileScreenState();
}

enum _Load { loading, failed, ready }

class _ProfileScreenState extends State<ProfileScreen> {
  _Load _state = _Load.loading;
  PlayerProfile? _profile;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _state = _Load.loading);
    try {
      final profile = await widget.api.getProfile();
      if (!mounted) return;
      setState(() {
        _profile = profile;
        _state = _Load.ready;
      });
    } on PlayerApiException {
      if (!mounted) return;
      // Веб оставлял на экране вечный скелет: непонятно, грузится или сломалось.
      setState(() => _state = _Load.failed);
    }
  }

  /// Язык применяется сразу, не дожидаясь сервера: это настройка интерфейса, и заминка
  /// в сети не повод показывать игроку чужой язык. На сервер он уходит как предпочтение —
  /// им пользуются письма и уведомления.
  Future<void> _chooseLanguage(String code) async {
    widget.onLocaleChanged(Locale(code));
    await _save(preferredLocale: code);
  }

  Future<void> _save({String? preferredLocale, bool? marketingOptIn}) async {
    final l = L.of(context);
    setState(() => _saving = true);
    try {
      final updated = await widget.api.updateProfile(
        preferredLocale: preferredLocale,
        marketingOptIn: marketingOptIn,
      );
      if (!mounted) return;
      setState(() => _profile = updated);
      _say(l.customerProfileSaved);
    } on PlayerApiException {
      if (mounted) _say(l.customerProfileSaveError);
    } finally {
      if (mounted) setState(() => _saving = false);
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
      appBar: AppBar(title: Text(l.customerProfileTitle)),
      body: switch (_state) {
        _Load.loading => Semantics(
            label: l.a11yLoadingProfile,
            child: const Center(child: CircularProgressIndicator()),
          ),
        _Load.failed => Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(l.customerProfileLoadError, style: TextStyle(color: theme.colorScheme.error)),
                const SizedBox(height: 8),
                TextButton(onPressed: _load, child: Text(l.customerCommonRetry)),
              ],
            ),
          ),
        _Load.ready => _body(l, theme, _profile!),
      },
    );
  }

  Widget _body(L l, ThemeData theme, PlayerProfile profile) {
    final current = Localizations.localeOf(context).languageCode;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text(profile.displayName, style: theme.textTheme.headlineSmall),
        const SizedBox(height: 4),
        Text(
          profile.phoneNumber ?? '—',
          style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
        ),
        Text(
          l.customerProfilePhoneNote,
          style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
        ),
        const SizedBox(height: 24),
        Text(l.customerProfileLanguage, style: theme.textTheme.titleMedium),
        const SizedBox(height: 8),
        // Таджикский здесь наравне с остальными: приложение работает в Таджикистане, и
        // веб-версия, предлагавшая только русский и английский, просто теряла эту часть
        // аудитории.
        SegmentedButton<String>(
          segments: [
            ButtonSegment(value: 'ru', label: Text(l.customerProfileLangRu)),
            ButtonSegment(value: 'tg', label: Text(l.customerProfileLangTg)),
            ButtonSegment(value: 'en', label: Text(l.customerProfileLangEn)),
          ],
          selected: {current},
          onSelectionChanged: _saving ? null : (selection) => _chooseLanguage(selection.first),
        ),
        const SizedBox(height: 24),
        SwitchListTile(
          contentPadding: EdgeInsets.zero,
          title: Text(l.customerProfileMarketing),
          value: profile.marketingOptIn,
          onChanged: _saving ? null : (value) => _save(marketingOptIn: value),
        ),
        const SizedBox(height: 24),
        OutlinedButton(onPressed: widget.onChangeClub, child: Text(l.customerClubPickerChange)),
        const SizedBox(height: 8),
        OutlinedButton(
          onPressed: widget.onSignOut,
          style: OutlinedButton.styleFrom(foregroundColor: theme.colorScheme.error),
          child: Text(l.customerProfileSignOut),
        ),
      ],
    );
  }
}
