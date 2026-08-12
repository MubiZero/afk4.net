import 'dart:async';

import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/dto.dart';
import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/history/cursor_list.dart';

CursorPage<String> page(List<String> items, String? next) =>
    CursorPage(items: items, nextCursor: next);

void main() {
  test('первая страница загружается сразу и знает, есть ли продолжение', () async {
    final list = CursorListController<String>((cursor) async => page(['a', 'b'], 'c2'));
    await pumpEventQueue();

    expect(list.status, CursorListStatus.ready);
    expect(list.items, ['a', 'b']);
    expect(list.hasMore, isTrue);
  });

  test('подгрузка добавляет страницу в конец и запоминает новый курсор', () async {
    final cursors = <String?>[];
    final list = CursorListController<String>((cursor) async {
      cursors.add(cursor);
      return cursor == null ? page(['a'], 'c2') : page(['b'], null);
    });
    await pumpEventQueue();

    await list.loadMore();

    expect(cursors, [null, 'c2']);
    expect(list.items, ['a', 'b']);
    expect(list.hasMore, isFalse);
  });

  test('без курсора подгружать нечего — лишнего запроса не будет', () async {
    var calls = 0;
    final list = CursorListController<String>((cursor) async {
      calls++;
      return page(['a'], null);
    });
    await pumpEventQueue();

    await list.loadMore();

    expect(calls, 1);
  });

  test('ошибка первой страницы даёт состояние отказа с возможностью повтора', () async {
    var attempt = 0;
    final list = CursorListController<String>((cursor) async {
      if (++attempt == 1) throw const PlayerApiException(500, 'boom');
      return page(['a'], null);
    });
    await pumpEventQueue();
    expect(list.status, CursorListStatus.failed);

    await list.load();

    expect(list.status, CursorListStatus.ready);
    expect(list.items, ['a']);
  });

  // Не дозагрузилось — не повод очищать то, что игрок уже читает.
  test('ошибка подгрузки оставляет уже показанные записи', () async {
    final list = CursorListController<String>((cursor) async {
      if (cursor == null) return page(['a'], 'c2');
      throw const PlayerApiException(500, 'boom');
    });
    await pumpEventQueue();

    await list.loadMore();

    expect(list.items, ['a']);
    expect(list.status, CursorListStatus.ready);
    expect(list.loadingMore, isFalse);
  });

  // Повтор после ошибки не должен склеиться со страницей от брошенной попытки.
  test('ответ отменённого запроса не попадает в список', () async {
    final gates = <Completer<CursorPage<String>>>[];
    final list = CursorListController<String>((cursor) {
      final gate = Completer<CursorPage<String>>();
      gates.add(gate);
      return gate.future;
    });
    await pumpEventQueue();

    // Пока висит первый запрос, игрок жмёт повтор — приходит второй.
    unawaited(list.load());
    await pumpEventQueue();
    gates[1].complete(page(['новое'], null));
    gates[0].complete(page(['старое'], 'c2'));
    await pumpEventQueue();

    expect(list.items, ['новое']);
    expect(list.hasMore, isFalse);
  });

  test('повторное нажатие «показать ещё» не шлёт второй запрос', () async {
    final gate = Completer<CursorPage<String>>();
    var moreCalls = 0;
    final list = CursorListController<String>((cursor) {
      if (cursor == null) return Future.value(page(['a'], 'c2'));
      moreCalls++;
      return gate.future;
    });
    await pumpEventQueue();

    unawaited(list.loadMore());
    unawaited(list.loadMore());
    await pumpEventQueue();
    gate.complete(page(['b'], null));
    await pumpEventQueue();

    expect(moreCalls, 1);
    expect(list.items, ['a', 'b']);
  });
}
