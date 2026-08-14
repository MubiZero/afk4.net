import 'package:flutter/material.dart';

import '../api/idempotency.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';

/// На сколько предлагается продлить. Полчаса — «доиграть катку», час — обычная добавка,
/// два — «остаёмся». Ввод произвольных минут сюда не просится: игрок нажимает эту кнопку,
/// когда время уже кончается, и лишний выбор стоит ему сессии.
const List<int> extendOptionsMinutes = [30, 60, 120];

/// Предвыбранный вариант. Пустой выбор заставил бы игрока принимать решение с нуля, а час —
/// то, что берут чаще всего; переключить его — одно касание.
const int defaultExtendMinutes = 60;

/// Продолжительность словами: до часа — в минутах, дальше — в часах.
///
/// Оба сообщения плюральные целиком, а не «число + слово рядом»: в русском «1 час»,
/// «2 часа», «5 часов» — три разные формы, и склейка их не даёт.
String extendDurationLabel(L l, int minutes) =>
    minutes < 60 ? l.customerSessionExtendMinutes(minutes) : l.customerSessionExtendHours(minutes ~/ 60);

/// Лист продления идущей сессии.
///
/// Возвращает выбранные минуты, когда продление прошло, и null, когда игрок закрыл лист.
/// Сам он ничего не рассказывает об успехе: сообщение показывает главный экран, который
/// после этого перечитывает баланс и остаток.
class ExtendSessionSheet extends StatefulWidget {
  const ExtendSessionSheet({super.key, required this.api, required this.sessionId});

  final PlayerApiClient api;
  final String sessionId;

  @override
  State<ExtendSessionSheet> createState() => _ExtendSessionSheetState();
}

class _ExtendSessionSheetState extends State<ExtendSessionSheet> {
  int _minutes = defaultExtendMinutes;
  bool _pending = false;
  String? _error;

  Future<void> _submit() async {
    final l = L.of(context);
    setState(() {
      _pending = true;
      _error = null;
    });

    try {
      await widget.api.extendSession(
        sessionId: widget.sessionId,
        additionalMinutes: _minutes,
        // Ключ рождается здесь, а не в клиенте API: повтор той же попытки должен нести тот
        // же ключ, иначе идемпотентность не спасёт от двойного списания.
        idempotencyKey: newIdempotencyKey(),
      );
      if (mounted) Navigator.of(context).pop(_minutes);
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _pending = false;
        _error = switch (error.statusCode) {
          409 => l.customerSessionExtendErrBalance,
          404 => l.customerSessionExtendErrGone,
          _ => l.customerSessionExtendErrGeneric,
        };
      });
    } catch (_) {
      // Любой другой сбой тоже обязан вернуть кнопку в рабочее состояние: незакрытое
      // «Продлеваем…» выглядит как зависшее списание, и игрок либо ждёт зря, либо жмёт ещё раз.
      if (!mounted) return;
      setState(() {
        _pending = false;
        _error = l.customerSessionExtendErrGeneric;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(l.customerSessionExtendTitle, style: theme.textTheme.titleLarge),
          const SizedBox(height: 16),
          Wrap(
            spacing: 8,
            children: [
              for (final minutes in extendOptionsMinutes)
                ChoiceChip(
                  label: Text(extendDurationLabel(l, minutes)),
                  selected: _minutes == minutes,
                  onSelected: _pending ? null : (_) => setState(() => _minutes = minutes),
                ),
            ],
          ),
          const SizedBox(height: 16),
          FilledButton(
            onPressed: _pending ? null : _submit,
            child: Text(
              _pending
                  ? l.customerSessionExtendPending
                  : l.customerSessionExtendConfirm(extendDurationLabel(l, _minutes)),
            ),
          ),
          // Ошибка живёт рядом с кнопкой, а не всплывашкой поверх: игрок должен видеть
          // причину и сразу выбрать другой вариант, а не ловить исчезающую подсказку.
          if (_error != null) ...[
            const SizedBox(height: 12),
            Text(_error!, style: TextStyle(color: theme.colorScheme.error)),
          ],
          const SizedBox(height: 12),
          // Откуда возьмутся деньги — сказано до нажатия, а не после списания.
          Text(
            l.customerSessionExtendHint,
            style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
        ],
      ),
    );
  }
}
