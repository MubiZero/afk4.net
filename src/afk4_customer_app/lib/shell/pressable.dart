import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

/// Нажатие, которое видно.
///
/// Крупные плитки и карточки нажимают пальцем, и одной ряби Material для них мало: палец
/// закрывает как раз то место, где она рисуется. Лёгкое проседание видно из-под пальца и
/// подтверждает, что касание попало, — та самая мелочь, по которой приложение читается как
/// живое, а не как картинка.
///
/// Плитка — такая же кнопка, как остальные, и с клавиатуры тоже: в веб-сборке её раньше нельзя
/// было выбрать Tab-ом (клуб на витрине, плитку на главной), и наведение мышью ничего не
/// меняло. Теперь она получает фокус, нажимается Enter и пробелом, у фокуса — видимое кольцо
/// акцентом, у наведения — лёгкая подсветка.
class Pressable extends StatefulWidget {
  const Pressable({super.key, required this.child, required this.onPressed, this.borderRadius});

  final Widget child;

  /// null — элемент не нажимается. Тогда и проседания нет: обещать отклик там, где ничего
  /// не произойдёт, хуже, чем не обещать.
  final VoidCallback? onPressed;

  /// Скругление подсветки и кольца фокуса — по форме того, что внутри. По умолчанию — как у
  /// плиток и полей.
  final BorderRadius? borderRadius;

  @override
  State<Pressable> createState() => _PressableState();
}

class _PressableState extends State<Pressable> {
  bool _down = false;
  bool _focused = false;
  bool _hovered = false;

  void _set(bool down) {
    if (widget.onPressed == null) return;
    setState(() => _down = down);
  }

  @override
  Widget build(BuildContext context) {
    // «Уменьшить движение» в системе — это просьба, а не пожелание: отклик остаётся, но
    // становится мгновенным, без анимации.
    final reduced = MediaQuery.of(context).disableAnimations;
    final enabled = widget.onPressed != null;
    final pressed = _down && enabled;
    final scheme = Theme.of(context).colorScheme;
    final radius = widget.borderRadius ?? BorderRadius.circular(AppTheme.radiusControl);

    return Semantics(
      button: enabled,
      child: FocusableActionDetector(
        enabled: enabled,
        mouseCursor: enabled ? SystemMouseCursors.click : MouseCursor.defer,
        onShowFocusHighlight: (value) => setState(() => _focused = value),
        onShowHoverHighlight: (value) => setState(() => _hovered = value),
        actions: {
          ActivateIntent: CallbackAction<ActivateIntent>(
            onInvoke: (_) {
              widget.onPressed?.call();
              return null;
            },
          ),
        },
        child: GestureDetector(
          onTapDown: (_) => _set(true),
          onTapUp: (_) => _set(false),
          onTapCancel: () => _set(false),
          onTap: widget.onPressed,
          child: AnimatedScale(
            scale: pressed ? 0.97 : 1,
            // 120 мс — верх бюджета микро-отклика: медленнее уже читается как задержка.
            duration: reduced ? Duration.zero : const Duration(milliseconds: 120),
            curve: Curves.easeOut,
            child: DecoratedBox(
              position: DecorationPosition.foreground,
              decoration: BoxDecoration(
                borderRadius: radius,
                color: _hovered && enabled ? scheme.onSurface.withValues(alpha: 0.04) : null,
                border: _focused && enabled ? Border.all(color: scheme.primary, width: 2) : null,
              ),
              child: widget.child,
            ),
          ),
        ),
      ),
    );
  }
}
