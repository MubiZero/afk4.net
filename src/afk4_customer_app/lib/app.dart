import 'dart:async';

import 'package:flutter/material.dart';

import 'api/dto.dart';
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
import 'push/push_messages.dart';
import 'push/push_service.dart';
import 'push/push_tokens.dart';
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
    this.pushTokens,
    this.pushMessages,
  });

  /// Жёстко задаётся только тестами: перебивает и выбор игрока, и язык устройства.
  final Locale? locale;
  final OrganizationDirectory directory;
  final PlayerApiClient api;
  final SelectedOrganizationStore selectedOrganizationStore;
  final PlayerSessionStore sessionStore;
  final LocalePreferenceStore localeStore;

  /// Откуда берётся адрес телефона для пушей. Тесты подставляют свой — настоящий Firebase
  /// в проверке того, что при выходе токен снимается, участвовать не должен.
  final PushTokens? pushTokens;

  /// Входящие уведомления. null — приходить нечему: веб или тест без подмены.
  final PushMessages? pushMessages;

  @override
  State<CustomerApp> createState() => _CustomerAppState();
}

class _CustomerAppState extends State<CustomerApp> {
  /// Язык, выбранный игроком в профиле. null — выбора не было, берётся язык устройства.
  Locale? _chosen;

  /// Цвет выбранного клуба. Приложение носит его, пока игрок в этом клубе: заведение он
  /// узнаёт по цвету раньше, чем прочитает название. null — клуб не выбран или цвет не задан,
  /// тогда остаётся фирменный emerald.
  Color? _clubColor;

  @override
  void initState() {
    super.initState();
    _restoreLocale();
  }

  Future<void> _restoreLocale() async {
    final saved = await widget.localeStore.read();
    if (mounted && saved != null) setState(() => _chosen = saved);
  }

  void _useClubColor(Organization? organization) {
    final color = AppTheme.parseBrandColor(organization?.accentColor);
    // Пересборка темы — это перерисовка всего приложения, поэтому только при настоящей смене.
    if (color != _clubColor) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) setState(() => _clubColor = color);
      });
    }
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
      theme: AppTheme.dark(clubColor: _clubColor),
      darkTheme: AppTheme.dark(clubColor: _clubColor),
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
        pushTokens: widget.pushTokens,
        pushMessages: widget.pushMessages,
        organizationStore: widget.selectedOrganizationStore,
        sessionStore: widget.sessionStore,
        onLocaleChanged: _chooseLocale,
        onOrganizationChanged: _useClubColor,
      ),
    );
  }
}

/// Решает, что показать при запуске: выбор клуба, вход или экран за ними.
class _Root extends StatefulWidget {
  const _Root({
    required this.directory,
    required this.api,
    required this.pushTokens,
    required this.pushMessages,
    required this.organizationStore,
    required this.sessionStore,
    required this.onLocaleChanged,
    required this.onOrganizationChanged,
  });

  final OrganizationDirectory directory;
  final PlayerApiClient api;
  final PushTokens? pushTokens;
  final PushMessages? pushMessages;
  final SelectedOrganizationStore organizationStore;
  final PlayerSessionStore sessionStore;
  final ValueChanged<Locale> onLocaleChanged;

  /// Клуб выбран, восстановлен или сброшен — теме пора перекраситься.
  final ValueChanged<Organization?> onOrganizationChanged;

  @override
  State<_Root> createState() => _RootState();
}

class _RootState extends State<_Root> {
  bool _restoring = true;
  Organization? _organization;
  PlayerSession? _session;
  PushService? _push;

  /// Человек и его клубы. null — ещё не спросили или не спросилось; тогда приложение работает
  /// как раньше, просто не знает, есть ли у игрока счёт в открытом клубе.
  Me? _me;

  /// Игрок открыл витрину, чтобы перейти в другой клуб. Выбранный до этого никуда не делся.
  bool _pickingClub = false;

  @override
  void initState() {
    super.initState();
    _restore();
  }

  Future<void> _restore() async {
    final tokens = widget.pushTokens;
    if (tokens != null) {
      _push = PushService(api: widget.api, tokens: tokens);
    }
    final organization = await widget.organizationStore.read();
    final session = await widget.sessionStore.read();
    // Клуб называется на каждом запросе: аккаунт один на всю сеть, и без этого сервер не
    // знает, чей кошелёк показывать.
    widget.api.organizationId = organization?.organizationId;
    // Слушать смену сессии обязательно, а не «полезно»: refresh-токен одноразовый, и продление
    // выдаёт новый. Пока продлённую сессию никто не писал на диск, там оставался токен, который
    // сервер уже отозвал, — приложение работало до перезапуска, а наутро встречало игрока
    // ошибкой, лечившейся только повторным входом.
    widget.api.onSessionChanged = _onSessionRotated;
    if (session != null) {
      widget.api.updateSession(session);
      // Токен FCM меняется сам по себе: без сверки при запуске пуши однажды просто
      // перестают доходить, и никто об этом не узнаёт.
      unawaited(_push?.syncAfterSignIn() ?? Future<void>.value());
    }
    if (!mounted) return;
    setState(() {
      _organization = organization;
      _session = session;
      _restoring = false;
    });
    if (session != null) await _loadMe();
  }

  /// Кто вошёл и где у него счета. Ошибку не показываем: без этого списка приложение работает
  /// как работало, а сообщение о сбое на первом экране пугает зря.
  Future<void> _loadMe() async {
    try {
      final me = await widget.api.getMe();
      if (mounted) setState(() => _me = me);
    } on PlayerApiException {
      if (mounted) setState(() => _me = null);
    }
  }

  /// Сессия сменилась внутри клиента — продлилась или умерла.
  ///
  /// Продлённую пишем на диск: следующий запуск обязан взять живой токен, а не тот, который
  /// сервер отозвал при продлении. Умершую стираем и показываем экран входа — это честный ответ
  /// вместо «проверьте соединение» на экране, за которым уже нет никакой сессии.
  void _onSessionRotated(PlayerSession? next) {
    unawaited(next == null ? widget.sessionStore.clear() : widget.sessionStore.write(next));
    if (!mounted) return;
    setState(() => _session = next);
  }

  Future<void> _selectOrganization(Organization organization) async {
    await widget.organizationStore.write(organization);
    widget.api.organizationId = organization.organizationId;
    if (!mounted) return;
    setState(() {
      _organization = organization;
      _pickingClub = false;
    });
  }

  Future<void> _onSignedIn() async {
    final session = widget.api.session;
    if (session != null) {
      await widget.sessionStore.write(session);
      await _push?.syncAfterSignIn();
    }
    if (!mounted) return;
    setState(() => _session = session);
    if (session != null) await _loadMe();
  }

  /// Выход стирает и сессию, и сохранённые данные: устройство бывает общим, и следующий
  /// вошедший не должен видеть чужой кошелёк. Правило перенесено из веб-версии.
  Future<void> _signOut() async {
    // Снимаем устройство до того, как выбросить сессию: после выхода звать сервер уже нечем,
    // а токен остался бы висеть на прежнем игроке — и уведомления пришли бы ему.
    await _push?.clearOnSignOut();
    widget.api.updateSession(null);
    await widget.sessionStore.clear();
    if (!mounted) return;
    setState(() {
      _session = null;
      _me = null;
    });
  }

  /// Смена клуба больше не выбрасывает из аккаунта: аккаунт один на всю сеть, и войти заново
  /// ради перехода в соседнее заведение — цена ни за что. Прежний клуб при этом не забывается
  /// до самого выбора: передумавший игрок иначе оставался бы на витрине без дороги назад.
  void _changeClub() => setState(() => _pickingClub = true);

  @override
  Widget build(BuildContext context) {
    if (_restoring) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }

    // Цвет клуба сообщается наружу из одного места — из построения экрана: сюда сходятся и
    // восстановление при запуске, и выбор, и смена клуба, и не надо помнить про каждый путь.
    widget.onOrganizationChanged(_organization);

    final organization = _organization;
    if (organization == null || _pickingClub) {
      return ClubPickerScreen(
        directory: widget.directory,
        onSelected: _selectOrganization,
        myClubs: _me?.clubs ?? const [],
        selectedOrganizationId: organization?.organizationId,
      );
    }

    final session = _session;
    if (session == null) {
      return SignInScreen(
        organization: organization,
        api: widget.api,
        onSignedIn: _onSignedIn,
        onChangeClub: _changeClub,
        onLocaleChanged: widget.onLocaleChanged,
      );
    }

    return AppShell(
      api: widget.api,
      session: session,
      organization: organization,
      me: _me,
      push: _push,
      pushMessages: widget.pushMessages,
      onSignOut: _signOut,
      onChangeClub: _changeClub,
      onLocaleChanged: widget.onLocaleChanged,
      onAccountOpened: _loadMe,
    );
  }
}
