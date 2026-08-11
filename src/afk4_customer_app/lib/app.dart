import 'package:flutter/material.dart';

import 'l10n/app_localizations.dart';
import 'l10n/localization_setup.dart';
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
  const CustomerApp({super.key, this.locale});

  /// Задаётся только тестами и будущим переключателем в профиле; null = язык устройства.
  final Locale? locale;

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
      home: const _StartupScreen(),
    );
  }
}

/// Временный экран каркаса: подтверждает, что тема и локализация доехали.
/// Уходит на следующем шаге, когда появится выбор клуба.
class _StartupScreen extends StatelessWidget {
  const _StartupScreen();

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    return Scaffold(
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(brandName, style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 8),
            Text(l.customerCommonLoading, style: Theme.of(context).textTheme.bodyMedium),
          ],
        ),
      ),
    );
  }
}
