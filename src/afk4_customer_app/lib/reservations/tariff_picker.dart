import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';

/// Выбор тарифа и цена брони по нему.
///
/// Цена приходит с сервера, а не считается здесь: минимальное оплачиваемое время и шаг
/// округления живут в биллинге. Приложение, умножающее минуты на цену, показало бы одну сумму,
/// а списалась бы другая — и это худший вид расхождения, какой может быть в деньгах.
class TariffPicker extends StatelessWidget {
  const TariffPicker({
    super.key,
    required this.tariffs,
    required this.selectedId,
    required this.quote,
    required this.quoting,
    required this.problem,
    required this.onSelected,
  });

  final List<TariffOption> tariffs;
  final String? selectedId;

  /// Посчитанная сервером стоимость. null — время ещё не выбрано или расчёт не удался.
  final ReservationQuote? quote;
  final bool quoting;

  /// Что пошло не так с расчётом: тариф сняли с публикации или сервер не ответил.
  final String? problem;

  final ValueChanged<String> onSelected;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;

    // Клуб не завёл прайс — это нормальная жизнь маленького клуба, а не поломка. Бронь
    // остаётся возможной, просто считают её на стойке.
    if (tariffs.isEmpty) {
      return Text(
        l.customerReservationsTariffNone,
        style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(l.customerReservationsTariff, style: theme.textTheme.titleSmall),
        const SizedBox(height: 8),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            for (final tariff in tariffs)
              ChoiceChip(
                label: Text(tariff.name),
                selected: tariff.tariffVersionId == selectedId,
                onSelected: (_) => onSelected(tariff.tariffVersionId),
              ),
          ],
        ),
        const SizedBox(height: 8),
        if (problem != null)
          Text(problem!, style: TextStyle(color: theme.colorScheme.error))
        else if (quoting)
          Text(
            l.customerReservationsTariffHint,
            style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          )
        else if (quote != null) ...[
          Text(
            l.customerReservationsPrice(
              formatMoney(quote!.amountMinorUnits, quote!.currencyCode, locale: locale),
            ),
            style: theme.textTheme.titleMedium?.copyWith(color: theme.colorScheme.primary),
          ),
          // Минимум тарифа объясняется до подтверждения: час брони на тарифе с минимумом в два
          // часа стоит два часа, и узнавать это из чека — худший момент.
          if (quote!.hasMinimum)
            Text(
              l.customerReservationsPriceMinimum(
                l.customerReservationsPriceMinutes(quote!.billableMinutes),
              ),
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
          // Деньги замораживаются в момент брони. Молча уменьшить баланс значит получить
          // вопрос «куда делись деньги» вместо понятной брони.
          Text(
            l.customerReservationsHoldNote,
            style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
        ] else
          Text(
            l.customerReservationsTariffHint,
            style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
      ],
    );
  }
}
