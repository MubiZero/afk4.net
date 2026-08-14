import 'package:flutter/material.dart';

/// Нажатие, которое видно.
///
/// Крупные плитки и карточки нажимают пальцем, и одной ряби Material для них мало: палец
/// закрывает как раз то место, где она рисуется. Лёгкое проседание видно из-под пальца и
/// подтверждает, что касание попало, — та самая мелочь, по которой приложение читается как
/// живое, а не как картинка.
class Pressable extends StatefulWidget {
  const Pressable({super.key, required this.child, required this.onPressed, this.borderRadius});

  final Widget child;

  /// null — элемент не нажимается. Тогда и проседания нет: обещать отклик там, где ничего
  /// не произойдёт, хуже, чем не обещать.
  final VoidCallback? onPressed;

  final BorderRadius? borderRadius;

  @override
  State<Pressable> createState() => _PressableState();
}

class _PressableState extends State<Pressable> {
  bool _down = false;

  void _set(bool down) {
    if (widget.onPressed == null) return;
    setState(() => _down = down);
  }

  @override
  Widget build(BuildContext context) {
    // «Уменьшить движение» в системе — это просьба, а не пожелание: отклик остаётся, но
    // становится мгновенным, без анимации.
    final reduced = MediaQuery.of(context).disableAnimations;
    final pressed = _down && widget.onPressed != null;

    return Semantics(
      button: widget.onPressed != null,
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
          child: widget.child,
        ),
      ),
    );
  }
}
