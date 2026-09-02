import 'dart:async';

import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../auth/player_session.dart';
import '../dashboard/dashboard_screen.dart';
import '../l10n/app_localizations.dart';
import '../organization/branch_choice.dart';
import '../organization/organization.dart';
import '../push/push_messages.dart';
import '../push/push_notification.dart';
import '../push/push_service.dart';
import '../profile/profile_screen.dart';
import '../reservations/reservations_screen.dart';
import '../wallet/wallet_screen.dart';
import 'network_ban_note.dart';
import 'push_note.dart';

/// Разделы приложения. Порядок — это заявление о том, чем игрок пользуется чаще: сначала то,
/// что происходит сейчас, потом деньги, в конце настройки.
enum AppSection { home, reservations, wallet, profile }

/// Оболочка вошедшего игрока: разделы внизу, содержимое сверху.
///
/// Раздел «Брони» появляется, только если клуб принимает онлайн-брони: вкладка, ведущая в
/// невозможное действие, хуже её отсутствия.
class AppShell extends StatefulWidget {
  const AppShell({
    super.key,
    required this.api,
    required this.session,
    required this.organization,
    this.me,
    this.push,
    this.pushMessages,
    required this.onSignOut,
    required this.onChangeClub,
    required this.onLocaleChanged,
    this.onAccountOpened,
    this.clock = DateTime.now,
  });

  final PlayerApiClient api;
  final PlayerSession session;

  /// Клуб, в который игрок вошёл: его знак и цвет носит приложение, пока игрок здесь.
  final Organization organization;

  /// Человек и его клубы. null — список не спросился; тогда разделы работают как раньше и
  /// сами разберутся с ответом сервера.
  final Me? me;

  /// Уведомления на телефон. null — платформа их не поддерживает (веб, тесты).
  final PushService? push;

  /// Входящие уведомления. null — приходить нечему: веб или тест без подмены.
  final PushMessages? pushMessages;
  final VoidCallback onSignOut;
  final VoidCallback onChangeClub;
  final ValueChanged<Locale> onLocaleChanged;

  /// Счёт в этом клубе только что открылся первым действием — список клубов пора перечитать.
  final Future<void> Function()? onAccountOpened;

  final DateTime Function() clock;

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  AppSection _section = AppSection.home;

  /// null — список возможностей не получен. Тогда разделы показываются все: спрятать
  /// «Брони» из-за сетевого сбоя значит соврать игроку, что клуб их не принимает. Это
  /// удобство интерфейса, а не защита — сервер всё равно проверяет право на действие.
  List<String>? _features;

  /// Номер подтвердили прямо сейчас. Сессия в памяти этого ещё не знает, а входить заново
  /// ради открывшихся возможностей — плохая цена за подтверждение.
  bool _phoneVerifiedNow = false;

  /// Зал, который игрок назвал для первого действия в этом клубе. Помнит оболочка, а не лист:
  /// зал нужен и брони, и пополнению, а спрашивать одно и то же дважды — цена ни за что.
  /// После открытия счёта не нужен вовсе: зал записан в самом счёте.
  String? _chosenBranchId;

  /// Просьба открыть магазин, пришедшая уведомлением. Меняется числом, а не флагом: два
  /// уведомления подряд про два заказа — это две просьбы, и вторая не должна потеряться.
  int _openShopRequest = 0;

  final List<StreamSubscription<PushNotification>> _pushSubscriptions = [];

  /// Уведомление, показанное поверх разделов, и таймер, который его уберёт.
  PushNotification? _note;
  Timer? _noteTimer;

  @override
  void initState() {
    super.initState();
    if (_accountOpen) _loadFeatures();
    _listenForPush();
  }

  @override
  void dispose() {
    _noteTimer?.cancel();
    for (final subscription in _pushSubscriptions) {
      subscription.cancel();
    }
    super.dispose();
  }

  /// Три пути одного уведомления: приложение запустилось нажатием, было в фоне и его вернули
  /// нажатием, или игрок уже внутри. Первые два ведут на нужный экран сразу, третий сначала
  /// показывает сообщение — выдёргивать человека с экрана, где он что-то делает, нельзя.
  void _listenForPush() {
    final messages = widget.pushMessages;
    if (messages == null) return;

    unawaited(
      messages.initialMessage().then((notification) {
        if (notification != null && mounted) _followPush(notification);
      }),
    );

    _pushSubscriptions.add(
      messages.onOpenedApp.listen((notification) {
        if (mounted) _followPush(notification);
      }),
    );

    _pushSubscriptions.add(
      messages.onForegroundMessage.listen((notification) {
        if (mounted) _announcePush(notification);
      }),
    );
  }

  /// Открыть то, о чём уведомление. Незнакомое событие не двигает игрока никуда: приложение
  /// просто открылось, и это честнее, чем увести наугад.
  void _followPush(PushNotification notification) {
    switch (pushDestinationFor(notification.template)) {
      case PushDestination.home:
        _open(AppSection.home);
      case PushDestination.reservations:
        if (_enabled('online_booking')) _open(AppSection.reservations);
      case PushDestination.wallet:
        _open(AppSection.wallet);
      case PushDestination.shop:
        setState(() {
          _section = AppSection.home;
          _openShopRequest++;
        });
      case null:
        break;
    }
  }

  /// Игрок уже в приложении: система в этом случае не показывает ничего сама. Полоса встаёт
  /// над разделами, не перекрывая работу, и уходит сама через несколько секунд. Перейти можно
  /// нажатием — но только если есть куда: кнопка, ведущая в никуда, хуже её отсутствия.
  void _announcePush(PushNotification notification) {
    final text = notification.body ?? notification.title;
    if (text == null || text.trim().isEmpty) return;

    setState(() => _note = notification);
    _noteTimer?.cancel();
    _noteTimer = Timer(const Duration(seconds: 6), () {
      if (mounted) setState(() => _note = null);
    });
  }

  void _dismissNote() {
    _noteTimer?.cancel();
    setState(() => _note = null);
  }

  @override
  void didUpdateWidget(AppShell oldWidget) {
    super.didUpdateWidget(oldWidget);
    // Счёт открылся первым действием — возможности клуба спрашиваем только теперь: до счёта
    // сервер на этот вопрос отвечать не станет.
    if (_accountOpen && _features == null) _loadFeatures();
  }

  Future<void> _loadFeatures() async {
    try {
      final features = await widget.api.getFeatures();
      if (mounted) setState(() => _features = features);
    } on PlayerApiException {
      // Остаётся null — «считаем включённым».
    }
  }

  /// Есть ли у игрока счёт в открытом клубе. Пока его нет, клубу нечего рассказать: ни денег,
  /// ни броней, ни истории. Это не сбой — счёт откроется первой бронью или пополнением.
  ///
  /// Список клубов не спросился (`me == null`) — считаем, что счёт есть: разделы сами
  /// разберутся с ответом сервера, а спрятать всё из-за сетевого сбоя было бы враньём.
  bool get _accountOpen =>
      widget.me == null ||
      widget.me!.clubAt(widget.organization.organizationId) != null;

  /// Где игрок собирается играть. Залы берутся из каталога клубов — того же ответа, из
  /// которого игрок выбирал сам клуб. Со счётом выбора нет: сервер уже знает зал, и обещать
  /// выбор, который ничего не изменит, нельзя.
  BranchChoice get _branch => _accountOpen
      ? const BranchChoice()
      : BranchChoice(
          halls: widget.organization.places,
          chosenId: _chosenBranchId,
          onChosen: (branchId) => setState(() => _chosenBranchId = branchId),
        );

  bool _enabled(String feature) =>
      _features == null || _features!.contains(feature);

  bool get _phoneVerified => _phoneVerifiedNow || widget.session.phoneVerified;

  void _open(AppSection section) => setState(() => _section = section);

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final booking = _enabled('online_booking');

    final sections = <(AppSection, Widget screen, NavigationDestination tab)>[
      (
        AppSection.home,
        DashboardScreen(
          api: widget.api,
          displayName:
              widget.me?.person.displayName ?? widget.session.displayName,
          organization: widget.organization,
          phoneVerified: _phoneVerified,
          features: _features,
          accountOpen: _accountOpen,
          onPhoneVerified: () => setState(() => _phoneVerifiedNow = true),
          onOpenReservations: booking
              ? () => _open(AppSection.reservations)
              : null,
          onOpenWallet: () => _open(AppSection.wallet),
          openShopRequest: _openShopRequest,
          clock: widget.clock,
        ),
        NavigationDestination(
          icon: const Icon(Icons.home_outlined),
          selectedIcon: const Icon(Icons.home),
          label: l.customerNavDashboard,
        ),
      ),
      if (booking)
        (
          AppSection.reservations,
          ReservationsScreen(
            api: widget.api,
            phoneVerified: _phoneVerified,
            accountOpen: _accountOpen,
            branch: _branch,
            onPhoneVerified: () => setState(() => _phoneVerifiedNow = true),
            onAccountOpened: widget.onAccountOpened,
            clock: widget.clock,
          ),
          NavigationDestination(
            icon: const Icon(Icons.event_outlined),
            selectedIcon: const Icon(Icons.event),
            label: l.customerNavReservations,
          ),
        ),
      (
        AppSection.wallet,
        WalletScreen(
          api: widget.api,
          phoneVerified: _phoneVerified,
          features: _features,
          accountOpen: _accountOpen,
          branch: _branch,
          currencyCode: widget.organization.currencyCode ?? 'TJS',
          onPhoneVerified: () => setState(() => _phoneVerifiedNow = true),
          onAccountOpened: widget.onAccountOpened,
          clock: widget.clock,
        ),
        NavigationDestination(
          icon: const Icon(Icons.account_balance_wallet_outlined),
          selectedIcon: const Icon(Icons.account_balance_wallet),
          label: l.customerNavWallet,
        ),
      ),
      (
        AppSection.profile,
        ProfileScreen(
          api: widget.api,
          person: widget.me?.person,
          accountOpen: _accountOpen,
          push: widget.push,
          onSignOut: widget.onSignOut,
          onChangeClub: widget.onChangeClub,
          onLocaleChanged: widget.onLocaleChanged,
          onPhoneVerified: () => setState(() => _phoneVerifiedNow = true),
          onPersonChanged: widget.onAccountOpened,
        ),
        NavigationDestination(
          icon: const Icon(Icons.person_outline),
          selectedIcon: const Icon(Icons.person),
          label: l.customerNavProfile,
        ),
      ),
    ];

    // Раздел ищется по имени, а не по номеру: список укорачивается, когда клуб не принимает
    // брони, и запомненный номер после этого указывал бы на соседа. Пропавший раздел
    // возвращает игрока на главную.
    final index = sections.indexWhere((section) => section.$1 == _section);
    final selected = index < 0 ? 0 : index;

    final person = widget.me?.person;

    return Scaffold(
      // Разделы держатся живыми: вернувшись на главную, игрок видит её сразу, а не заново
      // загружающийся экран.
      body: Column(
        children: [
          // Сетевой запрет действует во всех разделах сразу, поэтому и объясняется поверх всех,
          // а не на том экране, где человек первым получит отказ.
          if (person != null && person.networkBanned)
            NetworkBanNote(reason: person.networkBanReason),
          if (_note case final note?)
            PushNote(
              text: (note.body ?? note.title)!,
              onOpen: pushDestinationFor(note.template) == null
                  ? null
                  : () {
                      _dismissNote();
                      _followPush(note);
                    },
              onDismiss: _dismissNote,
            ),
          Expanded(
            child: IndexedStack(
              index: selected,
              children: [for (final (_, screen, _) in sections) screen],
            ),
          ),
        ],
      ),
      bottomNavigationBar: DecoratedBox(
        // Волосяная линия сверху: без неё панель сливается с прокрученным под неё списком, и
        // непонятно, где кончается содержимое и начинается навигация.
        decoration: BoxDecoration(
          border: Border(
            top: BorderSide(color: Theme.of(context).colorScheme.outline),
          ),
        ),
        child: NavigationBar(
          selectedIndex: selected,
          onDestinationSelected: (position) => _open(sections[position].$1),
          destinations: [for (final (_, _, tab) in sections) tab],
        ),
      ),
    );
  }
}
