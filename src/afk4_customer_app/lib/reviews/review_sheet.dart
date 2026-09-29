import 'package:flutter/material.dart';

import '../api/contracts.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../theme/app_palette.dart';
import '../shell/actions.dart';
import '../shell/app_sheet.dart';
import '../theme/space.dart';

/// Оценка визита: пять звёзд и необязательный комментарий.
///
/// Звёзды крупные, комментарий необязателен, отправка — одна кнопка. Отзыв пишут стоя в
/// гардеробе, и всё, что длиннее одного экрана, не пишут вовсе.
class ReviewSheet extends StatefulWidget {
  const ReviewSheet({super.key, required this.api, required this.visit});

  final PlayerApiClient api;
  final PendingClubReviewDto visit;

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
        // Ни один из этих отказов не лечится повтором: про вечер уже написано, текст длиннее
        // разрешённого, визита нет. Общее «не удалось отправить» звало бы жать кнопку зря.
        _error = switch ((error.statusCode, error.message)) {
          (_, _) when error.isOffline => l.customerErrorOffline,
          (409, _) => l.customerReviewDuplicate,
          (400, 'comment_too_long') => l.customerReviewErrTooLong,
          (404, _) => l.customerReviewErrVisitGone,
          _ => l.customerReviewError,
        };
        _sending = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return AppSheet(
      title: l.customerReviewSheetTitle,
      content: [
        Text(
          l.customerReviewInviteBody(widget.visit.seatName, widget.visit.branchName),
          style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
        ),
        const SizedBox(height: Space.s5),
        _Stars(
          rating: _rating,
          onRating: _sending ? null : (value) => setState(() => _rating = value),
        ),
        const SizedBox(height: Space.s5),
        TextField(
          controller: _comment,
          enabled: !_sending,
          minLines: 2,
          maxLines: 4,
          maxLength: 1000,
          decoration: InputDecoration(labelText: l.customerReviewSheetComment),
        ),
      ],
      actions: ActionStack(
        error: _error,
        // Без оценки отправлять нечего — над кнопкой сказано, чего она ждёт, а сама она
        // по-прежнему называет действие.
        hint: _rating == 0 && !_sending ? l.customerReviewSheetPickRating : null,
        primary: AppAction(
          _sending ? l.customerReviewSheetSending : l.customerReviewSheetSubmit,
          _rating == 0 || _sending ? null : _submit,
        ),
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
                color: star <= rating ? AppPalette.of(context).rating : theme.colorScheme.onSurfaceVariant,
              ),
            ),
          ),
      ],
    );
  }
}
