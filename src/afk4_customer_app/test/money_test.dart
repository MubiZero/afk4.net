import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/money/money.dart';

/// Пробелы-разделители разрядов у разных локалей разные (обычный, неразрывный, узкий) —
/// тест проверяет структуру суммы, а не то, каким именно пробелом её разбил `intl`.
String normalized(String value) => value.replaceAll(RegExp(r'\s'), ' ');

void main() {
  test('минорные единицы переводятся в мажорные и обратно', () {
    expect(minorToMajor(12345), 123.45);
    expect(majorToMinor(99.99), 9999);
    expect(majorToMinor(2.5), 250);
    // 1.005 в IEEE-754 лежит чуть ниже — без поправки округлилось бы вниз, в 100.
    expect(majorToMinor(1.005), 101);
  });

  test('валюта показывается знаком, а незнакомая — своим кодом', () {
    expect(currencySymbol('TJS'), 'с.');
    expect(currencySymbol('usd'), r'$');
    expect(currencySymbol('GBP'), 'GBP');
  });

  test('сумма показывается со знаком валюты и разделителем разрядов', () {
    expect(normalized(formatMoney(120050, 'TJS', locale: 'ru')), '1 200,50 с.');
  });

  // Копейки не срезаются: это кошелёк игрока, а не сводка клуба. Округли до целого —
  // и человек увидит на экране не свои деньги.
  test('копейки сохраняются в показе', () {
    expect(normalized(formatMoney(50, 'TJS', locale: 'ru')), '0,50 с.');
    expect(normalized(formatMoney(0, 'TJS', locale: 'ru')), '0,00 с.');
  });

  // `intl` не знает таджикского: без отката приложение падало бы на каждой сумме,
  // показанной на этом языке, — а это весь кошелёк.
  test('таджикская локаль не роняет форматирование, а считается русской', () {
    expect(formatMoney(120050, 'TJS', locale: 'tg'), formatMoney(120050, 'TJS', locale: 'ru'));
    expect(numberLocale('tg'), 'ru');
    expect(numberLocale('en'), 'en');
  });
}
