import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../shell/app_scaffold.dart';
import '../theme/app_theme.dart';

/// Стаж игрока: уровень, часы за ПК и достижения.
///
/// История визитов отвечает «когда я приходил», а этот экран — «сколько я всего наиграл».
/// Второй вопрос игроку интереснее, и до сих пор ответа на него в приложении не было.
class ProgressScreen extends StatefulWidget {
  const ProgressScreen({super.key, required this.api});

  final PlayerApiClient api;

  @override
  State<ProgressScreen> createState() => _ProgressScreenState();
}

class _ProgressScreenState extends State<ProgressScreen> {
  PlayerAchievements? _data;
  bool _failed = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _failed = false);
    try {
      final data = await widget.api.getAchievements();
      if (!mounted) return;
      setState(() => _data = data);
    } on PlayerApiException {
      if (!mounted) return;
      setState(() => _failed = true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final data = _data;

    return AppScaffold(
      title: l.customerProgressTitle,
      onRefresh: _load,
      slivers: [
        SliverPadding(
          padding: sectionPadding,
          sliver: SliverList.list(
            children: [
              if (_failed && data == null) ...[
                Text(l.customerProgressError, style: TextStyle(color: theme.colorScheme.error)),
                const SizedBox(height: 8),
                Align(
                  alignment: Alignment.centerLeft,
                  child: TextButton(onPressed: _load, child: Text(l.customerCommonRetry)),
                ),
              ] else if (data == null)
                const Padding(
                  padding: EdgeInsets.symmetric(vertical: 32),
                  child: Center(child: CircularProgressIndicator()),
                )
              else ...[
                LevelCard(data: data),
                const SizedBox(height: 20),
                Text(l.customerProgressAchievements, style: theme.textTheme.titleMedium),
                const SizedBox(height: 12),
                for (final achievement in data.achievements) ...[
                  _AchievementTile(achievement: achievement),
                  const SizedBox(height: 10),
                ],
              ],
            ],
          ),
        ),
      ],
    );
  }
}

/// Карточка уровня. Часы за ПК — то, что игрок про себя знает и без нас; ценность в том, что
/// они сложены и названы уровнем, а до следующего видно, сколько осталось.
class LevelCard extends StatelessWidget {
  const LevelCard({super.key, required this.data});

  final PlayerAchievements data;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final hours = (data.playedMinutes / 60).floor();
    final toNext = data.minutesToNextLevel;

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppTheme.radiusCard),
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [
            theme.colorScheme.primary.withValues(alpha: 0.24),
            AppTheme.violet.withValues(alpha: 0.20),
          ],
        ),
        border: Border.all(color: theme.colorScheme.outline),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(Icons.military_tech_outlined, color: theme.colorScheme.primary),
              const SizedBox(width: 8),
              Text(
                l.customerProgressLevel('${data.level}'),
                style: theme.textTheme.headlineSmall,
              ),
            ],
          ),
          const SizedBox(height: 6),
          Text(
            l.customerProgressHours('$hours', data.visitCount),
            style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
          const SizedBox(height: 4),
          Text(
            // Достигнут потолок — так и говорим. «До следующего уровня 0 ч» звучало бы как
            // подсчёт, который вот-вот сорвётся.
            toNext == null
                ? l.customerProgressMaxLevel
                : l.customerProgressToNext('${((toNext + 59) / 60).floor()}'),
            style: theme.textTheme.labelLarge?.copyWith(color: theme.colorScheme.primary),
          ),
        ],
      ),
    );
  }
}

/// Одно достижение. Прогресс виден и до получения — иначе список выглядит как набор запертых
/// дверей без замочных скважин.
class _AchievementTile extends StatelessWidget {
  const _AchievementTile({required this.achievement});

  final PlayerAchievement achievement;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final unlocked = achievement.unlocked;
    final accent = unlocked ? theme.colorScheme.primary : theme.colorScheme.onSurfaceVariant;

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: theme.colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(AppTheme.radiusControl),
        border: Border.all(color: theme.colorScheme.outline),
      ),
      child: Row(
        children: [
          Container(
            width: 40,
            height: 40,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: accent.withValues(alpha: unlocked ? 0.18 : 0.08),
            ),
            child: Icon(_iconFor(achievement.code), size: 20, color: accent),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(_titleFor(l, achievement.code), style: theme.textTheme.titleSmall),
                const SizedBox(height: 2),
                Text(
                  _hintFor(l, achievement.code),
                  style: theme.textTheme.bodySmall
                      ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                ),
              ],
            ),
          ),
          const SizedBox(width: 10),
          if (unlocked)
            Icon(Icons.check_circle, color: theme.colorScheme.primary)
          else
            Text(
              l.customerProgressLocked('${achievement.progress}', '${achievement.target}'),
              style: theme.textTheme.labelMedium
                  ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
        ],
      ),
    );
  }

  /// Коды достижений сервер присылает кодами, а подписи живут здесь — у них три языка.
  /// Незнакомый код (сервер новее приложения) показывается своим кодом, а не прячется:
  /// пропавшее достижение выглядело бы как потерянное.
  static String _titleFor(L l, String code) => switch (code) {
        'first_visit' => l.customerAchievementFirstVisit,
        'regular' => l.customerAchievementRegular,
        'veteran' => l.customerAchievementVeteran,
        'night_owl' => l.customerAchievementNightOwl,
        'marathon' => l.customerAchievementMarathon,
        'reviewer' => l.customerAchievementReviewer,
        _ => code,
      };

  static String _hintFor(L l, String code) => switch (code) {
        'first_visit' => l.customerAchievementFirstVisitHint,
        'regular' => l.customerAchievementRegularHint,
        'veteran' => l.customerAchievementVeteranHint,
        'night_owl' => l.customerAchievementNightOwlHint,
        'marathon' => l.customerAchievementMarathonHint,
        'reviewer' => l.customerAchievementReviewerHint,
        _ => '',
      };

  static IconData _iconFor(String code) => switch (code) {
        'first_visit' => Icons.flag_outlined,
        'regular' => Icons.repeat_rounded,
        'veteran' => Icons.workspace_premium_outlined,
        'night_owl' => Icons.nightlight_outlined,
        'marathon' => Icons.timer_outlined,
        'reviewer' => Icons.rate_review_outlined,
        _ => Icons.emoji_events_outlined,
      };
}
