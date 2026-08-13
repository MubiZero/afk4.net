import 'package:flutter/material.dart';

/// Знак продукта — перенос `brand/afk4-mark.svg` на виджеты.
///
/// Сетка 3×3 «командной решётки» с активной диагональю: те же ячейки, те же цвета, те же
/// пропорции, что в мастер-файле бренда. Рисуется кодом, а не подключается картинкой: знак
/// состоит из девяти прямоугольников, и лишний ассет со своей загрузкой здесь ничего не даёт.
///
/// Цвета берутся из `brand/README.md`, а не из темы приложения: знак принадлежит бренду и не
/// перекрашивается вместе с интерфейсом.
class BrandMark extends StatelessWidget {
  const BrandMark({super.key, this.size = 38, this.showWordmark = true});

  /// Сторона знака. Словесная часть масштабируется от неё.
  final double size;

  /// Знак без слова — для мест, где название уже написано рядом.
  final bool showWordmark;

  /// Акцент на тёмных поверхностях.
  static const Color accent = Color(0xFF2DD4A7);

  /// Погашенная ячейка сетки.
  static const Color inactive = Color(0xFF173028);

  /// Подложка знака.
  static const Color tile = Color(0xFF0E2019);

  /// Цвет словесного знака на тёмном.
  static const Color wordmark = Color(0xFFE2F1EC);

  /// Активны ячейки по диагонали: слева сверху, центр, справа снизу.
  static bool isActive(int row, int column) => row == column;

  @override
  Widget build(BuildContext context) {
    final grid = SizedBox(
      width: size,
      height: size,
      child: CustomPaint(painter: _MarkPainter()),
    );

    if (!showWordmark) return grid;

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        grid,
        SizedBox(width: size * 0.3),
        // Словесный знак всегда заглавными, `.NET` — акцентом. Правило бренда, не оформление.
        Text.rich(
          TextSpan(
            children: const [
              TextSpan(text: 'AFK4'),
              TextSpan(text: '.NET', style: TextStyle(color: accent)),
            ],
            style: TextStyle(
              color: wordmark,
              fontSize: size * 0.44,
              fontWeight: FontWeight.w700,
              letterSpacing: 0.4,
            ),
          ),
        ),
      ],
    );
  }
}

class _MarkPainter extends CustomPainter {
  /// Пропорции мастер-файла: поле 52, ячейка 13, шаг 17, отступ 3, скругление 3.5.
  static const double _canvas = 52;
  static const double _cell = 13;
  static const double _step = 17;
  static const double _inset = 3;
  static const double _radius = 3.5;

  @override
  void paint(Canvas canvas, Size size) {
    final scale = size.width / _canvas;
    final tile = Paint()..color = BrandMark.tile;
    canvas.drawRRect(
      RRect.fromRectAndRadius(Offset.zero & size, Radius.circular(size.width * 0.22)),
      tile,
    );

    for (var row = 0; row < 3; row++) {
      for (var column = 0; column < 3; column++) {
        final paint = Paint()
          ..color = BrandMark.isActive(row, column) ? BrandMark.accent : BrandMark.inactive;
        final rect = Rect.fromLTWH(
          (_inset + column * _step) * scale,
          (_inset + row * _step) * scale,
          _cell * scale,
          _cell * scale,
        );
        canvas.drawRRect(
          RRect.fromRectAndRadius(rect, Radius.circular(_radius * scale)),
          paint,
        );
      }
    }
  }

  @override
  bool shouldRepaint(_MarkPainter oldDelegate) => false;
}
