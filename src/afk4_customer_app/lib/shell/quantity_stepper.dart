import 'package:flutter/material.dart';

import '../theme/space.dart';

/// Сколько штук: минус, число, плюс.
///
/// Степпер был в двух видах — плоские значки в корзине бара и обведённые в числе мест брони.
/// Один вид на все места: обведённые кнопки полной зоны касания и число между ними.
///
/// [collapsed] — пока ничего не выбрано, стоит один плюс: у меню из десяти позиций десять
/// «− 0 +» читались бы шумом.
class QuantityStepper extends StatelessWidget {
  const QuantityStepper({
    super.key,
    required this.value,
    required this.onChanged,
    required this.decreaseLabel,
    required this.increaseLabel,
    this.min = 0,
    this.max,
    this.collapsed = false,
  });

  final int value;
  final int min;
  final int? max;

  /// null — менять сейчас нельзя.
  final ValueChanged<int>? onChanged;

  /// Подсказки кнопок для читалки и наведения: «Меньше мест», «Добавить: Кола».
  final String decreaseLabel;
  final String increaseLabel;
  final bool collapsed;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final change = onChanged;
    final top = max;
    final plus = IconButton.outlined(
      onPressed: change == null || (top != null && value >= top) ? null : () => change(value + 1),
      icon: const Icon(Icons.add),
      tooltip: increaseLabel,
    );
    if (collapsed && value <= min) return plus;

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        IconButton.outlined(
          onPressed: change == null || value <= min ? null : () => change(value - 1),
          icon: const Icon(Icons.remove),
          tooltip: decreaseLabel,
        ),
        // Ширина под два разряда: число не толкает кнопки при переходе с 9 на 10.
        ConstrainedBox(
          constraints: const BoxConstraints(minWidth: Space.s8 + Space.s3),
          child: Text('$value', textAlign: TextAlign.center, style: theme.textTheme.titleLarge),
        ),
        plus,
      ],
    );
  }
}
