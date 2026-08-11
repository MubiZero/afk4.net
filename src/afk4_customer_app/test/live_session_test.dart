import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/dashboard/live_session.dart';

final _start = DateTime.utc(2026, 8, 12, 10, 0, 0);

void main() {
  test('время сессии считается от её начала', () {
    expect(elapsedSeconds(_start, now: _start.add(const Duration(minutes: 90))), 5400);
  });

  // Часы устройства бывают позади серверных. Отрицательное время на экране выглядит
  // как поломка, поэтому счётчик упирается в ноль.
  test('часы позади сервера дают ноль, а не отрицательное время', () {
    expect(elapsedSeconds(_start, now: _start.subtract(const Duration(minutes: 5))), 0);
  });

  test('остаток оплаченной сессии уменьшается сам между опросами', () {
    final fetchedAt = DateTime.utc(2026, 8, 12, 10, 0, 0);
    expect(
      projectRemainingSeconds(600, fetchedAt, now: fetchedAt.add(const Duration(seconds: 45))),
      555,
    );
  });

  test('истёкший остаток не уходит в минус', () {
    final fetchedAt = DateTime.utc(2026, 8, 12, 10, 0, 0);
    expect(
      projectRemainingSeconds(30, fetchedAt, now: fetchedAt.add(const Duration(minutes: 5))),
      0,
    );
  });

  test('часы показываются как ЧЧ:ММ:СС', () {
    expect(formatClock(0), '00:00:00');
    expect(formatClock(59), '00:00:59');
    expect(formatClock(3661), '01:01:01');
    expect(formatClock(360000), '100:00:00');
  });
}
