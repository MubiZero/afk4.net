import 'package:flutter/material.dart';

import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import 'purchases_tab.dart';
import 'visits_tab.dart';

/// История: визиты и покупки. Две вкладки внутри одного раздела — это один вопрос игрока
/// «что у меня было», просто с двух сторон.
class HistoryScreen extends StatelessWidget {
  const HistoryScreen({super.key, required this.api, this.clock = DateTime.now});

  final PlayerApiClient api;
  final DateTime Function() clock;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);

    return DefaultTabController(
      length: 2,
      child: Scaffold(
        appBar: AppBar(
          title: Text(l.customerNavHistory),
          bottom: TabBar(
            tabs: [
              Tab(text: l.customerHistoryVisits),
              Tab(text: l.customerHistoryPurchases),
            ],
          ),
        ),
        body: TabBarView(
          children: [
            VisitsTab(api: api, clock: clock),
            PurchasesTab(api: api),
          ],
        ),
      ),
    );
  }
}
