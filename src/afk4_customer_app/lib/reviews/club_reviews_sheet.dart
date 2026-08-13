import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../format/date_time.dart';
import '../l10n/app_localizations.dart';
import '../organization/organization.dart';
import '../organization/organization_directory.dart';

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
  ClubReviews? _reviews;
  bool _failed = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _failed = false);
    try {
      final reviews = await widget.directory.reviews(widget.club.organizationId);
      if (!mounted) return;
      setState(() => _reviews = reviews);
    } on OrganizationDirectoryException {
      if (!mounted) return;
      setState(() => _failed = true);
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
              Row(
                children: [
                  Expanded(
                    child: Text(l.customerReviewsError,
                        style: TextStyle(color: theme.colorScheme.error)),
                  ),
                  TextButton(onPressed: _load, child: Text(l.customerCommonRetry)),
                ],
              )
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

  final ClubReview review;

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
                    ? const Color(0xFFFFC53D)
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
      ],
    );
  }
}
