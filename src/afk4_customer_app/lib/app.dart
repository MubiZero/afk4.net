import 'package:flutter/material.dart';

import 'api/player_api_client.dart';
import 'auth/player_session.dart';
import 'auth/player_session_store.dart';
import 'auth/sign_in_screen.dart';
import 'l10n/app_localizations.dart';
import 'l10n/localization_setup.dart';
import 'organization/club_picker_screen.dart';
import 'organization/organization.dart';
import 'organization/organization_directory.dart';
import 'organization/selected_organization_store.dart';
import 'theme/app_theme.dart';

/// Название продукта. Регистр и точка — часть знака, см. бренд-гайд; не переводится и не
/// склоняется. Название конкретного клуба приходит с сервера и живёт отдельно от него.
const String brandName = 'AFK4.NET';

/// Корень клиентского приложения.
///
/// Тема следует системной настройке телефона: у клуба ночная аудитория, и навязывать светлую
/// тему тому, кто держит телефон в тёмном зале, — плохая идея. Явный выбор языка появится
/// в профиле; до этого берётся язык устройства с откатом на русский.
class CustomerApp extends StatelessWidget {
  const CustomerApp({
    super.key,
    this.locale,
    required this.directory,
    required this.api,
    this.selectedOrganizationStore = const SelectedOrganizationStore(),
    this.sessionStore = const PlayerSessionStore(),
  });

  /// Задаётся только тестами и будущим переключателем в профиле; null = язык устройства.
  final Locale? locale;
  final OrganizationDirectory directory;
  final PlayerApiClient api;
  final SelectedOrganizationStore selectedOrganizationStore;
  final PlayerSessionStore sessionStore;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      // Бренд не локализуется — см. docs/product/copy-voice-and-terminology.md.
      title: brandName,
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light(),
      darkTheme: AppTheme.dark(),
      themeMode: ThemeMode.system,
      locale: locale,
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      localeResolutionCallback: (deviceLocale, supported) {
        final match = supported.where((l) => l.languageCode == deviceLocale?.languageCode);
        return match.isNotEmpty ? match.first : const Locale('ru');
      },
      home: _Root(
        directory: directory,
        api: api,
        organizationStore: selectedOrganizationStore,
        sessionStore: sessionStore,
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
  });

  final OrganizationDirectory directory;
  final PlayerApiClient api;
  final SelectedOrganizationStore organizationStore;
  final PlayerSessionStore sessionStore;

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

    return _PlaceholderHome(session: session, onSignOut: _signOut);
  }
}

/// Временная заглушка вместо главного экрана: он появится следующим шагом.
/// Здесь она подтверждает, что вход прошёл и сессия сохранилась.
class _PlaceholderHome extends StatelessWidget {
  const _PlaceholderHome({required this.session, required this.onSignOut});

  final PlayerSession session;
  final VoidCallback onSignOut;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    return Scaffold(
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(l.customerDashboardWelcome, style: Theme.of(context).textTheme.bodyMedium),
            Text(session.displayName, style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 16),
            OutlinedButton(onPressed: onSignOut, child: Text(l.customerProfileSignOut)),
          ],
        ),
      ),
    );
  }
}
