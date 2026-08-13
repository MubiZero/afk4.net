import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/organization/opening_hours.dart';

/// Расписание «с 10:00 до 23:00, воскресенье выходной».
List<OpeningDay> _daytime() => [
      for (var day = 1; day <= 7; day++)
        OpeningDay(
          dayOfWeek: day,
          isClosed: day == 7,
          openTime: day == 7 ? null : '10:00',
          closeTime: day == 7 ? null : '23:00',
        ),
    ];

/// Ночная смена: с 22:00 до 06:00 следующего дня — обычное расписание клуба.
List<OpeningDay> _overnight() => [
      for (var day = 1; day <= 7; day++)
        OpeningDay(dayOfWeek: day, isClosed: false, openTime: '22:00', closeTime: '06:00'),
    ];

// 2026-08-12 — среда.
DateTime _wednesdayAt(int hour, [int minute = 0]) => DateTime(2026, 8, 12, hour, minute);

void main() {
  test('в рабочие часы говорит, до скольких открыто', () {
    final status = openingStatusAt(_daytime(), _wednesdayAt(14));

    expect(status.kind, OpeningStatusKind.openUntil);
    expect(status.time, '23:00');
  });

  test('до открытия называет час открытия', () {
    final status = openingStatusAt(_daytime(), _wednesdayAt(8));

    expect(status.kind, OpeningStatusKind.opensAt);
    expect(status.time, '10:00');
  });

  // После закрытия честнее назвать завтрашний час, чем просто «закрыто»: игрок планирует
  // вечер, а не читает справку.
  test('после закрытия обещает завтрашнее открытие', () {
    final status = openingStatusAt(_daytime(), _wednesdayAt(23, 30));

    expect(status.kind, OpeningStatusKind.opensAt);
    expect(status.time, '10:00');
  });

  test('в выходной день так и говорит', () {
    // 2026-08-16 — воскресенье.
    final status = openingStatusAt(_daytime(), DateTime(2026, 8, 16, 14));

    expect(status.kind, OpeningStatusKind.closedToday);
  });

  // Клуб, работающий до утра, в час ночи открыт по вчерашней строке расписания. Считать
  // только сегодняшнюю значило бы показывать «закрыто» в разгар ночной смены.
  test('ночная смена считается открытой после полуночи', () {
    final status = openingStatusAt(_overnight(), _wednesdayAt(1));

    expect(status.kind, OpeningStatusKind.openUntil);
    expect(status.time, '06:00');
  });

  test('ночная смена вечером тоже открыта', () {
    final status = openingStatusAt(_overnight(), _wednesdayAt(23));

    expect(status.kind, OpeningStatusKind.openUntil);
    expect(status.time, '06:00');
  });

  test('днём между сменами закрыто до вечера', () {
    final status = openingStatusAt(_overnight(), _wednesdayAt(12));

    expect(status.kind, OpeningStatusKind.opensAt);
    expect(status.time, '22:00');
  });

  // «Открыто до 23:59» звучит как скорое закрытие, хотя клуб не закрывается вовсе.
  test('круглые сутки названы круглыми сутками', () {
    final schedule = [
      for (var day = 1; day <= 7; day++)
        OpeningDay(dayOfWeek: day, isClosed: false, openTime: '00:00', closeTime: '23:59'),
    ];

    expect(openingStatusAt(schedule, _wednesdayAt(3)).kind, OpeningStatusKind.openAllDay);
  });

  // Придумать за клуб часы работы значит отправить игрока к закрытой двери.
  test('без расписания статус неизвестен, а не «открыто»', () {
    expect(openingStatusAt(const [], _wednesdayAt(14)).kind, OpeningStatusKind.unknown);
  });

  test('поломанное время не выдаётся за расписание', () {
    final schedule = [
      for (var day = 1; day <= 7; day++)
        OpeningDay(dayOfWeek: day, isClosed: false, openTime: '9am', closeTime: 'позже'),
    ];

    expect(openingStatusAt(schedule, _wednesdayAt(14)).kind, OpeningStatusKind.unknown);
  });
}
