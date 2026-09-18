import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/pin_policy.dart';

void main() {
  // Приложение показывает правило раньше, чем зовёт сервер. Разъехавшись, эти двое начнут
  // спорить: приложение примет то, что сервер отвергнет, — и виноватым окажется человек.
  test('длина совпадает с той, что проверяет сервер', () {
    final source = File('../AFK4.Shared.Contracts/Identity/PinContracts.cs').readAsStringSync();
    final match = RegExp(r'public const int Length\s*=\s*(\d+)').firstMatch(source);

    expect(match, isNotNull);
    expect(int.parse(match!.group(1)!), PinPolicy.length);
  });

  test('принимает шесть цифр и отвергает всё остальное', () {
    expect(PinPolicy.isWellFormed('123456'), isTrue);
    expect(PinPolicy.isWellFormed('12345'), isFalse);
    expect(PinPolicy.isWellFormed('1234567'), isFalse);
    expect(PinPolicy.isWellFormed('12345a'), isFalse);
    expect(PinPolicy.isWellFormed('1234 6'), isFalse);
    expect(PinPolicy.isWellFormed(''), isFalse);
    expect(PinPolicy.isWellFormed('٦٦٦٦٦٦'), isFalse);
  });
}
