import 'package:flutter/material.dart';

import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../organization/organization.dart';
import '../theme/brand_mark.dart';

/// Вход в приложение: номер телефона и код из SMS.
///
/// Дверь одна на всех — и для того, кто здесь впервые, и для того, кто играет третий год.
/// Разделить «войти» и «зарегистрироваться» значило бы сказать каждому звонящему, знаком ли
/// системе его номер. Незнакомого человека после кода спрашивают об имени и языке — и это
/// вся регистрация.
///
/// Клуб на входе не участвует: аккаунт один на все клубы сети.
class SignInScreen extends StatefulWidget {
  const SignInScreen({
    super.key,
    required this.organization,
    required this.api,
    required this.onSignedIn,
    required this.onChangeClub,
    this.onLocaleChanged,
  });

  /// Клуб, выбранный до входа: игрок видит, куда попадёт. Ни на вход, ни на аккаунт он не
  /// влияет.
  final Organization organization;
  final PlayerApiClient api;
  final VoidCallback onSignedIn;
  final VoidCallback onChangeClub;

  /// Язык, выбранный при регистрации, применяется сразу — человек назвал его как раз затем,
  /// чтобы читать приложение на нём.
  final ValueChanged<Locale>? onLocaleChanged;

  @override
  State<SignInScreen> createState() => _SignInScreenState();
}

/// Три шага двери: назвать номер, ввести код, назваться самому.
enum _Step { phone, code, profile }

class _SignInScreenState extends State<SignInScreen> {
  final _phone = TextEditingController();
  final _code = TextEditingController();
  final _name = TextEditingController();

  _Step _step = _Step.phone;
  String _locale = 'ru';
  bool _busy = false;
  String? _error;
  String? _notice;

  @override
  void initState() {
    super.initState();
    _locale = WidgetsBinding.instance.platformDispatcher.locale.languageCode;
    if (!const ['ru', 'tg', 'en'].contains(_locale)) _locale = 'ru';
  }

  @override
  void dispose() {
    _phone.dispose();
    _code.dispose();
    _name.dispose();
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

  /// Обрыв связи — не «неверный код». В вебе оба случая показывались одинаково, и игрок
  /// при пропавшем интернете шёл искать SMS, которая давно пришла.
  String _message(L l, PlayerApiException error) => switch ((_step, error.statusCode)) {
        (_, null) => l.customerSigninNetworkError,
        (_Step.profile, _) => l.customerSigninSaveError,
        (_Step.code, 400) => l.customerSigninCodeError,
        (_, 400) => l.customerPhoneErrInvalidPhone,
        (_, 403) => l.customerSigninBlocked,
        (_, 410) => l.customerSigninCodeNone,
        (_, 429) => l.customerSigninCodeTooMany,
        _ => l.customerSigninFailed,
      };

  Future<void> _requestCode() => _run(() async {
        final phone = _phone.text.trim();
        await widget.api.startSignIn(phone);
        if (!mounted) return;
        setState(() {
          _step = _Step.code;
          _notice = L.of(context).customerSigninCodeSentAny(phone);
        });
      });

  Future<void> _submitCode() => _run(() async {
        final session = await widget.api.confirmSignIn(
          phoneNumber: _phone.text.trim(),
          code: _code.text.trim(),
        );
        if (!mounted) return;
        // Имя и язык спрашиваются только у того, кого мы ещё не знаем. Давнего игрока
        // тащить через форму знакомства значит не узнать его.
        if (session.profileCompleted) {
          widget.onSignedIn();
          return;
        }
        setState(() {
          _step = _Step.profile;
          _notice = null;
          _locale = session.preferredLocale ?? _locale;
        });
      });

  Future<void> _submitProfile() => _run(() async {
        final name = _name.text.trim();
        if (name.isEmpty) {
          if (mounted) setState(() => _error = L.of(context).customerSigninNameError);
          return;
        }
        await widget.api.updateMe(displayName: name, preferredLocale: _locale);
        final session = widget.api.session;
        if (session != null) widget.api.updateSession(session.withProfileCompleted(name));
        widget.onLocaleChanged?.call(Locale(_locale));
        if (mounted) widget.onSignedIn();
      });

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
                _step == _Step.profile ? l.customerSigninNameTitle : l.customerSigninTitle,
                style: theme.textTheme.bodyLarge?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
              const SizedBox(height: 2),
              Text(
                _step == _Step.profile ? l.customerSigninNameHint : widget.organization.name,
                style: _step == _Step.profile
                    ? theme.textTheme.bodyMedium
                        ?.copyWith(color: theme.colorScheme.onSurfaceVariant)
                    : theme.textTheme.headlineLarge,
              ),
              const SizedBox(height: 28),
              ..._fields(l, theme),
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
              // Из шага знакомства менять клуб некуда: человек уже вошёл, и его ждёт
              // приложение, а не второй выбор заведения.
              if (_step != _Step.profile) ...[
                const SizedBox(height: 8),
                TextButton(
                  onPressed: _busy ? null : widget.onChangeClub,
                  child: Text(l.customerClubPickerChange),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  List<Widget> _fields(L l, ThemeData theme) => switch (_step) {
        _Step.phone => [
            TextField(
              controller: _phone,
              enabled: !_busy,
              keyboardType: TextInputType.phone,
              autofillHints: const [AutofillHints.telephoneNumber],
              decoration: InputDecoration(labelText: l.customerSigninPhone),
              onSubmitted: (_) => _requestCode(),
            ),
            const SizedBox(height: 12),
            Text(
              l.customerSigninIntro,
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
          ],
        _Step.code => [
            TextField(
              controller: _phone,
              enabled: false,
              decoration: InputDecoration(labelText: l.customerSigninPhone),
            ),
            const SizedBox(height: 12),
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
        _Step.profile => [
            TextField(
              controller: _name,
              enabled: !_busy,
              autofocus: true,
              textCapitalization: TextCapitalization.words,
              autofillHints: const [AutofillHints.name],
              decoration: InputDecoration(labelText: l.customerSigninName),
              onSubmitted: (_) => _submitProfile(),
            ),
            const SizedBox(height: 20),
            Text(l.customerSigninLangTitle, style: theme.textTheme.titleSmall),
            const SizedBox(height: 8),
            SegmentedButton<String>(
              segments: [
                ButtonSegment(value: 'ru', label: Text(l.customerProfileLangRu)),
                ButtonSegment(value: 'tg', label: Text(l.customerProfileLangTg)),
                ButtonSegment(value: 'en', label: Text(l.customerProfileLangEn)),
              ],
              selected: {_locale},
              onSelectionChanged:
                  _busy ? null : (selection) => setState(() => _locale = selection.first),
            ),
          ],
      };

  List<Widget> _actions(L l) => switch (_step) {
        _Step.phone => [
            FilledButton(
              onPressed: _busy ? null : _requestCode,
              child: Text(_busy ? l.customerPhoneSending : l.customerPhoneSend),
            ),
          ],
        _Step.code => [
            FilledButton(
              onPressed: _busy ? null : _submitCode,
              child: Text(_busy ? l.customerSigninSubmitting : l.customerSigninSubmit),
            ),
            const SizedBox(height: 8),
            TextButton(onPressed: _busy ? null : _requestCode, child: Text(l.customerPhoneResend)),
          ],
        _Step.profile => [
            FilledButton(
              onPressed: _busy ? null : _submitProfile,
              child: Text(_busy ? l.customerSigninSaving : l.customerSigninFinish),
            ),
          ],
      };
}
