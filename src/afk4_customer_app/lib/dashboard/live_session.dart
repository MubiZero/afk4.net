/// Часы живой сессии. Отдельно от виджета, потому что это чистая арифметика, и ошибку в ней
/// дешевле поймать тестом, чем разглядыванием бегущих цифр.
library;

int elapsedSeconds(DateTime startedAtUtc, {required DateTime now}) {
  final seconds = now.difference(startedAtUtc).inSeconds;
  return seconds < 0 ? 0 : seconds;
}

/// Остаток по оплаченной сессии. Сервер отдаёт его на момент ответа, поэтому между опросами
/// остаток уменьшается сам — иначе цифра стояла бы на месте по полминуты и оживала рывком.
int projectRemainingSeconds(int remainingAtFetch, DateTime fetchedAt, {required DateTime now}) {
  final projected = remainingAtFetch - now.difference(fetchedAt).inSeconds;
  return projected < 0 ? 0 : projected;
}

String formatClock(int totalSeconds) {
  final s = totalSeconds < 0 ? 0 : totalSeconds;
  final hh = (s ~/ 3600).toString().padLeft(2, '0');
  final mm = ((s % 3600) ~/ 60).toString().padLeft(2, '0');
  final ss = (s % 60).toString().padLeft(2, '0');
  return '$hh:$mm:$ss';
}
