import 'package:flutter/material.dart';

import 'app_theme.dart';

/// Свет зала под всем приложением.
///
/// Ровная чёрная заливка на телефоне читается как «выключено»; два мягких пятна — фирменное
/// зелёное сверху и холодное снизу — дают экрану глубину и держат акцент, даже когда на
/// экране одни карточки. Рисуется один раз под навигатором: перекрашивать его на каждом
/// экране означало бы видеть шов при переходах.
class AmbientBackground extends StatelessWidget {
  const AmbientBackground({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final dark = theme.brightness == Brightness.dark;
    // Свет берётся из акцента темы, а не из фирменного emerald напрямую: у клуба может быть
    // свой цвет, и зелёное зарево под красным интерфейсом выглядело бы чужим.
    final accent = theme.colorScheme.primary;

    return DecoratedBox(
      decoration: BoxDecoration(color: theme.canvasColor),
      child: Stack(
        children: [
          if (dark) ...[
            _Glow(
              alignment: const Alignment(-0.9, -1),
              color: accent.withValues(alpha: 0.22),
              size: 1.15,
            ),
            _Glow(
              alignment: const Alignment(1.1, -0.75),
              color: AppTheme.violet.withValues(alpha: 0.16),
              size: 0.9,
            ),
            _Glow(
              alignment: const Alignment(0, 1.25),
              color: accent.withValues(alpha: 0.10),
              size: 1.3,
            ),
          ] else
            _Glow(
              alignment: const Alignment(-0.9, -1),
              color: accent.withValues(alpha: 0.10),
              size: 1.1,
            ),
          child,
        ],
      ),
    );
  }
}

class _Glow extends StatelessWidget {
  const _Glow({required this.alignment, required this.color, required this.size});

  final Alignment alignment;
  final Color color;

  /// Доля ширины экрана: пятно всегда пропорционально устройству, а не задано в пикселях.
  final double size;

  @override
  Widget build(BuildContext context) {
    final width = MediaQuery.sizeOf(context).width * size;

    return Align(
      alignment: alignment,
      child: IgnorePointer(
        child: Container(
          width: width,
          height: width,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            gradient: RadialGradient(colors: [color, color.withValues(alpha: 0)]),
          ),
        ),
      ),
    );
  }
}
