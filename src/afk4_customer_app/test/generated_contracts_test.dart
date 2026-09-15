import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/contracts.dart';

/// Классы в `lib/api/contracts.dart` порождены из записей C#. Проверять их имена бессмысленно —
/// они оттуда и взяты; проверять надо, что разбор ответа действительно работает и что имена
/// полей на проводе те же, что у сервера.
///
/// Тела ответов взяты один в один из сериализационных наборов на стороне сервера
/// (`tests/AFK4.Shared.Contracts.Tests`), а не выдуманы: выдуманное тело проверяет только само
/// себя — ровно так девять раз подряд зеленел экран, который в проде не работал.
void main() {
  test('деньги разбираются и собираются обратно без потерь', () {
    const payload = '{"currencyCode":"TJS","minorUnits":2400}';
    final money = MoneyDto.fromJson(jsonDecode(payload) as Map<String, dynamic>);

    expect(money.currencyCode, 'TJS');
    expect(money.minorUnits, 2400);
    expect(jsonEncode(money.toJson()), payload);
  });

  test('главный экран игрока: пустая сессия остаётся пустой, а не нулём', () {
    final dashboard = PlayerDashboardDto.fromJson(jsonDecode('''
      {
        "walletBalance": {"currencyCode": "TJS", "minorUnits": 15000},
        "heldBalance": {"currencyCode": "TJS", "minorUnits": 5000},
        "debtBalance": {"currencyCode": "TJS", "minorUnits": 0},
        "activeSession": null
      }
    ''') as Map<String, dynamic>);

    expect(dashboard.walletBalance.minorUnits, 15000);
    expect(dashboard.heldBalance.minorUnits, 5000);
    expect(dashboard.activeSession, isNull);
  });

  test('сессия: необязательные поля тарифа приходят пустыми и остаются пустыми', () {
    final session = ActiveSessionDto.fromJson(jsonDecode('''
      {
        "sessionId": "11111111-1111-4111-8111-111111111111",
        "seatId": "22222222-2222-4222-8222-222222222222",
        "seatName": "PC-01",
        "startedAtUtc": "2026-09-15T10:00:00+05:00",
        "durationMode": "open",
        "remainingSeconds": null,
        "accruedCostMinorUnits": 4200,
        "currencyCode": "TJS",
        "tariffName": null,
        "pricePerHourMinorUnits": null,
        "zoneName": "Зал"
      }
    ''') as Map<String, dynamic>);

    expect(session.seatName, 'PC-01');
    expect(session.startedAtUtc.toUtc().hour, 5);
    // Пусто там, где тарифа у сессии нет: подставленная ставка врала бы про цену.
    expect(session.tariffName, isNull);
    expect(session.pricePerHourMinorUnits, isNull);
    expect(session.accruedCostMinorUnits, 4200);
  });

  test('новость игрока читается тем именем, которым её отдаёт сервер', () {
    final news = PlayerNewsItemDto.fromJson(jsonDecode('''
      {
        "id": "33333333-3333-4333-8333-333333333333",
        "title": "Турнир в субботу",
        "body": "Приходите",
        "imageUrl": null,
        "publishedAtUtc": "2026-08-10T09:00:00+00:00"
      }
    ''') as Map<String, dynamic>);

    expect(news.title, 'Турнир в субботу');
    expect(news.publishedAtUtc.toUtc().year, 2026);
  });

  test('страница курсора разбирает свои элементы чужим разборщиком', () {
    final page = CursorPage<PlayerLedgerEntryDto>.fromJson(
      jsonDecode('''
        {
          "items": [
            {
              "ledgerEntryId": "44444444-4444-4444-8444-444444444444",
              "entryType": "top_up",
              "amount": {"currencyCode": "TJS", "minorUnits": 10000},
              "quantitySeconds": 0,
              "createdAtUtc": "2026-09-15T10:00:00+05:00"
            }
          ],
          "nextCursor": "eyJ4IjoxfQ"
        }
      ''') as Map<String, dynamic>,
      PlayerLedgerEntryDto.fromJson,
    );

    expect(page.items, hasLength(1));
    expect(page.items.single.amount.minorUnits, 10000);
    expect(page.nextCursor, 'eyJ4IjoxfQ');
  });

  test('строка чека в запросе несёт только товар и количество', () {
    const line = CreatePosSaleLineDto(
      productId: '55555555-5555-4555-8555-555555555555',
      quantity: 2,
    );

    expect(line.toJson(), {
      'productId': '55555555-5555-4555-8555-555555555555',
      'quantity': 2,
    });
  });
}
