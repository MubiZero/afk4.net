import 'dart:math';

/// Ключ идемпотентности для денежных действий.
///
/// Нужен там, где повтор запроса означает второе списание: ответ потерялся в пути, игрок
/// нажал ещё раз — сервер по ключу узнаёт ту же попытку и не берёт деньги дважды. Поэтому
/// ключ генерируется ОДИН раз на попытку и переиспользуется при повторе, а не на каждый вызов.
String newIdempotencyKey() {
  final stamp = DateTime.now().microsecondsSinceEpoch.toRadixString(36);
  final noise = _random.nextInt(1 << 32).toRadixString(36);
  return '$stamp-$noise';
}

final Random _random = Random();
