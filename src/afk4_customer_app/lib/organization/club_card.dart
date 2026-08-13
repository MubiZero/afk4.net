import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';
import '../money/money.dart';
import '../shell/pressable.dart';
import '../theme/app_theme.dart';
import 'organization.dart';

/// Карточка клуба в витрине: фото зала, название поверх него, оценка, цена часа и адрес.
///
/// Клуб выбирают глазами. Строчка с названием отвечала только на вопрос «как называется» —
/// а игрок спрашивает «где это, сколько стоит и как там внутри». Поэтому карточка крупная и
/// начинается с фотографии: она отвечает на последний вопрос быстрее любого описания.
class ClubCard extends StatelessWidget {
  const ClubCard({super.key, required this.club, required this.onTap, this.onOpenReviews});

  final Organization club;
  final VoidCallback onTap;

  /// Открыть отзывы. null — читать нечего (или некому показать): тогда оценка остаётся
  /// подписью и не притворяется кнопкой.
  final VoidCallback? onOpenReviews;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l = L.of(context);

    return Pressable(
      onPressed: onTap,
      borderRadius: BorderRadius.circular(AppTheme.radiusCard),
      child: Container(
        clipBehavior: Clip.antiAlias,
        decoration: BoxDecoration(
          color: theme.colorScheme.surfaceContainerHighest,
          borderRadius: BorderRadius.circular(AppTheme.radiusCard),
          border: Border.all(color: theme.colorScheme.outline),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _Cover(club: club, onOpenReviews: onOpenReviews),
            Padding(
              padding: const EdgeInsets.fromLTRB(14, 12, 14, 14),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _AddressLine(club: club),
                  const SizedBox(height: 10),
                  // Цена и мест — то, ради чего игрок открывает карточку клуба на любом сервисе.
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      _Chip(
                        icon: Icons.payments_outlined,
                        label: club.pricePerHourFromMinorUnits == null
                            ? l.customerClubPickerPriceUnknown
                            : l.customerClubPickerPriceFrom(formatMoney(
                                club.pricePerHourFromMinorUnits!,
                                club.currencyCode ?? 'TJS',
                                locale: Localizations.localeOf(context).languageCode,
                              )),
                        highlighted: club.pricePerHourFromMinorUnits != null,
                      ),
                      if (club.seatCount > 0)
                        _Chip(
                          icon: Icons.desktop_windows_outlined,
                          label: l.customerClubPickerSeats(club.seatCount),
                        ),
                      if (club.places.length > 1)
                        _Chip(
                          icon: Icons.apartment_outlined,
                          label: l.customerClubPickerMorePlaces(club.places.length - 1),
                        ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Фото зала с названием поверх него. Фотографии может не быть — тогда её место занимает
/// фирменный градиент со знаком клуба: пустой серый прямоугольник читался бы как ошибка
/// загрузки, а не как «клуб не прислал фото».
class _Cover extends StatelessWidget {
  const _Cover({required this.club, this.onOpenReviews});

  final Organization club;
  final VoidCallback? onOpenReviews;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final cover = club.coverImageUrl;

    return SizedBox(
      height: 150,
      width: double.infinity,
      child: Stack(
        fit: StackFit.expand,
        children: [
          if (cover != null && cover.isNotEmpty)
            Image.network(
              cover,
              fit: BoxFit.cover,
              errorBuilder: (_, _, _) => _CoverFallback(club: club),
              loadingBuilder: (context, child, progress) =>
                  progress == null ? child : _CoverFallback(club: club),
            )
          else
            _CoverFallback(club: club),
          // Затемнение снизу: без него светлое фото съедает белое название.
          const DecoratedBox(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.center,
                end: Alignment.bottomCenter,
                colors: [Colors.transparent, Color(0xCC000000)],
              ),
            ),
          ),
          Positioned(
            left: 14,
            right: 14,
            bottom: 12,
            child: Text(
              club.name,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: theme.textTheme.titleLarge?.copyWith(
                color: Colors.white,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
          Positioned(
            top: 12,
            right: 12,
            child: _RatingBadge(club: club, onOpenReviews: onOpenReviews),
          ),
        ],
      ),
    );
  }
}

class _CoverFallback extends StatelessWidget {
  const _CoverFallback({required this.club});

  final Organization club;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final accent = theme.colorScheme.primary;
    final logo = club.logoUrl;

    return DecoratedBox(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [accent.withValues(alpha: 0.32), AppTheme.violet.withValues(alpha: 0.26)],
        ),
      ),
      child: Center(
        child: logo != null && logo.isNotEmpty
            ? Image.network(
                logo,
                height: 56,
                errorBuilder: (_, _, _) => _Monogram(club: club),
              )
            : _Monogram(club: club),
      ),
    );
  }
}

class _Monogram extends StatelessWidget {
  const _Monogram({required this.club});

  final Organization club;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Text(
      club.name.characters.first.toUpperCase(),
      style: theme.textTheme.displaySmall?.copyWith(
        color: Colors.white.withValues(alpha: 0.9),
        fontWeight: FontWeight.w700,
      ),
    );
  }
}

/// Оценка клуба. «Оценок пока нет» — не ноль звёзд: клуб, куда ещё никто не сходил, не хуже
/// клуба, который все ругают, и выглядеть хуже он не должен.
class _RatingBadge extends StatelessWidget {
  const _RatingBadge({required this.club, this.onOpenReviews});

  final Organization club;
  final VoidCallback? onOpenReviews;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l = L.of(context);
    final rating = club.rating;
    // Цифра отвечает «насколько хорошо», а на «почему» отвечают только слова игроков —
    // поэтому по оценке открываются отзывы. Пока их нет, открывать нечего.
    final openable = onOpenReviews != null && club.reviewCount > 0;

    final badge = DecoratedBox(
      decoration: BoxDecoration(
        color: const Color(0xCC000000),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              Icons.star_rounded,
              size: 16,
              color: rating == null ? Colors.white54 : const Color(0xFFFFC53D),
            ),
            const SizedBox(width: 4),
            Text(
              rating == null
                  ? l.customerClubPickerNoRating
                  : rating.toStringAsFixed(1).replaceAll('.', ','),
              style: theme.textTheme.labelMedium?.copyWith(color: Colors.white),
            ),
            if (rating != null && club.reviewCount > 0) ...[
              const SizedBox(width: 6),
              Text(
                '·  ${club.reviewCount}',
                style: theme.textTheme.labelSmall?.copyWith(color: Colors.white70),
              ),
            ],
          ],
        ),
      ),
    );

    if (!openable) return badge;

    return Semantics(
      button: true,
      label: l.customerReviewsTitle,
      child: GestureDetector(onTap: onOpenReviews, child: badge),
    );
  }
}

class _AddressLine extends StatelessWidget {
  const _AddressLine({required this.club});

  final Organization club;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final place = club.places.isEmpty ? null : club.places.first;
    if (place == null) return const SizedBox.shrink();

    final text = [place.city, place.address].where((part) => part != null && part.isNotEmpty).join(', ');
    if (text.isEmpty) return const SizedBox.shrink();

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(Icons.place_outlined, size: 16, color: theme.colorScheme.onSurfaceVariant),
        const SizedBox(width: 6),
        Expanded(
          child: Text(
            text,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
        ),
      ],
    );
  }
}

class _Chip extends StatelessWidget {
  const _Chip({required this.icon, required this.label, this.highlighted = false});

  final IconData icon;
  final String label;
  final bool highlighted;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final color = highlighted ? theme.colorScheme.primary : theme.colorScheme.onSurfaceVariant;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: highlighted
            ? theme.colorScheme.primary.withValues(alpha: 0.12)
            : theme.colorScheme.surface,
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: theme.colorScheme.outline),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 14, color: color),
            const SizedBox(width: 6),
            Text(label, style: theme.textTheme.labelMedium?.copyWith(color: color)),
          ],
        ),
      ),
    );
  }
}
