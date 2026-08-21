import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';

/// PIN, которым игрок садится за ПК.
///
/// Это не пароль от приложения: сюда входят по коду из SMS, а PIN нужен на экране самого ПК
/// в клубе. Старый PIN здесь не спрашивается намеренно — потребовать его значило бы запереть
/// выход ровно тому, кто его забыл.
class PinSheet extends StatefulWidget {
  const PinSheet({super.key, required this.api, required this.pinSet});

  final PlayerApiClient api;

  /// Задан ли PIN сейчас: от этого зависит только подпись кнопки, но не сама процедура.
  final bool pinSet;

  @override
  State<PinSheet> createState() => _PinSheetState();
}

class _PinSheetState extends State<PinSheet> {
  /// Столько же, сколько разрешает сервер: две проверки одной длины разъехались бы на первой
  /// правке, поэтому границы взяты из контракта, а не придуманы заново.
  static const int _minLength = 4;
  static const int _maxLength = 8;

  final _pin = TextEditingController();
  final _repeat = TextEditingController();

  bool _saving = false;
  String? _error;

  @override
  void dispose() {
    _pin.dispose();
    _repeat.dispose();
    super.dispose();
  }

  /// Что не так с введённым — до отправки. Сервер проверит то же самое, но ответит одним
  /// кодом, а игроку нужно знать, какое из двух полей чинить.
  String? _problem(L l) {
    final pin = _pin.text.trim();
    if (pin.length < _minLength ||
        pin.length > _maxLength ||
        !pin.split('').every((symbol) => '0123456789'.contains(symbol))) {
      return l.customerPinErrFormat;
    }
    if (_repeat.text.trim() != pin) return l.customerPinErrRepeat;
    return null;
  }

  Future<void> _save() async {
    final l = L.of(context);
    final problem = _problem(l);
    if (problem != null) {
      setState(() => _error = problem);
      return;
    }

    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await widget.api.setPin(_pin.text.trim());
      if (mounted) Navigator.of(context).pop(true);
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _saving = false;
        _error = error.statusCode == 400 ? l.customerPinErrFormat : l.customerPinErrSave;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(l.customerPinTitle, style: theme.textTheme.titleLarge),
            const SizedBox(height: 8),
            Text(
              l.customerPinIntro,
              style: theme.textTheme.bodyMedium
                  ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 4),
            Text(
              l.customerPinScope,
              style: theme.textTheme.bodySmall
                  ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 20),
            TextField(
              controller: _pin,
              enabled: !_saving,
              autofocus: true,
              obscureText: true,
              keyboardType: TextInputType.number,
              maxLength: _maxLength,
              inputFormatters: [FilteringTextInputFormatter.digitsOnly],
              decoration: InputDecoration(
                labelText: l.customerPinField,
                helperText: l.customerPinRule,
              ),
            ),
            const SizedBox(height: 4),
            TextField(
              controller: _repeat,
              enabled: !_saving,
              obscureText: true,
              keyboardType: TextInputType.number,
              maxLength: _maxLength,
              inputFormatters: [FilteringTextInputFormatter.digitsOnly],
              decoration: InputDecoration(labelText: l.customerPinRepeat),
              onSubmitted: (_) => _save(),
            ),
            if (_error != null) ...[
              const SizedBox(height: 8),
              Text(_error!, style: TextStyle(color: theme.colorScheme.error)),
            ],
            const SizedBox(height: 16),
            FilledButton(
              onPressed: _saving ? null : _save,
              child: Text(_saving ? l.customerPinSaving : l.customerPinSave),
            ),
            const SizedBox(height: 8),
            Text(
              l.customerPinForgot,
              style: theme.textTheme.bodySmall
                  ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
          ],
        ),
      ),
    );
  }
}
