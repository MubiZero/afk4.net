import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../phone/phone_verification_sheet.dart';
import '../shell/app_scaffold.dart';

/// Профиль: кто вошёл, на каком языке говорить, и выходы — из аккаунта и из клуба.
class ProfileScreen extends StatefulWidget {
  const ProfileScreen({
    super.key,
    required this.api,
    required this.onSignOut,
    required this.onChangeClub,
    required this.onLocaleChanged,
    this.onPhoneVerified,
  });

  final PlayerApiClient api;
  final VoidCallback onSignOut;
  final VoidCallback onChangeClub;
  final ValueChanged<Locale> onLocaleChanged;

  /// Номер подтвердили отсюда — оболочке пора считать игрока подтверждённым.
  final VoidCallback? onPhoneVerified;

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

  /// Смена номера — та же процедура, что и первое подтверждение: код приходит на НОВЫЙ номер,
  /// и владение им доказывается прежде, чем он станет основным.
  Future<void> _verifyPhone(PlayerProfile profile) async {
    final l = L.of(context);
    final confirmed = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (_) => PhoneVerificationSheet(api: widget.api, initialPhone: profile.phoneNumber),
    );
    if (confirmed != true || !mounted) return;

    _say(l.customerPhoneDone);
    widget.onPhoneVerified?.call();
    await _load();
  }

  void _say(String message) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return AppScaffold(
      title: l.customerProfileTitle,
      slivers: [
        SliverPadding(
          padding: sectionPadding,
          sliver: SliverList.list(
            children: switch (_state) {
              _Load.loading => [
                  Semantics(
                    label: l.a11yLoadingProfile,
                    child: const Padding(
                      padding: EdgeInsets.symmetric(vertical: 32),
                      child: Center(child: CircularProgressIndicator()),
                    ),
                  ),
                ],
              _Load.failed => [
                  Center(
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(l.customerProfileLoadError,
                            style: TextStyle(color: theme.colorScheme.error)),
                        const SizedBox(height: 8),
                        TextButton(onPressed: _load, child: Text(l.customerCommonRetry)),
                      ],
                    ),
                  ),
                ],
              _Load.ready => _body(l, theme, _profile!),
            },
          ),
        ),
      ],
    );
  }

  /// Три карточки вместо одного столбца контролов: кто я, как со мной говорить, как отсюда
  /// выйти. Раньше настройка языка и кнопка выхода отличались друг от друга только текстом,
  /// и глазу не за что было зацепиться при беглом просмотре.
  List<Widget> _body(L l, ThemeData theme, PlayerProfile profile) {
    final current = Localizations.localeOf(context).languageCode;

    return [
      _Group(
        children: [
          Text(profile.displayName, style: theme.textTheme.headlineSmall),
          const SizedBox(height: 4),
          Text(
            profile.phoneNumber ?? '—',
            style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
          // Подтверждённость номера — не украшение: от неё зависят пополнение и брони,
          // поэтому видно и состояние, и способ его изменить.
          Text(
            profile.phoneVerified ? l.customerProfilePhoneNote : l.customerProfilePhoneUnverified,
            style: theme.textTheme.bodySmall?.copyWith(
              color: profile.phoneVerified
                  ? theme.colorScheme.onSurfaceVariant
                  : theme.colorScheme.error,
            ),
          ),
          const SizedBox(height: 12),
          Align(
            alignment: Alignment.centerLeft,
            child: OutlinedButton(
              onPressed: _saving ? null : () => _verifyPhone(profile),
              child: Text(profile.phoneVerified
                  ? l.customerProfileChangePhone
                  : l.customerProfileVerifyPhone),
            ),
          ),
        ],
      ),
      const SizedBox(height: 12),
      _Group(
        children: [
          Text(l.customerProfileLanguage, style: theme.textTheme.titleMedium),
          const SizedBox(height: 12),
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
        ],
      ),
      const SizedBox(height: 12),
      // Рассылка — своей карточкой, а не хвостом языковой: заголовок «Язык» над переключателем
      // об акциях обещал не то, что под ним стоит.
      _Group(
        children: [
          SwitchListTile(
            contentPadding: EdgeInsets.zero,
            title: Text(l.customerProfileMarketing),
            value: profile.marketingOptIn,
            onChanged: _saving ? null : (value) => _save(marketingOptIn: value),
          ),
        ],
      ),
      const SizedBox(height: 12),
      _Group(
        children: [
          SizedBox(
            width: double.infinity,
            child: OutlinedButton(
              onPressed: widget.onChangeClub,
              child: Text(l.customerClubPickerChange),
            ),
          ),
          const SizedBox(height: 8),
          SizedBox(
            width: double.infinity,
            child: OutlinedButton(
              onPressed: widget.onSignOut,
              style: OutlinedButton.styleFrom(foregroundColor: theme.colorScheme.error),
              child: Text(l.customerProfileSignOut),
            ),
          ),
        ],
      ),
    ];
  }
}

/// Группа настроек одной карточкой. Соседство внутри карточки говорит «это об одном», и
/// говорит дешевле, чем заголовки над каждой строкой.
class _Group extends StatelessWidget {
  const _Group({required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) => Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: children),
        ),
      );
}
