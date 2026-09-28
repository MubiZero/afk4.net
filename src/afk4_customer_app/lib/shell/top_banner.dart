import 'package:flutter/material.dart';

/// Общая вёрстка полосы уведомлений вверху экрана: заливка на всю ширину, иконка слева,
/// содержимое по центру, необязательные действия справа. `NetworkBanNote` (запрет сети) и
/// `PushNote` (пуш поверх экрана) — один и тот же каркас, разное содержимое и заливка.
class TopBanner extends StatelessWidget {
  const TopBanner({
    super.key,
    required this.color,
    required this.foreground,
    required this.icon,
    required this.content,
    this.actions = const [],
    this.padding = const EdgeInsets.fromLTRB(20, 12, 20, 12),
    this.crossAxisAlignment = CrossAxisAlignment.center,
  });

  final Color color;
  final Color foreground;
  final IconData icon;
  final Widget content;
  final List<Widget> actions;
  final EdgeInsetsGeometry padding;
  final CrossAxisAlignment crossAxisAlignment;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: color,
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: padding,
          child: Row(
            crossAxisAlignment: crossAxisAlignment,
            children: [
              Icon(icon, size: 18, color: foreground),
              const SizedBox(width: 8),
              Expanded(child: content),
              ...actions,
            ],
          ),
        ),
      ),
    );
  }
}
