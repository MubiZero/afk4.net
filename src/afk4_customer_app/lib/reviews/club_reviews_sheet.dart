import 'package:flutter/material.dart';

import '../api/contracts.dart';
import '../format/date_time.dart';
import '../l10n/app_localizations.dart';
import '../organization/organization.dart';
import '../organization/organization_directory.dart';
import '../shell/load_failure.dart';
import '../theme/app_palette.dart';

/// Отзывы о клубе — то, что читают до входа. Открывается из карточки клуба по оценке:
/// цифра «4,6» отвечает «насколько хорошо», а на «почему» отвечают только слова игроков.
class ClubReviewsSheet extends StatefulWidget {
  const ClubReviewsSheet({super.key, required this.directory, required this.club});

  final OrganizationDirectory directory;
  final Organization club;

  @override
  State<ClubReviewsSheet> createState() => _ClubReviewsSheetState();
}

class _ClubReviewsSheetState extends State<ClubReviewsSheet> {
  ClubReviewsPageDto? _reviews;
  bool _failed = false;
  bool _offline = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _failed = false;
      _offline = false;
    });
    try {
      final reviews = await widget.directory.reviews(widget.club.organizationId);
      if (!mounted) return;
      setState(() => _reviews = reviews);
    } on OrganizationDirectoryException catch (error) {
      if (!mounted) return;
      setState(() {
        _offline = error.isOffline;
        _failed = true;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final reviews = _reviews;

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 20),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(l.customerReviewsTitle, style: theme.textTheme.titleLarge),
            const SizedBox(height: 4),
            Text(
              widget.club.name,
              style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 16),
            if (_failed)
              _offline
                  ? LoadFailure.offline(message: l.customerErrorOffline, onRetry: _load)
                  : LoadFailure(message: l.customerReviewsError, onRetry: _load)
            else if (reviews == null)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 24),
                child: Center(child: CircularProgressIndicator()),
              )
            else if (reviews.items.isEmpty)
              Text(
                l.customerReviewsEmpty,
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: theme.colorScheme.onSurfaceVariant,
                ),
              )
            else
              Flexible(
                child: ListView.separated(
                  shrinkWrap: true,
                  itemCount: reviews.items.length,
                  separatorBuilder: (_, _) => const Divider(height: 24),
                  itemBuilder: (context, index) => _ReviewTile(review: reviews.items[index]),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _ReviewTile extends StatelessWidget {
  const _ReviewTile({required this.review});

  final ClubReviewDto review;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(child: Text(review.authorName, style: theme.textTheme.titleSmall)),
            for (var star = 1; star <= 5; star++)
              Icon(
                star <= review.rating ? Icons.star_rounded : Icons.star_outline_rounded,
                size: 16,
                color: star <= review.rating
                    ? AppPalette.of(context).rating
                    : theme.colorScheme.onSurfaceVariant,
              ),
          ],
        ),
        Text(
          formatDateTime(L.of(context), review.createdAtUtc, locale),
          style: theme.textTheme.labelSmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
        ),
        if (review.comment != null && review.comment!.isNotEmpty) ...[
          const SizedBox(height: 6),
          Text(review.comment!, style: theme.textTheme.bodyMedium),
        ],
        // Текст скрыл клуб (оскорбление, чужой телефон) — звёзды остались, об этом и говорим.
        if (review.commentHidden == true) ...[
          const SizedBox(height: 6),
          Text(
            L.of(context).customerReviewsHidden,
            style: theme.textTheme.bodySmall?.copyWith(
              color: theme.colorScheme.onSurfaceVariant,
              fontStyle: FontStyle.italic,
            ),
          ),
        ],
        if (review.clubReply != null && review.clubReply!.isNotEmpty) ...[
          const SizedBox(height: 8),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.fromLTRB(12, 8, 12, 8),
            decoration: BoxDecoration(
              color: theme.colorScheme.surfaceContainerHighest,
              border: Border(left: BorderSide(color: theme.colorScheme.primary, width: 3)),
              borderRadius: const BorderRadius.horizontal(right: Radius.circular(8)),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  L.of(context).customerReviewsClubReply,
                  style: theme.textTheme.labelMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                ),
                const SizedBox(height: 2),
                Text(review.clubReply!, style: theme.textTheme.bodyMedium),
              ],
            ),
          ),
        ],
      ],
    );
  }
}
