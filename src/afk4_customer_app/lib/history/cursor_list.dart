import 'package:flutter/foundation.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';

typedef FetchPage<T> = Future<CursorPage<T>> Function(String? cursor);

enum CursorListStatus { loading, failed, ready }

/// Подгрузка списка по курсору: первая страница при открытии, дальше — по кнопке.
///
/// Только вперёд: история растёт с одного конца, и нумерация страниц съезжала бы на каждой
/// новой записи. Логика отдельно от экрана, чтобы её можно было проверить без виджетов.
class CursorListController<T> extends ChangeNotifier {
  CursorListController(this._fetch) {
    load();
  }

  final FetchPage<T> _fetch;

  CursorListStatus _status = CursorListStatus.loading;
  List<T> _items = const [];
  String? _cursor;
  bool _loadingMore = false;
  bool _moreFailed = false;
  bool _disposed = false;

  /// Отсекает ответы отменённых запросов: повтор после ошибки не должен получить страницу
  /// от предыдущей попытки и склеить два списка.
  int _requestSeq = 0;

  CursorListStatus get status => _status;
  List<T> get items => _items;
  bool get hasMore => _cursor != null;
  bool get loadingMore => _loadingMore;

  /// Подгрузка следующей страницы сорвалась. Пока это так, список не пытается сам —
  /// иначе прокрутка у края повторяла бы запрос без остановки.
  bool get moreFailed => _moreFailed;

  Future<void> load() async {
    final seq = ++_requestSeq;
    _status = CursorListStatus.loading;
    _moreFailed = false;
    _notify();
    try {
      final page = await _fetch(null);
      if (seq != _requestSeq) return;
      _items = page.items;
      _cursor = page.nextCursor;
      _status = CursorListStatus.ready;
    } on PlayerApiException {
      if (seq != _requestSeq) return;
      _status = CursorListStatus.failed;
    }
    _notify();
  }

  Future<void> loadMore() async {
    final cursor = _cursor;
    if (cursor == null || _loadingMore) return;

    final seq = _requestSeq;
    _loadingMore = true;
    _moreFailed = false;
    _notify();
    try {
      final page = await _fetch(cursor);
      if (seq != _requestSeq) return;
      _items = [..._items, ...page.items];
      _cursor = page.nextCursor;
    } on PlayerApiException {
      // Уже показанные записи остаются: не дозагрузилось — не повод очищать список.
      if (seq == _requestSeq) _moreFailed = true;
    } finally {
      if (seq == _requestSeq) {
        _loadingMore = false;
        _notify();
      }
    }
  }

  void _notify() {
    if (!_disposed) notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    super.dispose();
  }
}
