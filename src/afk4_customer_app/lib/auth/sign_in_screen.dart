import 'package:flutter/material.dart';

import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../organization/organization.dart';

/// Вход в клуб, выбранный на предыдущем экране. Организация обязательна: игрок опознаётся
/// парой организация + телефон, один и тот же номер может быть игроком в разных сетях.
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

class _SignInScreenState extends State<SignInScreen> {
  final _phone = TextEditingController();
  final _password = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _phone.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_busy) return;
    setState(() {
      _busy = true;
      _error = null;
    });

    final l = L.of(context);
    try {
      await widget.api.signIn(
        organizationId: widget.organization.organizationId,
        phoneNumber: _phone.text.trim(),
        password: _password.text,
      );
      if (!mounted) return;
      widget.onSignedIn();
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      setState(() {
        // Обрыв связи — не «неверный пароль». В вебе оба случая показывались одинаково, и
        // игрок при пропавшем интернете шёл менять правильный пароль.
        _error = error.statusCode == null ? l.customerSigninNetworkError : l.customerSigninError;
        _busy = false;
      });
    }
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
              const SizedBox(height: 24),
              Text(l.customerSigninTitle, style: theme.textTheme.bodyMedium),
              Text(widget.organization.name, style: theme.textTheme.headlineMedium),
              const SizedBox(height: 24),
              TextField(
                controller: _phone,
                enabled: !_busy,
                keyboardType: TextInputType.phone,
                autofillHints: const [AutofillHints.telephoneNumber],
                decoration: InputDecoration(labelText: l.customerSigninPhone),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: _password,
                enabled: !_busy,
                obscureText: true,
                autofillHints: const [AutofillHints.password],
                decoration: InputDecoration(labelText: l.customerSigninPassword),
                onSubmitted: (_) => _submit(),
              ),
              if (_error != null) ...[
                const SizedBox(height: 12),
                Text(_error!, style: TextStyle(color: theme.colorScheme.error)),
              ],
              const SizedBox(height: 24),
              FilledButton(
                onPressed: _busy ? null : _submit,
                child: Text(_busy ? l.customerSigninSubmitting : l.customerSigninSubmit),
              ),
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
}
