import 'dart:async';

import 'package:flutter/material.dart';

import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';

/// Подтверждение своего номера по SMS.
///
/// Пока номер не подтверждён, онлайн-пополнение и онлайн-бронь закрыты. Раньше интерфейс
/// отправлял игрока к администратору клуба — но и у администратора такой возможности не было,
/// так что дверь была заперта с обеих сторон.
///
/// Возвращает `true`, если номер подтверждён: вызвавший экран обновляет состояние.
class PhoneVerificationSheet extends StatefulWidget {
  const PhoneVerificationSheet({super.key, required this.api, this.initialPhone});

  final PlayerApiClient api;

  /// Номер из профиля — чаще всего подтверждают именно его, и набирать заново незачем.
  final String? initialPhone;

  @override
  State<PhoneVerificationSheet> createState() => _PhoneVerificationSheetState();
}

enum _Step { phone, code }

class _PhoneVerificationSheetState extends State<PhoneVerificationSheet> {
  late final TextEditingController _phone = TextEditingController(text: widget.initialPhone ?? '');
  final TextEditingController _code = TextEditingController();

  _Step _step = _Step.phone;
  bool _pending = false;
  String? _problem;

  /// Сколько секунд осталось до следующей попытки. Молчаливо заблокированная кнопка
  /// выглядит как поломка, поэтому обратный отсчёт виден.
  int _resendIn = 0;
  Timer? _countdown;

  @override
  void dispose() {
    _countdown?.cancel();
    _phone.dispose();
    _code.dispose();
    super.dispose();
  }

  void _startCountdown(int seconds) {
    _countdown?.cancel();
    setState(() => _resendIn = seconds);
    _countdown = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (!mounted) return timer.cancel();
      setState(() => _resendIn -= 1);
      if (_resendIn <= 0) timer.cancel();
    });
  }

  Future<void> _send() async {
    final l = L.of(context);
    setState(() {
      _pending = true;
      _problem = null;
    });
    try {
      final started = await widget.api.startPhoneVerification(_phone.text.trim());
      if (!mounted) return;
      setState(() {
        _step = _Step.code;
        _pending = false;
      });
      _startCountdown(started.resendAfterSeconds);
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _pending = false;
        _problem = _startProblem(l, error);
      });
    }
  }

  Future<void> _confirm() async {
    final l = L.of(context);
    setState(() {
      _pending = true;
      _problem = null;
    });
    try {
      await widget.api.confirmPhone(_code.text.trim());
      if (mounted) Navigator.of(context).pop(true);
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _pending = false;
        _problem = _confirmProblem(l, error);
        // Устаревший код и исчерпанные попытки лечатся только новым кодом — возвращаем на шаг
        // назад, чтобы игрок не бился в поле, которое уже ничего не примет.
        if (error.statusCode == 410 || error.statusCode == 429) _step = _Step.phone;
      });
    }
  }

  /// Причина отказа словами. Общее «не удалось» здесь бесполезно: «подождите минуту» и
  /// «проверьте номер» требуют разных действий.
  static String _startProblem(L l, PlayerApiException error) => switch (error.statusCode) {
        400 => l.customerPhoneErrInvalidPhone,
        429 => l.customerPhoneErrCooldown,
        502 => l.customerPhoneErrSms,
        _ => l.customerPhoneErrGeneric,
      };

  static String _confirmProblem(L l, PlayerApiException error) => switch (error.statusCode) {
        400 => l.customerPhoneErrInvalidCode,
        410 => l.customerPhoneErrExpired,
        429 => l.customerPhoneErrTooManyAttempts,
        409 => l.customerPhoneErrInUse,
        _ => l.customerPhoneErrGeneric,
      };

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
            Text(l.customerPhoneTitle, style: theme.textTheme.titleLarge),
            const SizedBox(height: 4),
            Text(
              l.customerPhoneIntro,
              style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 16),
            if (_step == _Step.phone) ..._phoneStep(l) else ..._codeStep(l, theme),
            if (_problem != null) ...[
              const SizedBox(height: 12),
              Text(_problem!, style: TextStyle(color: theme.colorScheme.error)),
            ],
          ],
        ),
      ),
    );
  }

  List<Widget> _phoneStep(L l) => [
        TextField(
          controller: _phone,
          enabled: !_pending,
          autofocus: true,
          keyboardType: TextInputType.phone,
          autofillHints: const [AutofillHints.telephoneNumber],
          decoration: InputDecoration(labelText: l.customerPhoneNumber),
          onSubmitted: (_) => _pending ? null : _send(),
        ),
        const SizedBox(height: 16),
        FilledButton(
          onPressed: _pending ? null : _send,
          child: Text(_pending ? l.customerPhoneSending : l.customerPhoneSend),
        ),
      ];

  List<Widget> _codeStep(L l, ThemeData theme) => [
        Text(
          l.customerPhoneSent(_phone.text.trim()),
          style: theme.textTheme.bodyMedium,
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _code,
          enabled: !_pending,
          autofocus: true,
          keyboardType: TextInputType.number,
          autofillHints: const [AutofillHints.oneTimeCode],
          decoration: InputDecoration(labelText: l.customerPhoneCode),
          onSubmitted: (_) => _pending ? null : _confirm(),
        ),
        const SizedBox(height: 16),
        FilledButton(
          onPressed: _pending ? null : _confirm,
          child: Text(_pending ? l.customerPhoneConfirming : l.customerPhoneConfirm),
        ),
        const SizedBox(height: 8),
        TextButton(
          onPressed: _resendIn > 0 || _pending ? null : _send,
          child: Text(
            _resendIn > 0 ? l.customerPhoneResendIn('$_resendIn') : l.customerPhoneResend,
          ),
        ),
      ];
}
