import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import '../api/contracts.dart';
import '../api/idempotency.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../shell/actions.dart';
import '../shell/group_header.dart';
import '../theme/space.dart';
import 'start_session_screen.dart';

/// Что прочитали с монитора: код посадки и, если QR его назвал, клуб этого ПК.
class PcSignInLink {
  const PcSignInLink(this.code, {this.organizationId});

  final String code;
  final String? organizationId;
}

/// Код посадки — шесть цифр (SeatingCodePolicy на сервере).
final RegExp _seatingCode = RegExp(r'^\d{6}$');

/// QR на ПК несёт `https://afk4.net/s/{код}?o={клуб}` (спека оболочки, §5.4). Годится и голый код —
/// его игрок набирает руками, если камера не справилась. null — это не наш QR.
PcSignInLink? parsePcSignInLink(String raw) {
  final text = raw.trim();
  if (_seatingCode.hasMatch(text)) return PcSignInLink(text);

  final uri = Uri.tryParse(text.contains('://') ? text : 'https://$text');
  if (uri == null || !uri.host.endsWith('afk4.net')) return null;
  final segments = uri.pathSegments.where((segment) => segment.isNotEmpty).toList();
  if (segments.length != 2 || segments[0] != 's' || !_seatingCode.hasMatch(segments[1])) return null;
  final club = uri.queryParameters['o'];
  return PcSignInLink(segments[1], organizationId: club == null || club.isEmpty ? null : club);
}

/// «Сесть за ПК» — одна дверь вместо двух.
///
/// Раньше на главной было две: «Сесть за ПК» (код с монитора → тариф → время) и «Войти на ПК по
/// QR» (камера → ПК входит сам). Обе начинались одним и тем же — кодом с экрана ПК, — и игрок у
/// монитора должен был угадать, какая из них его. Теперь шаг один: навести камеру на QR или
/// набрать код. Дальше — тариф и время на телефоне, если телефону есть из чего выбрать; иначе, или
/// если игрок сам так решил, ПК просто впускает его, а время выбирается на экране ПК.
///
/// Возвращает имя места, если сессия началась с телефона: главная скажет «садитесь за ПК 07».
class PcSignInScreen extends StatefulWidget {
  const PcSignInScreen({
    super.key,
    required this.api,
    this.initialLink,
    this.enableCamera = true,
    this.branchId,
    this.pinSet,
  });

  final PlayerApiClient api;

  /// Зал игрока — из профиля. Без него на телефоне не из чего выбрать тариф, и ПК впускает игрока
  /// сразу. Он же отсекает чужой клуб: QR другого клуба ведёт только ко входу.
  final String? branchId;

  /// Задан ли ПИН — для предупреждения на шаге тарифа.
  final bool? pinSet;

  /// Ссылка, которой открыли приложение (системная камера): тогда сканировать уже нечего.
  final PcSignInLink? initialLink;

  /// Камера в тестах не нужна.
  final bool enableCamera;

  @override
  State<PcSignInScreen> createState() => _PcSignInScreenState();
}

enum _Stage { scanning, sending, waiting, done, failed }

class _PcSignInScreenState extends State<PcSignInScreen> {
  /// Сколько ждать, пока ПК заберёт заявку. Дольше — значит ПК не на связи, и честнее сказать это.
  static const Duration _waitLimit = Duration(seconds: 45);
  static const Duration _pollEvery = Duration(milliseconds: 1500);

  final TextEditingController _code = TextEditingController();
  _Stage _stage = _Stage.scanning;
  String? _seat;
  String? _problem;
  Timer? _poll;

  /// Ключ текущей попытки входа. Сеть моргнула или ПК не ответил вовремя — ключ переживает
  /// повтор: код одноразовый, и уже погашенный первой попыткой код на новый ключ ответил бы
  /// «код неверен». Ключ отпускается, только когда сервер сам вынес решение — успех, отказ с
  /// кодом или статус заявки.
  final AttemptKey _attempt = AttemptKey();

  @override
  void initState() {
    super.initState();
    final link = widget.initialLink;
    if (link != null) {
      WidgetsBinding.instance.addPostFrameCallback((_) => _submit(link));
    }
  }

  @override
  void dispose() {
    _poll?.cancel();
    _code.dispose();
    super.dispose();
  }

  void _onDetect(BarcodeCapture capture) {
    if (_stage != _Stage.scanning) return;
    for (final barcode in capture.barcodes) {
      final link = parsePcSignInLink(barcode.rawValue ?? '');
      if (link != null) {
        HapticFeedback.mediumImpact();
        _proceed(link);
        return;
      }
    }
  }

  Future<void> _submit(PcSignInLink link) async {
    final l = L.of(context);
    setState(() {
      _stage = _Stage.sending;
      _problem = null;
    });
    // Намерение — этот код в этом клубе: сменился код или QR назвал другой клуб — попытка
    // другая, и ей нужен свой ключ.
    final idempotencyKey = _attempt.forSubject('${link.code}:${link.organizationId ?? ''}');
    try {
      final claim = await widget.api.claimPcSignIn(
        seatingCode: link.code,
        idempotencyKey: idempotencyKey,
        organizationId: link.organizationId,
      );
      if (!mounted) return;
      _follow(claim, link.organizationId);
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      // Сервер ответил decisively (4xx со своим кодом) — заявка решена, повтор пойдёт с новым
      // ключом. Сеть моргнула (isOffline) — сервер мог и принять запрос, ключ должен пережить.
      if (!error.isOffline) _attempt.done();
      _fail(_problemText(l, error));
    }
  }

  void _follow(PlayerSignInClaimDto claim, String? organizationId) {
    if (claim.status == PlayerSignInClaimStatusNames.redeemed) {
      _attempt.done();
      _succeed(claim.seatLabel);
      return;
    }
    if (claim.status == PlayerSignInClaimStatusNames.expired) {
      _attempt.done();
      _fail(L.of(context).customerPcSignInExpired);
      return;
    }

    setState(() {
      _stage = _Stage.waiting;
      _seat = claim.seatLabel;
    });
    final deadline = DateTime.now().add(_waitLimit);
    _poll?.cancel();
    _poll = Timer.periodic(_pollEvery, (timer) async {
      if (DateTime.now().isAfter(deadline)) {
        timer.cancel();
        // Клиент устал ждать — сервер тут ни при чём, заявка может быть ещё жива. Ключ остаётся:
        // повтор должен выкупить ту же заявку, а не наткнуться на уже погашенный код.
        if (mounted) _fail(L.of(context).customerPcSignInExpired);
        return;
      }
      try {
        final next = await widget.api.pcSignInClaim(claim.claimId, organizationId: organizationId);
        if (!mounted || _stage != _Stage.waiting) return;
        if (next.status == PlayerSignInClaimStatusNames.redeemed) {
          timer.cancel();
          _attempt.done();
          _succeed(next.seatLabel);
        } else if (next.status == PlayerSignInClaimStatusNames.expired) {
          timer.cancel();
          _attempt.done();
          _fail(L.of(context).customerPcSignInExpired);
        }
      } on PlayerApiException {
        // Сеть моргнула — следующий опрос попробует снова; предел ожидания всё равно сработает.
      }
    });
  }

  void _succeed(String? seat) {
    HapticFeedback.heavyImpact();
    setState(() {
      _stage = _Stage.done;
      _seat = seat;
    });
  }

  void _fail(String problem) {
    _poll?.cancel();
    setState(() {
      _stage = _Stage.failed;
      _problem = problem;
    });
  }

  static String _problemText(L l, PlayerApiException error) => switch (error.message) {
        _ when error.isOffline => l.customerErrorOffline,
        'seating_code_invalid' => l.customerPcSignInInvalid,
        'seating_code_attempts_exceeded' => l.customerPcSignInTooMany,
        'session_not_yours' => l.customerPcSignInBusy,
        'platform_account_required' => l.customerPcSignInNeedsAccount,
        'device_not_assigned' => l.customerPcSignInNotReady,
        'device_in_maintenance' => l.customerPcSignInMaintenance,
        // ПК сверх бесплатного тарифа клуба: место есть, а сессию на нём не начать — та же
        // фраза, что и на экране самостоятельной посадки.
        'device_outside_plan' => l.customerPlayErrOutsidePlan,
        _ => l.customerPcSignInFailed,
      };

  void _submitTyped() {
    final link = parsePcSignInLink(_code.text);
    if (link == null) {
      setState(() => _problem = L.of(context).customerPcSignInInvalid);
      return;
    }
    FocusScope.of(context).unfocus();
    _proceed(link);
  }

  /// Код прочитан. Тариф выбирается на телефоне, только когда есть где: зал известен и QR этого же
  /// клуба. Иначе — сразу вход, время выберут на ПК.
  Future<void> _proceed(PcSignInLink link) async {
    final branchId = widget.branchId;
    final club = link.organizationId;
    final sameClub = club == null || club == widget.api.session?.organizationId;
    if (branchId == null || !sameClub) {
      await _submit(link);
      return;
    }

    final outcome = await Navigator.of(context).push<SitDownOutcome>(
      MaterialPageRoute(
        builder: (_) => StartSessionScreen(
          api: widget.api,
          branchId: branchId,
          seatingCode: link.code,
          pinSet: widget.pinSet,
        ),
      ),
    );
    if (!mounted) return;
    switch (outcome) {
      case SessionStarted(:final seatName):
        Navigator.of(context).pop(seatName);
      case ChooseOnPc():
        await _submit(link);
      case null:
        // Вернулись назад — остаёмся на шаге кода: может, человек ошибся монитором.
        break;
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: Text(l.customerPlayStart)),
      body: SafeArea(
        child: switch (_stage) {
          _Stage.scanning => ListView(
              padding: const EdgeInsets.all(Space.screen),
              children: [
                Text(l.customerPcSignInHint, style: theme.textTheme.bodyMedium),
                const SizedBox(height: Space.s4),
                if (widget.enableCamera) ...[
                  ClipRRect(
                    borderRadius: BorderRadius.circular(Space.s4),
                    child: AspectRatio(
                      aspectRatio: 1,
                      child: MobileScanner(
                        onDetect: _onDetect,
                        errorBuilder: (context, error) => Container(
                          color: theme.colorScheme.surfaceContainerHighest,
                          alignment: Alignment.center,
                          padding: const EdgeInsets.all(Space.s6),
                          child: Text(
                            error.errorCode == MobileScannerErrorCode.permissionDenied
                                ? l.customerPcSignInCameraDenied
                                : l.customerPcSignInCameraFailed,
                            textAlign: TextAlign.center,
                          ),
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(height: Space.s5),
                ],
                GroupHeader(l.customerPcSignInOrType),
                TextField(
                  controller: _code,
                  keyboardType: TextInputType.number,
                  maxLength: 6,
                  inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                  decoration: InputDecoration(
                    labelText: l.customerPcSignInCodeLabel,
                    errorText: _problem,
                    counterText: '',
                  ),
                  onSubmitted: (_) => _submitTyped(),
                ),
              ],
            ),
          _Stage.sending || _Stage.waiting => Center(
              child: Padding(
                padding: const EdgeInsets.all(Space.s6),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const CircularProgressIndicator(),
                    const SizedBox(height: Space.s4),
                    Text(
                      _stage == _Stage.waiting && _seat != null
                          ? l.customerPcSignInWaitingSeat(_seat!)
                          : l.customerPcSignInWaiting,
                      textAlign: TextAlign.center,
                      style: theme.textTheme.titleMedium,
                    ),
                  ],
                ),
              ),
            ),
          _Stage.done => _Outcome(
              icon: Icons.check_circle_outline,
              color: theme.colorScheme.primary,
              title: _seat != null ? l.customerPcSignInDoneSeat(_seat!) : l.customerPcSignInDone,
              body: l.customerPcSignInDoneHint,
            ),
          _Stage.failed => _Outcome(
              icon: Icons.error_outline,
              color: theme.colorScheme.error,
              title: _problem ?? l.customerPcSignInFailed,
            ),
        },
      ),
      // Главное действие каждого шага — у низа экрана, под большим пальцем.
      bottomNavigationBar: switch (_stage) {
        _Stage.scanning => PinnedActions(
            child: ActionStack(primary: AppAction(l.customerPcSignInNext, _submitTyped)),
          ),
        _Stage.done => PinnedActions(
            child: ActionStack(
              primary: AppAction(l.customerPcSignInClose, () => Navigator.of(context).maybePop()),
            ),
          ),
        _Stage.failed => PinnedActions(
            child: ActionStack(
              primary: AppAction(
                l.customerCommonRetry,
                () => setState(() {
                  _stage = _Stage.scanning;
                  _problem = null;
                }),
              ),
            ),
          ),
        _ => null,
      },
    );
  }
}

/// Итог шага: значок, крупная строка и пояснение под ней.
class _Outcome extends StatelessWidget {
  const _Outcome({required this.icon, required this.color, required this.title, this.body});

  final IconData icon;
  final Color color;
  final String title;
  final String? body;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(Space.s6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 56, color: color),
            const SizedBox(height: Space.s4),
            Text(title, textAlign: TextAlign.center, style: theme.textTheme.titleLarge),
            if (body case final text?) ...[
              const SizedBox(height: Space.s2),
              Text(
                text,
                textAlign: TextAlign.center,
                style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
