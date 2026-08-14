import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../theme/app_theme.dart';

/// Оценка визита: пять звёзд и необязательный комментарий.
///
/// Звёзды крупные, комментарий необязателен, отправка — одна кнопка. Отзыв пишут стоя в
/// гардеробе, и всё, что длиннее одного экрана, не пишут вовсе.
class ReviewSheet extends StatefulWidget {
  const ReviewSheet({super.key, required this.api, required this.visit});

  final PlayerApiClient api;
  final PendingReview visit;

  @override
  State<ReviewSheet> createState() => _ReviewSheetState();
}

class _ReviewSheetState extends State<ReviewSheet> {
  final TextEditingController _comment = TextEditingController();
  int _rating = 0;
  bool _sending = false;
  String? _error;

  @override
  void dispose() {
    _comment.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final l = L.of(context);
    if (_rating == 0) return;

    setState(() {
      _sending = true;
      _error = null;
    });

    try {
      await widget.api.submitReview(
        sessionId: widget.visit.sessionId,
        rating: _rating,
        comment: _comment.text,
      );
      if (!mounted) return;
      Navigator.of(context).pop(true);
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      setState(() {
        // 409 — про этот вечер уже написано. Это не сбой отправки, и звать «повторить»
        // здесь было бы приглашением к тому, что снова не получится.
        _error = error.statusCode == 409 ? l.customerReviewDuplicate : l.customerReviewError;
        _sending = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Padding(
      padding: EdgeInsets.only(
        left: 20,
        right: 20,
        top: 20,
        bottom: MediaQuery.of(context).viewInsets.bottom + 20,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(l.customerReviewSheetTitle, style: theme.textTheme.titleLarge),
          const SizedBox(height: 4),
          Text(
            l.customerReviewInviteBody(widget.visit.seatName, widget.visit.branchName),
            style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
          const SizedBox(height: 20),
          _Stars(
            rating: _rating,
            onRating: _sending ? null : (value) => setState(() => _rating = value),
          ),
          const SizedBox(height: 20),
          TextField(
            controller: _comment,
            enabled: !_sending,
            minLines: 2,
            maxLines: 4,
            maxLength: 1000,
            decoration: InputDecoration(labelText: l.customerReviewSheetComment),
          ),
          if (_error != null) ...[
            const SizedBox(height: 8),
            Text(_error!, style: TextStyle(color: theme.colorScheme.error)),
          ],
          const SizedBox(height: 12),
          SizedBox(
            height: AppTheme.primaryButtonHeight,
            child: FilledButton(
              // Без оценки отправлять нечего, и кнопка говорит, чего именно ждёт, — вместо
              // того чтобы просто быть серой.
              onPressed: _rating == 0 || _sending ? null : _submit,
              child: Text(switch ((_sending, _rating)) {
                (true, _) => l.customerReviewSheetSending,
                (_, 0) => l.customerReviewSheetPickRating,
                _ => l.customerReviewSheetSubmit,
              }),
            ),
          ),
        ],
      ),
    );
  }
}

/// Пять звёзд. Каждая — своя цель касания не меньше минимальной: промах по звезде ставит
/// не ту оценку, и это заметят все, кроме поставившего.
class _Stars extends StatelessWidget {
  const _Stars({required this.rating, required this.onRating});

  final int rating;
  final ValueChanged<int>? onRating;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        for (var star = 1; star <= 5; star++)
          Semantics(
            label: l.customerReviewStar(star),
            selected: rating == star,
            button: true,
            child: IconButton(
              iconSize: 40,
              onPressed: onRating == null ? null : () => onRating!(star),
              icon: Icon(
                star <= rating ? Icons.star_rounded : Icons.star_outline_rounded,
                color: star <= rating ? const Color(0xFFFFC53D) : theme.colorScheme.onSurfaceVariant,
              ),
            ),
          ),
      ],
    );
  }
}
