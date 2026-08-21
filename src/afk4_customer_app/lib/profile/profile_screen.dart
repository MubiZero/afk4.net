import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../push/push_service.dart';
import '../l10n/app_localizations.dart';
import '../phone/phone_verification_sheet.dart';
import '../shell/app_scaffold.dart';
import '../theme/brand_mark.dart';
import 'pin_sheet.dart';

/// Профиль: кто вошёл, чем садиться за ПК, на каком языке говорить, и выходы — из аккаунта
/// и из клуба.
///
/// Имя, номер и PIN принадлежат человеку и одинаковы во всех клубах сети. Рассылка — дело
/// клуба, поэтому её карточка появляется, только когда счёт в клубе уже есть.
class ProfileScreen extends StatefulWidget {
  const ProfileScreen({
    super.key,
    required this.api,
    this.person,
    this.accountOpen = true,
    this.push,
    required this.onSignOut,
    required this.onChangeClub,
    required this.onLocaleChanged,
    this.onPhoneVerified,
    this.onPersonChanged,
  });

  final PlayerApiClient api;

  /// Личность: имя, номер и признак «PIN задан». null — список клубов не спросился, тогда
  /// экран обходится клубным профилем, как раньше.
  final MePerson? person;

  /// Есть ли у игрока счёт в этом клубе. Пока нет, клубного профиля не существует.
  final bool accountOpen;

  /// Уведомления на телефон. null — платформа их не поддерживает (веб, тесты).
  final PushService? push;
  final VoidCallback onSignOut;
  final VoidCallback onChangeClub;
  final ValueChanged<Locale> onLocaleChanged;

  /// Номер подтвердили отсюда — оболочке пора считать игрока подтверждённым.
  final VoidCallback? onPhoneVerified;

  /// Личность изменилась — имя, язык или PIN. Оболочке пора перечитать её.
  final Future<void> Function()? onPersonChanged;

  @override
  State<ProfileScreen> createState() => _ProfileScreenState();
}

enum _Load { loading, failed, ready }

class _ProfileScreenState extends State<ProfileScreen> {
  _Load _state = _Load.loading;
  PlayerProfile? _profile;
  bool _saving = false;
  bool _pushEnabled = false;

  @override
  void initState() {
    super.initState();
    _load();
    _readPushState();
  }

  @override
  void didUpdateWidget(ProfileScreen oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.accountOpen && !oldWidget.accountOpen) _load();
  }

  Future<void> _readPushState() async {
    final push = widget.push;
    if (push == null || !push.isSupported) return;
    final enabled = await push.isEnabled();
    if (mounted) setState(() => _pushEnabled = enabled);
  }

  /// Включение может не состояться: система вправе отказать в разрешении. Тогда переключатель
  /// возвращается назад — врать игроку, что уведомления включены, хуже, чем не включить их.
  Future<void> _togglePush(bool value) async {
    final push = widget.push;
    if (push == null) return;

    setState(() => _saving = true);
    var enabled = false;
    if (value) {
      enabled = await push.enable();
    } else {
      await push.disable();
    }
    if (!mounted) return;
    setState(() {
      _pushEnabled = enabled;
      _saving = false;
    });
  }

  Future<void> _load() async {
    // В клубе, где счёта ещё нет, клубного профиля тоже нет — и это не сбой. Экран при этом
    // работает: имя, PIN и язык принадлежат человеку, а не заведению.
    if (!widget.accountOpen) {
      setState(() {
        _profile = null;
        _state = _Load.ready;
      });
      return;
    }

    setState(() => _state = _Load.loading);
    try {
      final profile = await widget.api.getProfile();
      if (!mounted) return;
      setState(() {
        _profile = profile;
        _state = _Load.ready;
      });
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      if (error.isNoAccountInClub) {
        setState(() {
          _profile = null;
          _state = _Load.ready;
        });
        return;
      }
      // Веб оставлял на экране вечный скелет: непонятно, грузится или сломалось.
      setState(() => _state = _Load.failed);
    }
  }

  /// Язык применяется сразу, не дожидаясь сервера: это настройка интерфейса, и заминка
  /// в сети не повод показывать игроку чужой язык. На сервер он уходит как предпочтение —
  /// им пользуются письма и уведомления.
  ///
  /// Язык принадлежит человеку, а не клубу: выбрав его в одном заведении, игрок не должен
  /// выбирать заново в соседнем.
  Future<void> _chooseLanguage(String code) async {
    final l = L.of(context);
    widget.onLocaleChanged(Locale(code));

    final person = widget.person;
    if (person == null) {
      await _save(preferredLocale: code);
      return;
    }

    setState(() => _saving = true);
    try {
      await widget.api.updateMe(displayName: person.displayName, preferredLocale: code);
      await widget.onPersonChanged?.call();
      if (mounted) _say(l.customerProfileSaved);
    } on PlayerApiException {
      if (mounted) _say(l.customerProfileSaveError);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
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
  Future<void> _verifyPhone(String? phoneNumber) async {
    final l = L.of(context);
    final confirmed = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (_) => PhoneVerificationSheet(api: widget.api, initialPhone: phoneNumber),
    );
    if (confirmed != true || !mounted) return;

    _say(l.customerPhoneDone);
    widget.onPhoneVerified?.call();
    await widget.onPersonChanged?.call();
    if (mounted) await _load();
  }

  /// PIN задают здесь и только здесь: клуб сетевой PIN не назначает, а SMS на это не тратится.
  Future<void> _changePin(bool pinSet) async {
    final l = L.of(context);
    final saved = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (_) => PinSheet(api: widget.api, pinSet: pinSet),
    );
    if (saved != true || !mounted) return;

    _say(l.customerPinDone);
    await widget.onPersonChanged?.call();
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
              _Load.ready => _body(l, theme, _profile),
            },
          ),
        ),
      ],
    );
  }

  /// Карточки вместо одного столбца контролов: кто я, чем сажусь за ПК, как со мной говорить,
  /// как отсюда выйти. Раньше настройка языка и кнопка выхода отличались друг от друга только
  /// текстом, и глазу не за что было зацепиться при беглом просмотре.
  List<Widget> _body(L l, ThemeData theme, PlayerProfile? profile) {
    final current = Localizations.localeOf(context).languageCode;
    final person = widget.person;

    // Имя и номер — человека, а не клубной карточки: в соседнем заведении они те же самые.
    final displayName = person?.displayName ?? profile?.displayName ?? '';
    final phoneNumber = person?.phoneNumber ?? profile?.phoneNumber;
    final phoneVerified = person?.phoneVerified ?? profile?.phoneVerified ?? false;
    final pinSet = person?.pinSet ?? false;

    return [
      _Group(
        children: [
          Text(displayName, style: theme.textTheme.headlineSmall),
          const SizedBox(height: 4),
          Text(
            phoneNumber ?? '—',
            style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
          // Подтверждённость номера — не украшение: от неё зависят пополнение и брони,
          // поэтому видно и состояние, и способ его изменить.
          Text(
            phoneVerified ? l.customerProfilePhoneNote : l.customerProfilePhoneUnverified,
            style: theme.textTheme.bodySmall?.copyWith(
              color: phoneVerified ? theme.colorScheme.onSurfaceVariant : theme.colorScheme.error,
            ),
          ),
          // Сменить номер можно только там, где клуб уже знает игрока: подтверждение живёт
          // на клубной карточке, и до первого действия его негде поставить.
          if (profile != null) ...[
            const SizedBox(height: 12),
            Align(
              alignment: Alignment.centerLeft,
              child: OutlinedButton(
                onPressed: _saving ? null : () => _verifyPhone(phoneNumber),
                child: Text(phoneVerified
                    ? l.customerProfileChangePhone
                    : l.customerProfileVerifyPhone),
              ),
            ),
          ],
        ],
      ),
      const SizedBox(height: 12),
      // PIN — своей карточкой: это единственное место во всей системе, где его задают, и
      // выглядеть строкой настроек оно не должно.
      if (person != null) ...[
        _Group(
          children: [
            Text(l.customerPinTitle, style: theme.textTheme.titleMedium),
            const SizedBox(height: 4),
            Text(
              pinSet ? l.customerPinStateSet : l.customerPinStateUnset,
              style: theme.textTheme.bodySmall?.copyWith(
                color: pinSet ? theme.colorScheme.onSurfaceVariant : theme.colorScheme.error,
              ),
            ),
            const SizedBox(height: 4),
            Text(
              l.customerPinIntro,
              style:
                  theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 12),
            Align(
              alignment: Alignment.centerLeft,
              child: OutlinedButton(
                onPressed: _saving ? null : () => _changePin(pinSet),
                child: Text(pinSet ? l.customerPinChange : l.customerPinSet),
              ),
            ),
          ],
        ),
        const SizedBox(height: 12),
      ],
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
      // Пуши — отдельно от рассылки: это разные вещи. Рассылка про акции, пуши про то, что
      // сессия кончается через десять минут. Выключение здесь настоящее: устройство снимается
      // с сервера, а не просто прячется флажком.
      if (widget.push?.isSupported ?? false) ...[
        _Group(
          children: [
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              title: Text(l.customerProfilePush),
              subtitle: Text(l.customerProfilePushNote),
              value: _pushEnabled,
              onChanged: _saving ? null : _togglePush,
            ),
          ],
        ),
        const SizedBox(height: 12),
      ],
      // Рассылка — дело клуба: она про его акции, и до счёта в нём соглашаться не на что.
      if (profile != null) ...[
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
      ],
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
      // Чей это продукт. Приложение носит цвет и знак клуба — игрок пришёл к нему, а не к
      // нам, — поэтому наш знак стоит здесь: в самом низу настроек, где подпись платформы
      // никому не мешает и никого не путает.
      const SizedBox(height: 24),
      Center(
        child: Column(
          children: [
            const BrandMark(size: 30),
            const SizedBox(height: 8),
            Text(
              l.customerProfilePoweredBy,
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
          ],
        ),
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
