import 'package:flutter/material.dart';

import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../organization/organization.dart';
import '../theme/brand_mark.dart';

/// Вход в клуб, выбранный на предыдущем экране. Организация обязательна: игрок опознаётся
/// парой организация + телефон, один и тот же номер может быть игроком в разных сетях.
///
/// Два способа: пароль и код из SMS. Код — не запасной путь, а равноправный: телефон и так
/// служит логином, а пароль можно забыть.
class SignInScreen extends StatefulWidget {
  const SignInScreen({
    super.key,
    required this.organization,
    required this.api,
    required this.onSignedIn,
    required this.onChangeClub,
  });

  final Organization organization;
  final PlayerApiClient api;
  final VoidCallback onSignedIn;
  final VoidCallback onChangeClub;

  @override
  State<SignInScreen> createState() => _SignInScreenState();
}

/// Чем игрок доказывает, что аккаунт его: паролем или кодом, который ещё надо запросить.
enum _Method { password, codeRequest, codeEntry }

class _SignInScreenState extends State<SignInScreen> {
  final _phone = TextEditingController();
  final _password = TextEditingController();
  final _code = TextEditingController();

  _Method _method = _Method.password;
  bool _busy = false;
  String? _error;
  String? _notice;

  @override
  void dispose() {
    _phone.dispose();
    _password.dispose();
    _code.dispose();
    super.dispose();
  }

  Future<void> _run(Future<void> Function() action) async {
    if (_busy) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await action();
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _error = _message(L.of(context), error);
        _busy = false;
      });
      return;
    }
    if (mounted) setState(() => _busy = false);
  }

  /// Обрыв связи — не «неверный пароль». В вебе оба случая показывались одинаково, и игрок
  /// при пропавшем интернете шёл менять правильный пароль.
  String _message(L l, PlayerApiException error) => switch (error.statusCode) {
        null => l.customerSigninNetworkError,
        400 when _method == _Method.codeEntry => l.customerSigninCodeError,
        410 => l.customerSigninCodeNone,
        429 => l.customerSigninCodeTooMany,
        _ => l.customerSigninError,
      };

  Future<void> _submitPassword() => _run(() async {
        await widget.api.signIn(
          organizationId: widget.organization.organizationId,
          phoneNumber: _phone.text.trim(),
          password: _password.text,
        );
        if (mounted) widget.onSignedIn();
      });

  Future<void> _requestCode() => _run(() async {
        await widget.api.startCodeSignIn(
          organizationId: widget.organization.organizationId,
          phoneNumber: _phone.text.trim(),
        );
        if (!mounted) return;
        setState(() {
          _method = _Method.codeEntry;
          _notice = L.of(context).customerSigninCodeSent(_phone.text.trim());
        });
      });

  Future<void> _submitCode() => _run(() async {
        await widget.api.confirmCodeSignIn(
          organizationId: widget.organization.organizationId,
          phoneNumber: _phone.text.trim(),
          code: _code.text.trim(),
        );
        if (mounted) widget.onSignedIn();
      });

  void _switchMethod() {
    setState(() {
      _method = _method == _Method.password ? _Method.codeRequest : _Method.password;
      _error = null;
      _notice = null;
      _code.clear();
    });
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Scaffold(
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const SizedBox(height: 12),
              const Align(alignment: Alignment.centerLeft, child: BrandMark()),
              const SizedBox(height: 40),
              Text(
                l.customerSigninTitle,
                style: theme.textTheme.bodyLarge?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
              const SizedBox(height: 2),
              Text(widget.organization.name, style: theme.textTheme.headlineLarge),
              const SizedBox(height: 28),
              TextField(
                controller: _phone,
                enabled: !_busy && _method != _Method.codeEntry,
                keyboardType: TextInputType.phone,
                autofillHints: const [AutofillHints.telephoneNumber],
                decoration: InputDecoration(labelText: l.customerSigninPhone),
              ),
              const SizedBox(height: 12),
              ..._methodFields(l),
              if (_notice != null) ...[
                const SizedBox(height: 12),
                Text(
                  _notice!,
                  style: theme.textTheme.bodySmall
                      ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                ),
              ],
              if (_error != null) ...[
                const SizedBox(height: 12),
                Text(_error!, style: TextStyle(color: theme.colorScheme.error)),
              ],
              const SizedBox(height: 24),
              ..._actions(l),
              const SizedBox(height: 8),
              TextButton(
                onPressed: _busy ? null : widget.onChangeClub,
                child: Text(l.customerClubPickerChange),
              ),
            ],
          ),
        ),
      ),
    );
  }

  List<Widget> _methodFields(L l) => switch (_method) {
        _Method.password => [
            TextField(
              controller: _password,
              enabled: !_busy,
              obscureText: true,
              autofillHints: const [AutofillHints.password],
              decoration: InputDecoration(labelText: l.customerSigninPassword),
              onSubmitted: (_) => _submitPassword(),
            ),
          ],
        _Method.codeRequest => const [],
        _Method.codeEntry => [
            TextField(
              controller: _code,
              enabled: !_busy,
              autofocus: true,
              keyboardType: TextInputType.number,
              autofillHints: const [AutofillHints.oneTimeCode],
              decoration: InputDecoration(labelText: l.customerPhoneCode),
              onSubmitted: (_) => _submitCode(),
            ),
          ],
      };

  List<Widget> _actions(L l) => switch (_method) {
        _Method.password => [
            FilledButton(
              onPressed: _busy ? null : _submitPassword,
              child: Text(_busy ? l.customerSigninSubmitting : l.customerSigninSubmit),
            ),
            const SizedBox(height: 8),
            TextButton(onPressed: _busy ? null : _switchMethod, child: Text(l.customerSigninByCode)),
          ],
        _Method.codeRequest => [
            FilledButton(
              onPressed: _busy ? null : _requestCode,
              child: Text(_busy ? l.customerPhoneSending : l.customerPhoneSend),
            ),
            const SizedBox(height: 8),
            TextButton(
              onPressed: _busy ? null : _switchMethod,
              child: Text(l.customerSigninByPassword),
            ),
          ],
        _Method.codeEntry => [
            FilledButton(
              onPressed: _busy ? null : _submitCode,
              child: Text(_busy ? l.customerSigninSubmitting : l.customerSigninSubmit),
            ),
            const SizedBox(height: 8),
            TextButton(
              onPressed: _busy ? null : _requestCode,
              child: Text(l.customerPhoneResend),
            ),
            TextButton(
              onPressed: _busy ? null : _switchMethod,
              child: Text(l.customerSigninByPassword),
            ),
          ],
      };
}
