import 'package:flutter/material.dart';

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
    this.selectedOrganizationStore = const SelectedOrganizationStore(),
  });

  /// Задаётся только тестами и будущим переключателем в профиле; null = язык устройства.
  final Locale? locale;
  final OrganizationDirectory directory;
  final SelectedOrganizationStore selectedOrganizationStore;

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
      home: _Root(directory: directory, store: selectedOrganizationStore),
    );
  }
}

/// Решает, что показать при запуске: выбор клуба или экран за ним. Клуб, выбранный однажды,
/// не спрашивается снова — это предпочтение, а не шаг входа.
class _Root extends StatefulWidget {
  const _Root({required this.directory, required this.store});

  final OrganizationDirectory directory;
  final SelectedOrganizationStore store;

  @override
  State<_Root> createState() => _RootState();
}

class _RootState extends State<_Root> {
  bool _restoring = true;
  Organization? _organization;

  @override
  void initState() {
    super.initState();
    _restore();
  }

  Future<void> _restore() async {
    final saved = await widget.store.read();
    if (!mounted) return;
    setState(() {
      _organization = saved;
      _restoring = false;
    });
  }

  Future<void> _select(Organization organization) async {
    await widget.store.write(organization);
    if (!mounted) return;
    setState(() => _organization = organization);
  }

  @override
  Widget build(BuildContext context) {
    if (_restoring) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }

    final organization = _organization;
    if (organization == null) {
      return ClubPickerScreen(directory: widget.directory, onSelected: _select);
    }

    return _PlaceholderHome(organization: organization, onChangeClub: () async {
      await widget.store.clear();
      if (!mounted) return;
      setState(() => _organization = null);
    });
  }
}

/// Временная заглушка вместо главного экрана: он появится следующим шагом вместе со входом.
/// Здесь она подтверждает, что клуб выбран и запомнился.
class _PlaceholderHome extends StatelessWidget {
  const _PlaceholderHome({required this.organization, required this.onChangeClub});

  final Organization organization;
  final VoidCallback onChangeClub;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    return Scaffold(
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(organization.name, style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 12),
            OutlinedButton(onPressed: onChangeClub, child: Text(l.customerClubPickerChange)),
          ],
        ),
      ),
    );
  }
}
