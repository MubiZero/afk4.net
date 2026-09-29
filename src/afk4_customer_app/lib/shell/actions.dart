import 'package:flutter/material.dart';

import '../theme/app_theme.dart';
import '../theme/space.dart';

/// Кнопки по роли, а не по месту.
///
/// Одна и та же роль жила в трёх видах: главная то во всю ширину, то по тексту у левого края,
/// то прибитая к низу; высоты 54 и 48 шли вперемешку. Игрок учит приложение по форме кнопки —
/// и форма обязана значить одно и то же на любом экране:
/// - главное действие — заливка во всю ширину, 54;
/// - второе — обводка во всю ширину, 48;
/// - третье — текст по центру, 48.
///
/// [danger] — действие разрушает: красный текст и обводка. [irreversible] — разрушает насовсем:
/// красная заливка. Такая кнопка главная по смыслу — человек пришёл именно за этим, — но цвет
/// честно говорит, что обратно дороги нет.
class AppAction {
  const AppAction(
    this.label,
    this.onPressed, {
    this.icon,
    this.danger = false,
    this.irreversible = false,
  });

  final String label;

  /// null — действие сейчас недоступно. Почему — говорит [ActionStack.hint], а не подпись
  /// выключенной кнопки: серый текст на серой заливке читается хуже всего на экране.
  final VoidCallback? onPressed;
  final IconData? icon;
  final bool danger;
  final bool irreversible;
}

/// Кнопка подтверждения в диалоге, когда подтверждают необратимое: удалить учётную запись,
/// удалить друга, отменить бронь или участие. Красная заливка — та же, что у
/// [AppAction.irreversible]: раньше одно такое подтверждение было зелёным, другое — текстом, и
/// «Удалить» выглядело так же, как «Сохранить».
ButtonStyle irreversibleConfirmStyle(BuildContext context) {
  final scheme = Theme.of(context).colorScheme;
  return FilledButton.styleFrom(backgroundColor: scheme.error, foregroundColor: scheme.onError);
}

/// Главная кнопка: заливка во всю ширину.
class PrimaryButton extends StatelessWidget {
  const PrimaryButton({super.key, required this.action});

  final AppAction action;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final style = FilledButton.styleFrom(
      minimumSize: const Size.fromHeight(AppTheme.primaryButtonHeight),
      backgroundColor: action.danger || action.irreversible ? scheme.error : null,
      foregroundColor: action.danger || action.irreversible ? scheme.onError : null,
    );
    final icon = action.icon;
    return icon == null
        ? FilledButton(style: style, onPressed: action.onPressed, child: _Label(action.label))
        : FilledButton.icon(
            style: style,
            onPressed: action.onPressed,
            icon: Icon(icon, size: 20),
            label: _Label(action.label),
          );
  }
}

/// Второе действие: обводка во всю ширину.
class SecondaryButton extends StatelessWidget {
  const SecondaryButton({super.key, required this.action});

  final AppAction action;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    if (action.irreversible) return PrimaryButton(action: action);
    final style = OutlinedButton.styleFrom(
      minimumSize: const Size.fromHeight(AppTheme.minTouchTarget),
      foregroundColor: action.danger ? scheme.error : null,
      side: action.danger ? BorderSide(color: scheme.error) : null,
    );
    final icon = action.icon;
    return icon == null
        ? OutlinedButton(style: style, onPressed: action.onPressed, child: _Label(action.label))
        : OutlinedButton.icon(
            style: style,
            onPressed: action.onPressed,
            icon: Icon(icon, size: 20),
            label: _Label(action.label),
          );
  }
}

/// Третье действие: текст по центру, во всю ширину касания.
class TertiaryButton extends StatelessWidget {
  const TertiaryButton({super.key, required this.action});

  final AppAction action;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final style = TextButton.styleFrom(
      minimumSize: const Size.fromHeight(AppTheme.minTouchTarget),
      foregroundColor: action.danger || action.irreversible ? scheme.error : null,
    );
    final icon = action.icon;
    return icon == null
        ? TextButton(style: style, onPressed: action.onPressed, child: _Label(action.label))
        : TextButton.icon(
            style: style,
            onPressed: action.onPressed,
            icon: Icon(icon, size: 20),
            label: _Label(action.label),
          );
  }
}

/// Подпись кнопки переносится, а не обрезается: на крупном шрифте и длинном языке «Пополнить
/// баланс» не должно превращаться в «Пополнить ба…».
class _Label extends StatelessWidget {
  const _Label(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Text(text, textAlign: TextAlign.center);
}

/// Стопка действий блока: до трёх кнопок в порядке важности, над ними — причина и ошибка.
///
/// [hint] говорит, чего ждёт выключенная главная кнопка или что будет после нажатия. Раньше это
/// жило внутри самой кнопки («Введите код»), серым по серому с контрастом 2,2:1, и кнопка
/// переставала называть действие. [error] — почему последняя попытка не удалась: рядом с
/// кнопкой, а не всплывашкой, которая уйдёт раньше, чем её прочтут.
class ActionStack extends StatelessWidget {
  const ActionStack({
    super.key,
    this.primary,
    this.secondary,
    this.tertiary,
    this.hint,
    this.error,
  });

  final AppAction? primary;
  final AppAction? secondary;
  final AppAction? tertiary;
  final String? hint;
  final String? error;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final buttons = <Widget>[
      if (primary case final action?) PrimaryButton(action: action),
      if (secondary case final action?) SecondaryButton(action: action),
      if (tertiary case final action?) TertiaryButton(action: action),
    ];

    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (error case final text?) ...[
          Text(text, style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.error)),
          const SizedBox(height: Space.s2),
        ],
        if (hint case final text?) ...[
          Text(
            text,
            textAlign: TextAlign.center,
            style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
          const SizedBox(height: Space.s2),
        ],
        for (final (index, button) in buttons.indexed) ...[
          if (index > 0) const SizedBox(height: Space.s2),
          button,
        ],
      ],
    );
  }
}

/// Стопка, прибитая к низу экрана формы: главное действие всегда под пальцем, сколько бы ни
/// было полей выше. Отступ от жестовой полосы телефона — [SafeArea].
class PinnedActions extends StatelessWidget {
  const PinnedActions({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) => SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(Space.screen, Space.s2, Space.screen, Space.s4),
          child: child,
        ),
      );
}
