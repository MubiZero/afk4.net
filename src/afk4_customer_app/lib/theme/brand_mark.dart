import 'package:flutter/material.dart';

import '../app.dart';
import 'app_theme.dart';

/// Знак продукта: светящийся квадрат с монограммой и название рядом.
///
/// Стоит на экранах до входа — выбор клуба и вход. Игрок должен видеть, что открыл приложение
/// AFK4, а не безымянную форму: название клуба на этих экранах меняется, знак — нет.
class BrandMark extends StatelessWidget {
  const BrandMark({super.key});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final dark = theme.brightness == Brightness.dark;

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 38,
          height: 38,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(12),
            gradient: const LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: [AppTheme.emeraldBright, AppTheme.emerald],
            ),
            boxShadow: dark ? AppTheme.accentGlow(AppTheme.emerald) : null,
          ),
          child: const Text(
            'A4',
            style: TextStyle(
              color: Color(0xFF04120D),
              fontWeight: FontWeight.w800,
              fontSize: 15,
              letterSpacing: -0.5,
            ),
          ),
        ),
        const SizedBox(width: 10),
        Text(
          brandName,
          style: theme.textTheme.titleMedium?.copyWith(
            fontWeight: FontWeight.w700,
            letterSpacing: 0.5,
          ),
        ),
      ],
    );
  }
}
