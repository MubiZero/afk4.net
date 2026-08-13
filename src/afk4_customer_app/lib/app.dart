import 'package:flutter/material.dart';

import 'api/player_api_client.dart';
import 'auth/player_session.dart';
import 'auth/player_session_store.dart';
import 'auth/sign_in_screen.dart';
import 'shell/app_shell.dart';
import 'l10n/locale_preference_store.dart';
import 'l10n/localization_setup.dart';
import 'organization/club_picker_screen.dart';
import 'organization/organization.dart';
import 'organization/organization_directory.dart';
import 'organization/selected_organization_store.dart';
import 'theme/ambient_background.dart';
import 'theme/app_theme.dart';

/// Название продукта. Регистр и точка — часть знака, см. бренд-гайд; не переводится и не
/// склоняется. Название конкретного клуба приходит с сервера и живёт отдельно от него.
const String brandName = 'AFK4.NET';

/// Корень клиентского приложения.
///
/// Тема следует системной настройке телефона: у клуба ночная аудитория, и навязывать светлую
/// тему тому, кто держит телефон в тёмном зале, — плохая идея. Явный выбор языка появится
/// в профиле; до этого берётся язык устройства с откатом на русский.
class CustomerApp extends StatefulWidget {
  const CustomerApp({
    super.key,
    this.locale,
    required this.directory,
    required this.api,
    this.selectedOrganizationStore = const SelectedOrganizationStore(),
    this.sessionStore = const PlayerSessionStore(),
    this.localeStore = const LocalePreferenceStore(),
  });

  /// Жёстко задаётся только тестами: перебивает и выбор игрока, и язык устройства.
  final Locale? locale;
  final OrganizationDirectory directory;
  final PlayerApiClient api;
  final SelectedOrganizationStore selectedOrganizationStore;
  final PlayerSessionStore sessionStore;
  final LocalePreferenceStore localeStore;

  @override
  State<CustomerApp> createState() => _CustomerAppState();
}

class _CustomerAppState extends State<CustomerApp> {
  /// Язык, выбранный игроком в профиле. null — выбора не было, берётся язык устройства.
  Locale? _chosen;

  @override
  void initState() {
    super.initState();
    _restoreLocale();
  }

  Future<void> _restoreLocale() async {
    final saved = await widget.localeStore.read();
    if (mounted && saved != null) setState(() => _chosen = saved);
  }

  Future<void> _chooseLocale(Locale locale) async {
    setState(() => _chosen = locale);
    await widget.localeStore.write(locale);
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      // Бренд не локализуется — см. docs/product/copy-voice-and-terminology.md.
      title: brandName,
      debugShowCheckedModeBanner: false,
      // Приложение всегда тёмное, а не «следует системе»: это игровая витрина, её смотрят
      // в зале и ночью, и весь визуальный строй — свет, свечение акцента, контраст цифр —
      // построен на тёмном. Светлый вариант тех же экранов был бы вторым продуктом.
      theme: AppTheme.dark(),
      darkTheme: AppTheme.dark(),
      themeMode: ThemeMode.dark,
      // Свет зала живёт под навигатором: один фон на все экраны, без шва при переходах.
      builder: (context, child) => AmbientBackground(child: child ?? const SizedBox.shrink()),
      locale: widget.locale ?? _chosen,
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      localeResolutionCallback: (deviceLocale, supported) {
        final match = supported.where((l) => l.languageCode == deviceLocale?.languageCode);
        return match.isNotEmpty ? match.first : const Locale('ru');
      },
      home: _Root(
        directory: widget.directory,
        api: widget.api,
        organizationStore: widget.selectedOrganizationStore,
        sessionStore: widget.sessionStore,
        onLocaleChanged: _chooseLocale,
      ),
    );
  }
}

/// Решает, что показать при запуске: выбор клуба, вход или экран за ними.
class _Root extends StatefulWidget {
  const _Root({
    required this.directory,
    required this.api,
    required this.organizationStore,
    required this.sessionStore,
    required this.onLocaleChanged,
  });

  final OrganizationDirectory directory;
  final PlayerApiClient api;
  final SelectedOrganizationStore organizationStore;
  final PlayerSessionStore sessionStore;
  final ValueChanged<Locale> onLocaleChanged;

  @override
  State<_Root> createState() => _RootState();
}

class _RootState extends State<_Root> {
  bool _restoring = true;
  Organization? _organization;
  PlayerSession? _session;

  @override
  void initState() {
    super.initState();
    _restore();
  }

  Future<void> _restore() async {
    final organization = await widget.organizationStore.read();
    final session = await widget.sessionStore.read();
    if (session != null) widget.api.updateSession(session);
    if (!mounted) return;
    setState(() {
      _organization = organization;
      _session = session;
      _restoring = false;
    });
  }

  Future<void> _selectOrganization(Organization organization) async {
    await widget.organizationStore.write(organization);
    if (!mounted) return;
    setState(() => _organization = organization);
  }

  Future<void> _onSignedIn() async {
    final session = widget.api.session;
    if (session != null) await widget.sessionStore.write(session);
    if (!mounted) return;
    setState(() => _session = session);
  }

  /// Выход стирает и сессию, и сохранённые данные: устройство бывает общим, и следующий
  /// вошедший не должен видеть чужой кошелёк. Правило перенесено из веб-версии.
  Future<void> _signOut() async {
    widget.api.updateSession(null);
    await widget.sessionStore.clear();
    if (!mounted) return;
    setState(() => _session = null);
  }

  Future<void> _changeClub() async {
    await _signOut();
    await widget.organizationStore.clear();
    if (!mounted) return;
    setState(() => _organization = null);
  }

  @override
  Widget build(BuildContext context) {
    if (_restoring) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }

    final organization = _organization;
    if (organization == null) {
      return ClubPickerScreen(directory: widget.directory, onSelected: _selectOrganization);
    }

    final session = _session;
    if (session == null) {
      return SignInScreen(
        organization: organization,
        api: widget.api,
        onSignedIn: _onSignedIn,
        onChangeClub: _changeClub,
      );
    }

    return AppShell(
      api: widget.api,
      session: session,
      onSignOut: _signOut,
      onChangeClub: _changeClub,
      onLocaleChanged: widget.onLocaleChanged,
    );
  }
}
