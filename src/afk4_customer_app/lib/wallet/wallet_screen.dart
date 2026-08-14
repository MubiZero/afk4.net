import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../history/purchases_tab.dart';
import '../history/visits_tab.dart';
import '../l10n/app_localizations.dart';
import '../shell/app_scaffold.dart';
import '../shell/skeleton.dart';
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
    this.onPhoneVerified,
    this.clock = DateTime.now,
  });

  final PlayerApiClient api;
  final bool phoneVerified;
  final List<String>? features;
  final VoidCallback? onPhoneVerified;
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

  Future<void> _load() async {
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

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final data = _data;

    return DefaultTabController(
      length: 2,
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
                        debtBalance: data.debtBalance,
                        phoneVerified: widget.phoneVerified,
                        features: widget.features,
                        onPhoneVerified: widget.onPhoneVerified,
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
                  ],
                ),
              ),
            ),
          ],
          body: TabBarView(
            children: [
              VisitsTab(api: widget.api, clock: widget.clock),
              PurchasesTab(api: widget.api),
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
