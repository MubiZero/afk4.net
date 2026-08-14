import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

/// Заглушка формой будущего содержимого.
///
/// Спиннер посреди пустого экрана сообщает только «подожди». Скелет сообщает «сейчас здесь
/// будет карточка вот такого размера»: содержимое подставляется на своё место, экран не
/// прыгает, и ожидание кажется короче, чем оно есть.
class SkeletonBox extends StatefulWidget {
  const SkeletonBox({super.key, required this.height, this.width, this.radius});

  final double height;
  final double? width;
  final double? radius;

  @override
  State<SkeletonBox> createState() => _SkeletonBoxState();
}

class _SkeletonBoxState extends State<SkeletonBox> with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 1400),
  )..repeat();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final reduced = MediaQuery.of(context).disableAnimations;
    final base = theme.colorScheme.surfaceContainerHighest;
    final radius = BorderRadius.circular(widget.radius ?? AppTheme.radiusControl);

    if (reduced) {
      return Container(
        height: widget.height,
        width: widget.width,
        decoration: BoxDecoration(color: base, borderRadius: radius),
      );
    }

    return AnimatedBuilder(
      animation: _controller,
      builder: (context, _) {
        // Блик идёт слева направо и уходит за край: движение говорит «работа идёт», а не
        // «что-то мигает». Прозрачность блика низкая намеренно — скелет не должен спорить
        // с настоящим содержимым, которое вот-вот встанет на его место.
        final shift = _controller.value * 2 - 1;
        return Container(
          height: widget.height,
          width: widget.width,
          decoration: BoxDecoration(
            borderRadius: radius,
            gradient: LinearGradient(
              begin: Alignment(shift - 0.6, 0),
              end: Alignment(shift + 0.6, 0),
              colors: [base, theme.colorScheme.outline.withValues(alpha: 0.45), base],
            ),
          ),
        );
      },
    );
  }
}
