import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/idempotency.dart';

/// Ключ идемпотентности защищает от двойного списания ровно в одном случае: ответ на первый
/// запрос потерялся, и человек нажал ещё раз. Новый ключ на каждое нажатие делает защиту
/// бессмысленной именно там, ради чего она написана.
void main() {
  test('повтор той же попытки несёт тот же ключ', () {
    final attempt = AttemptKey();

    expect(attempt.forSubject('pkg1'), attempt.forSubject('pkg1'));
  });

  test('другая покупка получает свой ключ', () {
    final attempt = AttemptKey();

    final first = attempt.forSubject('pkg1');
    final second = attempt.forSubject('pkg2');

    expect(second, isNot(first));
  });

  // Со старым ключом сервер вернул бы ответ прошлой покупки и денег не взял — второй такой же
  // пакет игрок не получил бы вовсе.
  test('после удачной покупки такая же следующая идёт с новым ключом', () {
    final attempt = AttemptKey();

    final first = attempt.forSubject('pkg1');
    attempt.done();

    expect(attempt.forSubject('pkg1'), isNot(first));
  });

  test('вернувшись к прежней покупке, попытка берёт новый ключ, а не прежний', () {
    final attempt = AttemptKey();

    final first = attempt.forSubject('pkg1');
    attempt.forSubject('pkg2');

    expect(attempt.forSubject('pkg1'), isNot(first));
  });

  test('ключи не повторяются', () {
    final keys = {for (var i = 0; i < 500; i++) newIdempotencyKey()};

    expect(keys.length, 500);
  });
}
