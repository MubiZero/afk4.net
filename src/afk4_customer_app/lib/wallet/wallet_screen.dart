import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../history/ledger_tab.dart';
import '../history/purchases_tab.dart';
import '../history/visits_tab.dart';
import '../l10n/app_localizations.dart';
import '../organization/branch_choice.dart';
import '../shell/app_scaffold.dart';
import '../shell/new_club_note.dart';
import '../shell/skeleton.dart';
import 'top_up_sheet.dart';
import 'wallet_card.dart';

/// Раздел денег: сколько есть, как пополнить и куда ушло.
///
/// Раньше баланс жил карточкой на главной, а траты — в отдельном разделе «История», и на
/// вопрос «куда делись деньги» игрок отвечал переключением между двумя местами. Деньги и их
/// движение — один вопрос, поэтому и раздел один: сверху остаток, под ним визиты и покупки.
/// Так устроены банковские приложения, и привычка у игрока уже оттуда.
class WalletScreen extends StatefulWidget {
  const WalletScreen({
    super.key,
    required this.api,
    required this.phoneVerified,
    required this.features,
    this.accountOpen = true,
    this.branch = const BranchChoice(),
    this.currencyCode = 'TJS',
    this.onPhoneVerified,
    this.onAccountOpened,
    this.clock = DateTime.now,
  });

  final PlayerApiClient api;
  final bool phoneVerified;
  final List<String>? features;

  /// Есть ли у игрока счёт в этом клубе. Пока нет — показывать нечего, и спрашивать сервер
  /// не о чем: деньги появятся вместе со счётом.
  final bool accountOpen;

  /// Зал, в котором открыть счёт первым пополнением. Со счётом не нужен: кошелёк уже в зале.
  final BranchChoice branch;

  /// Валюта сети из каталога клубов. Нужна только до первого пополнения: дальше валюту
  /// называет сам кошелёк, а он появляется вместе со счётом.
  final String currencyCode;

  final VoidCallback? onPhoneVerified;
  final Future<void> Function()? onAccountOpened;
  final DateTime Function() clock;

  @override
  State<WalletScreen> createState() => _WalletScreenState();
}

class _WalletScreenState extends State<WalletScreen> {
  PlayerDashboard? _data;
  bool _failed = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  /// Счёт открылся первым действием — бронью или пополнением, — и теперь клубу есть что
  /// рассказать. Без этого на месте кошелька до перезапуска приложения оставался скелет:
  /// обещание карточки, которая уже не придёт.
  @override
  void didUpdateWidget(WalletScreen oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.accountOpen && !oldWidget.accountOpen) _load();
  }

  Future<void> _load() async {
    if (!widget.accountOpen) return;
    try {
      final data = await widget.api.getDashboard();
      if (mounted) {
        setState(() {
          _data = data;
          _failed = false;
        });
      }
    } on PlayerApiException {
      // Показанный остаток не стирается сбоем сети: цифра с прошлого удачного ответа полезнее
      // пустого места. А вот вечно мерцающий скелет на её месте — обещание карточки, которая
      // уже не придёт, поэтому вместо него встаёт строка с повтором. Списки под ней сообщают
      // о своих ошибках сами.
      if (mounted) setState(() => _failed = _data == null);
    }
  }

  /// Список возможностей не загрузился (`null`) — считаем пополнение включённым: право на
  /// запись всё равно проверяет сервер, а спрятанная из-за сбоя кнопка выглядит как поломка.
  bool get _topUpEnabled => widget.features == null || widget.features!.contains('online_topup');

  /// Первое пополнение в клубе: счёта ещё нет, поэтому ни остатка, ни прошлых заявок здесь
  /// не бывает — только сумма и зал, в котором клуб заведёт кошелёк.
  Future<void> _openFirstTopUp() async {
    final l = L.of(context);
    final sent = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (_) => TopUpSheet(
        api: widget.api,
        currencyCode: widget.currencyCode,
        intents: const [],
        branch: widget.branch,
      ),
    );
    if (sent != true || !mounted) return;

    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(l.customerWalletSent)));
    // Заявкой счёт и открылся — оболочке пора перечитать клубы, иначе раздел останется
    // пустым при уже существующем кошельке.
    await widget.onAccountOpened?.call();
    if (mounted) await _load();
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final data = _data;

    // Клуб, где счёта ещё нет, не о чем спрашивать: ни остатка, ни визитов, ни покупок.
    // Вкладки истории здесь показали бы два пустых списка или две ошибки. Пополнить при этом
    // можно — этим счёт и открывается, наравне с первой бронью.
    if (!widget.accountOpen) {
      return Scaffold(
        body: CustomScrollView(
          slivers: [
            appHeader(context, title: l.customerNavWallet),
            SliverPadding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
              sliver: SliverList.list(children: [
                const NewClubNote(),
                if (_topUpEnabled && widget.phoneVerified) ...[
                  const SizedBox(height: 16),
                  Align(
                    alignment: Alignment.centerLeft,
                    child: FilledButton.icon(
                      onPressed: _openFirstTopUp,
                      icon: const Icon(Icons.add, size: 20),
                      label: Text(l.customerWalletTopUp),
                    ),
                  ),
                ],
              ]),
            ),
          ],
        ),
      );
    }

    return DefaultTabController(
      length: 3,
      child: Scaffold(
        body: NestedScrollView(
          headerSliverBuilder: (context, _) => [
            appHeader(context, title: l.customerNavWallet),
            SliverToBoxAdapter(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
                child: data == null && _failed
                    ? _BalanceFailed(onRetry: _load)
                    : data == null
                    ? const SkeletonBox(height: 188, radius: 24)
                    : WalletCard(
                        api: widget.api,
                        walletBalance: data.walletBalance,
                        heldBalance: data.heldBalance,
                        debtBalance: data.debtBalance,
                        phoneVerified: widget.phoneVerified,
                        features: widget.features,
                        onPhoneVerified: widget.onPhoneVerified,
                        onToppedUp: widget.onAccountOpened,
                      ),
              ),
            ),
            SliverPersistentHeader(
              pinned: true,
              delegate: _TabBarHeader(
                background: theme.canvasColor,
                tabBar: TabBar(
                  tabs: [
                    Tab(text: l.customerHistoryVisits),
                    Tab(text: l.customerHistoryPurchases),
                    Tab(text: l.customerWalletLedgerTab),
                  ],
                ),
              ),
            ),
          ],
          body: TabBarView(
            children: [
              VisitsTab(api: widget.api, clock: widget.clock),
              PurchasesTab(api: widget.api),
              LedgerTab(api: widget.api),
            ],
          ),
        ),
      ),
    );
  }
}

/// Остаток не загрузился. Своя ошибка и свой повтор: списки трат под ней живут отдельно и
/// сбоем баланса не затрагиваются.
class _BalanceFailed extends StatelessWidget {
  const _BalanceFailed({required this.onRetry});

  final Future<void> Function() onRetry;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Row(
      children: [
        Expanded(
          child: Text(
            l.customerDashboardLoadError,
            style: TextStyle(color: theme.colorScheme.error),
          ),
        ),
        TextButton(onPressed: onRetry, child: Text(l.customerCommonRetry)),
      ],
    );
  }
}

/// Прилипшая полоса вкладок. Своя подложка обязательна: без неё сквозь прилипший
/// переключатель просвечивает уезжающий под ним список.
class _TabBarHeader extends SliverPersistentHeaderDelegate {
  _TabBarHeader({required this.tabBar, required this.background});

  final TabBar tabBar;
  final Color background;

  @override
  double get minExtent => tabBar.preferredSize.height;

  @override
  double get maxExtent => tabBar.preferredSize.height;

  @override
  Widget build(BuildContext context, double shrinkOffset, bool overlapsContent) =>
      ColoredBox(color: background, child: tabBar);

  @override
  bool shouldRebuild(_TabBarHeader oldDelegate) =>
      oldDelegate.tabBar != tabBar || oldDelegate.background != background;
}
