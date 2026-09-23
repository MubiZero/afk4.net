import 'dart:io';

import 'package:flutter/services.dart';

/// Подключает настоящий Roboto — шрифт Android — вместо тестового.
///
/// Тестовый шрифт рисует каждую букву квадратом во всю высоту кегля: строка у него почти вдвое
/// шире настоящей. Для проверки «не переполняется ли экран на крупном шрифте» это ложная
/// тревога на каждом шагу, поэтому такие тесты меряют тем же шрифтом, каким рисует телефон.
///
/// Шрифт берётся из кеша Flutter SDK (`material_fonts`), который есть везде, где есть Flutter.
/// Не нашёлся — тест падает, а не молча меряет квадратами: зелёный на чужом шрифте ничего бы
/// не доказывал.
Future<void> loadRealFonts() async {
  final root = Platform.environment['FLUTTER_ROOT'];
  if (root == null) {
    throw StateError('FLUTTER_ROOT не задан — негде взять Roboto для замера вёрстки');
  }
  final directory = Directory('$root/bin/cache/artifacts/material_fonts');
  final loader = FontLoader('Roboto');
  for (final weight in ['Regular', 'Medium', 'Bold', 'Black']) {
    final file = File('${directory.path}/Roboto-$weight.ttf');
    if (!file.existsSync()) throw StateError('Нет ${file.path}');
    final bytes = file.readAsBytesSync();
    loader.addFont(Future.value(ByteData.sublistView(bytes)));
  }
  await loader.load();
}
