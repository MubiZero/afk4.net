import { describe, expect, it } from 'bun:test';
import { createTranslator } from '@afk4/i18n';
import { PermissionRefusal, projectOperatorError, requiresManagerApproval, retryCanHelp } from './apiErrors';
import { PlatformApiError } from './platformApi';

const t = createTranslator('ru');

describe('projectOperatorError', () => {
  it('keeps actionable platform error text', () => {
    expect(projectOperatorError(new Error('Platform API returned 401 Unauthorized.'), t)).toEqual({
      title: 'Действие не выполнено',
      detail: 'Platform API returned 401 Unauthorized.',
      retryCanHelp: true
    });
  });

  // Сбой до ответа сервера: fetch бросает TypeError с браузерным текстом («Failed to fetch»).
  // Он попадал на экран кассы как есть — английская строка в момент, когда в руках деньги.
  it('обрыв связи объясняет по-человечески, а не текстом браузера', () => {
    expect(projectOperatorError(new TypeError('Failed to fetch'), t)).toEqual({
      title: 'Действие не выполнено',
      detail: 'Нет связи с сервером. Проверьте сеть и повторите.',
      retryCanHelp: true
    });
  });

  it('uses a stable fallback when the failure has no details', () => {
    expect(projectOperatorError(undefined, t)).toEqual({
      title: 'Действие не выполнено',
      detail: 'Сервер не вернул подробности. Повторите действие или проверьте связь.',
      retryCanHelp: true
    });
  });

  it.each([
    ['reservation_not_found', 'Бронь не найдена. Обновите список броней.'],
    ['organization_mismatch', 'Запрос относится к другой организации. Обновите подключение к клубу.'],
    ['invalid_payment_split', 'Проверьте распределение суммы между способами оплаты.'],
    ['mixed_currency', 'Все части оплаты должны быть в одной валюте.'],
    ['out_of_stock', 'Товара недостаточно на складе. Обновите остатки.'],
    ['idempotency_key_required', 'Не удалось безопасно повторить действие. Попробуйте ещё раз.'],
    ['reservation_already_started', 'Для этой брони сессия уже запущена.'],
    ['version_conflict', 'Данные уже изменились. Обновите их и повторите действие.'],
    ['reservation_confirmation_required', 'Сначала подтвердите бронь, затем запускайте сессию.'],
    ['reservation_expired', 'Время брони уже истекло. Создайте новую бронь или запустите гостевую сессию.'],
    ['seat_unavailable', 'Выбранное место недоступно для запуска сессии.'],
    ['player_required_for_wallet', 'Для выбранного способа оплаты нужен клиент клуба.'],
    ['session_start_invalid', 'Проверьте параметры запуска сессии.'],
    ['idempotency_conflict', 'Этот запрос уже использован с другими параметрами. Обновите данные.'],
    ['open_shift_required', 'Чтобы принять оплату, сначала откройте смену.'],
    ['insufficient_funds', 'На балансе клиента недостаточно средств.'],
    ['session_start_conflict', 'Сессию нельзя запустить из-за конфликта. Обновите данные.'],
    // Касса и смены: раньше эти отказы приезжали без имени, и на экране оставался дамп запроса.
    ['shift_already_open', 'В этом филиале смена уже открыта. Обновите экран — возможно, её открыл кто-то другой.'],
    ['shift_already_closed', 'Смена уже закрыта. Обновите экран, чтобы увидеть её итог.'],
    ['shift_currency_mismatch', 'Валюта операции не совпадает с валютой смены.'],
    ['shift_sign_off_required', 'Расхождение по кассе больше допустимого — закрыть смену можно только с подписью старшего.'],
    ['shift_sign_off_must_differ', 'Подписать расхождение должен не тот, кто открывал или закрывает смену.'],
    ['shift_sign_off_not_authorized', 'У выбранного сотрудника нет права подписывать расхождение по кассе.'],
    ['cash_movement_needs_open_shift', 'Вносить и изымать наличные можно только при открытой смене.'],
    ['tariff_name_taken', 'Тариф с таким названием в филиале уже есть.'],
    // Брони: раньше стойка читала на них английскую фразу сервера.
    ['reservation_not_pending', 'Ответ на эту заявку уже дан. Обновите список броней.'],
    ['reservation_not_seatable', 'Посадить можно заявку или подтверждённую бронь. Обновите список.'],
    ['reservation_seat_required', 'У брони нет места — выберите место, потом сажайте гостя.'],
    ['reservation_cancel_reason_required', 'Напишите причину отмены: её увидит гость.'],
    ['reservation_not_rejectable', 'Отказать можно в заявке, на которую клуб ещё не ответил.'],
    ['reservation_no_show_not_allowed', 'Неявку отмечают у подтверждённой брони, время которой уже началось.']
  ])('переводит отказ %s, когда сервер назвал его полем code', (code, expectedDetail) => {
    // Так отвечает старт брони: код в `code`, человеческое пояснение рядом в `error`.
    const error = new PlatformApiError(
      'request failed',
      409,
      'Conflict',
      JSON.stringify({ code, error: 'backend detail', currentVersion: 4 })
    );

    expect(projectOperatorError(error, t)).toMatchObject({
      title: 'Действие не выполнено',
      detail: expectedDetail
    });
  });

  // Так отвечает касса, склад и смены: `{ error: "<код>" }` и больше ничего. Пока разборщик
  // читал только `code`, оператор видел здесь сырую английскую строку из бэкенда.
  it.each([
    ['open_shift_required', 'Чтобы принять оплату, сначала откройте смену.'],
    ['insufficient_funds', 'На балансе клиента недостаточно средств.'],
    ['out_of_stock', 'Товара недостаточно на складе. Обновите остатки.'],
    ['mixed_currency', 'Все части оплаты должны быть в одной валюте.'],
    ['invalid_payment_split', 'Проверьте распределение суммы между способами оплаты.'],
    ['player_required_for_wallet', 'Для выбранного способа оплаты нужен клиент клуба.'],
    ['version_conflict', 'Данные уже изменились. Обновите их и повторите действие.'],
    ['idempotency_conflict', 'Этот запрос уже использован с другими параметрами. Обновите данные.']
  ])('переводит отказ %s, когда сервер назвал его полем error', (code, expectedDetail) => {
    const error = new PlatformApiError(
      'request failed',
      409,
      'Conflict',
      JSON.stringify({ error: code })
    );

    expect(projectOperatorError(error, t)).toMatchObject({
      title: 'Действие не выполнено',
      detail: expectedDetail
    });
  });

  it('не принимает свободный текст в error за код отказа', () => {
    // `error` у сервера бывает и пояснением для человека. Подставлять под него фразу из
    // словаря нельзя: оператор прочитал бы не то, что случилось.
    const error = new PlatformApiError(
      'request failed',
      400,
      'Bad Request',
      JSON.stringify({ error: 'End time must be after start time.' })
    );

    expect(projectOperatorError(error, t)).toEqual({
      title: 'Действие не выполнено',
      detail: 'Сервер не принял эти данные. Проверьте, что ввели, и повторите.',
      retryCanHelp: false
    });
  });

  // До этого кассир видел на экране строку транспорта целиком: «Platform API returned 400 Bad
  // Request: {"Error":"An open shift already exists for this branch.","Code":null}» — английская
  // фраза и сырой JSON посреди русского экрана.
  it.each([
    [400, 'Сервер не принял эти данные. Проверьте, что ввели, и повторите.'],
    [403, 'Недостаточно прав для этого действия.'],
    [404, 'Того, к чему относится действие, уже нет. Обновите экран.'],
    [409, 'Данные успели измениться. Обновите экран и повторите.'],
    [500, 'Сервер вернул ошибку. Повторите позже.']
  ])('на отказ %s без кода отвечает человеческой фразой, а не дампом запроса', (status, expectedDetail) => {
    const error = new PlatformApiError(
      `Platform API returned ${status}: {"Error":"An open shift already exists for this branch."}`,
      status,
      'Bad Request',
      JSON.stringify({ error: 'An open shift already exists for this branch.', code: null })
    );

    const projection = projectOperatorError(error, t);

    expect(projection.detail).toBe(expectedDetail);
    expect(projection.detail).not.toContain('Platform API');
  });

  it('provides localized copy for an early reservation start warning', () => {
    expect(t('op.booking.start.earlyWarning', { time: '18:30' })).toBe(
      'Бронь начинается в 18:30. При раннем запуске оплата начнётся сейчас.'
    );
  });

  it('превращает отказ по лимиту тарифа во фразу с числами', () => {
    const t = createTranslator('ru');
    const error = new PlatformApiError('conflict', 409, 'Conflict', JSON.stringify({
      code: 'plan_limit_reached',
      planLimit: { code: 'plan_limit_reached', limitName: 'concurrent_sessions', limit: 40, current: 40, planCode: 'growth' }
    }));

    const projection = projectOperatorError(error, t);

    expect(projection.detail).toContain('40');
  });
});

// Развилка «сумма выше порога сотрудника»: сервер отвечает 409 с requiresApproval, и это не
// запрет, а предложение отправить старшему. Жёсткий лимит (422) отправлять некуда — спутать
// их значит пообещать очередь одобрений там, где её не будет.
describe('requiresManagerApproval', () => {
  const error = (status: number, body: string) =>
    new PlatformApiError('failed', status, 'Conflict', body);

  it('распознаёт отказ по порогу', () => {
    expect(requiresManagerApproval(
      error(409, JSON.stringify({ error: 'Amount exceeds the approval threshold', requiresApproval: true }))
    )).toBe(true);
  });

  it('не путает его с жёстким лимитом', () => {
    expect(requiresManagerApproval(
      error(422, JSON.stringify({ error: 'Amount exceeds the configured per-transaction or daily cap.' }))
    )).toBe(false);
  });

  it('не срабатывает на другом конфликте', () => {
    expect(requiresManagerApproval(error(409, JSON.stringify({ error: 'version_conflict' })))).toBe(false);
  });

  it('переживает нечитаемое тело и посторонние ошибки', () => {
    expect(requiresManagerApproval(error(409, 'not json'))).toBe(false);
    expect(requiresManagerApproval(new Error('boom'))).toBe(false);
    expect(requiresManagerApproval(undefined)).toBe(false);
  });
});

// «Повторить» под отказом, который повтор не исправит, обещает то, чего не будет: сколько ни жми,
// ответ тот же. Человек жмёт по кругу и решает, что сломалась программа, а настоящее действие —
// попросить доступ или поправить данные — на экране не названо.
describe('retryCanHelp', () => {
  const refusal = (status: number, body = '') => new PlatformApiError('failed', status, 'Status', body);

  it.each([401, 403, 404, 400, 422])('на отказ %s повтор не поможет', (status) => {
    expect(retryCanHelp(refusal(status))).toBe(false);
  });

  it.each([0, 408, 429, 500, 502, 503, 504])('на сбой %s повтор поможет', (status) => {
    expect(retryCanHelp(refusal(status))).toBe(true);
  });

  it('обрыв связи до ответа сервера проходит повтором', () => {
    expect(retryCanHelp(new TypeError('Failed to fetch'))).toBe(true);
  });

  // Правило бизнеса не меняется от того, что запрос отправили ещё раз.
  it.each([
    ['out_of_stock', 409],
    ['open_shift_required', 409],
    ['reservation_not_found', 404],
    ['plan_limit_reached', 409],
    ['too_many_password_attempts', 429]
  ])('отказ по правилу %s повтором не проходит', (code, status) => {
    expect(retryCanHelp(refusal(status, JSON.stringify({ code })))).toBe(false);
  });

  // Эти отказы сами просят обновить и повторить: данные изменились у соседа, и свежий запрос
  // получит свежий ответ.
  it.each(['version_conflict', 'stale_version'])('конфликт версии %s повтор проходит', (code) => {
    expect(retryCanHelp(refusal(409, JSON.stringify({ code })))).toBe(true);
  });

  it('конфликт без кода проходит повтором: данные успели измениться', () => {
    expect(retryCanHelp(refusal(409))).toBe(true);
  });

  it('порог одобрения повтором не проходит: нужен старший, а не вторая попытка', () => {
    expect(retryCanHelp(refusal(409, JSON.stringify({ requiresApproval: true })))).toBe(false);
  });

  it('отказ по правам, который приложение проверило само, повтором не проходит', () => {
    expect(retryCanHelp(new PermissionRefusal('Нет права смотреть чеки.'))).toBe(false);
  });

  it('пустой и неизвестный отказ повтор не запрещает', () => {
    expect(retryCanHelp(undefined)).toBe(true);
    expect(retryCanHelp(new Error('boom'))).toBe(true);
  });

  it('проекция несёт тот же ответ рядом с причиной', () => {
    expect(projectOperatorError(refusal(403), t).retryCanHelp).toBe(false);
    expect(projectOperatorError(refusal(503), t).retryCanHelp).toBe(true);
  });
});

// Единственное настоящее действие при нехватке прав — попросить доступ. Экран должен сказать,
// у кого, иначе сотрудник не знает, чинить ли права или ждать сервер.
describe('accessHint', () => {
  it('на 403 называет, к кому идти за доступом', () => {
    expect(projectOperatorError(new PlatformApiError('failed', 403, 'Forbidden', ''), t).accessHint)
      .toBe('Попросите доступ у управляющего или владельца организации.');
  });

  it('то же для отказа, который приложение проверило само', () => {
    const projection = projectOperatorError(new PermissionRefusal('Нет права смотреть чеки.'), t);
    expect(projection.detail).toBe('Нет права смотреть чеки.');
    expect(projection.accessHint).toBe('Попросите доступ у управляющего или владельца организации.');
  });

  // 401 — истёкший вход, а не нехватка прав: управляющий тут ничем не поможет.
  it.each([401, 404, 500])('на %s подсказки про доступ нет', (status) => {
    expect(projectOperatorError(new PlatformApiError('failed', status, 'Status', ''), t).accessHint).toBeUndefined();
  });
});
