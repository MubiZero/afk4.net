# Setup Wizard — шаг 4 create-only: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Свести шаг 4 визарда («Игровое место») к одному действию — задать имя и создать под него место; список/поиск/тумблер/empty-состояния убрать. Расстановка по карте остаётся в Operator App.

**Architecture:** `DeviceScreen.tsx` переписывается на единственное поле имени. На submit для роли `gaming_pc` визард создаёт место (имя = имя ПК) в зоне по умолчанию филиала (`branch.zones[0]`, всегда сидится как «Main Hall»), затем enroll-ит устройство в это место; для `manager_workstation` — enroll без места. Бэкенд не меняется.

**Tech Stack:** React + TypeScript, Vite, `@afk4/i18n` (flat-JSON локали + codegen), bun test (happy-dom + @testing-library/react).

**Спека:** `docs/superpowers/specs/2026-06-13-setup-wizard-step4-slim-seat-create-design.md`

---

## File Structure

- **Modify** `src/AFK4.SetupWizard.Web/src/DeviceScreen.tsx` — полная замена на slim-вариант (одно поле + submit-поток create→enroll).
- **Create** `src/AFK4.SetupWizard.Web/src/DeviceScreen.test.tsx` — тесты целевого поведения.
- **Modify** `src/AFK4.SetupWizard.Web/src/styles.css` — удалить мёртвые стили списка/сетки/тулбара/формы создания/empty.
- **Modify** `locales/ru.json`, `locales/en.json`, `locales/tg.json` — удалить ключи `setup.wizard.device.seats.*` и `setup.wizard.device.create.*`; обновить `setup.wizard.device.gaming.subtitle`.
- Регенерация типов: `packages/i18n` → `bun run gen`.

`wizardApi.ts` не трогаем — типы `WizardSeatDraft`/`WizardEnrollDraft`/`WizardInstallClient` уже подходят.

---

## Task 1: Тесты целевого поведения DeviceScreen

**Files:**
- Test: `src/AFK4.SetupWizard.Web/src/DeviceScreen.test.tsx` (create)

`DeviceScreen` принимает `installClient` пропом — мокаем объект напрямую, `mock.module` не нужен. `isHostBridgeUnavailableError` импортируется реально (чистый предикат).

- [ ] **Step 1: Написать падающий тест-файл**

```tsx
import { describe, it, expect, mock, beforeEach } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { WizardBranch, WizardEnrollResult, WizardSeat } from './wizardApi';
import { DeviceScreen } from './DeviceScreen';

const ZONE = { zoneId: 'z-1', name: 'Main Hall', sortOrder: 0 };
const BRANCH: WizardBranch = {
  branchId: 'b-1',
  branchSlug: 'main',
  branchName: 'Главный',
  zones: [ZONE],
  seats: [],
  freeSeatIds: [],
};
const CREATED_SEAT: WizardSeat = {
  seatId: 's-1',
  pcName: 'PC-NEW',
  zoneId: 'z-1',
  zoneName: 'Main Hall',
  sortOrder: 1,
  status: 'Free',
  deviceId: null,
  deviceName: null,
  isOnline: null,
};
const ENROLL_RESULT = { deviceId: 'd-1', displayName: 'PC-NEW' } as unknown as WizardEnrollResult;

const createSeat = mock(async () => CREATED_SEAT);
const enrollDevice = mock(async () => ENROLL_RESULT);

function renderScreen(props: Partial<Parameters<typeof DeviceScreen>[0]> = {}) {
  const onEnrolled = mock(() => {});
  const onBack = mock(() => {});
  render(
    <I18nProvider>
      <DeviceScreen
        installClient={{ createSeat, enrollDevice }}
        ownerName="Сотрудник"
        branch={BRANCH}
        role="gaming_pc"
        defaultDisplayName="PREVIEW-PC"
        onEnrolled={onEnrolled}
        onBack={onBack}
        {...props}
      />
    </I18nProvider>,
  );
  return { onEnrolled, onBack };
}

const submit = () => fireEvent.click(screen.getByRole('button', { name: /зарегистрировать/i }));

describe('DeviceScreen (create-only)', () => {
  beforeEach(() => {
    createSeat.mockClear();
    enrollDevice.mockClear();
    createSeat.mockImplementation(async () => CREATED_SEAT);
    enrollDevice.mockImplementation(async () => ENROLL_RESULT);
  });

  it('gaming_pc: creates a seat named after the PC, then enrolls into it', async () => {
    const { onEnrolled } = renderScreen();
    submit();
    await waitFor(() => expect(enrollDevice).toHaveBeenCalledTimes(1));
    expect(createSeat).toHaveBeenCalledWith({
      branchId: 'b-1',
      zoneId: 'z-1',
      zoneName: 'Main Hall',
      name: 'PREVIEW-PC',
    });
    expect(enrollDevice).toHaveBeenCalledWith({
      branchId: 'b-1',
      seatId: 's-1',
      role: 'gaming_pc',
      displayName: 'PREVIEW-PC',
    });
    expect(onEnrolled).toHaveBeenCalledWith(ENROLL_RESULT, CREATED_SEAT);
  });

  it('manager_workstation: enrolls without a seat and never creates one', async () => {
    const { onEnrolled } = renderScreen({ role: 'manager_workstation' });
    submit();
    await waitFor(() => expect(enrollDevice).toHaveBeenCalledTimes(1));
    expect(createSeat).not.toHaveBeenCalled();
    expect(enrollDevice).toHaveBeenCalledWith({
      branchId: 'b-1',
      seatId: null,
      role: 'manager_workstation',
      displayName: 'PREVIEW-PC',
    });
    expect(onEnrolled).toHaveBeenCalledWith(ENROLL_RESULT, null);
  });

  it('blocks enroll when the name is too short', async () => {
    renderScreen({ defaultDisplayName: 'PC' });
    expect(screen.getByRole('button', { name: /зарегистрировать/i })).toBeDisabled();
    submit();
    expect(createSeat).not.toHaveBeenCalled();
    expect(enrollDevice).not.toHaveBeenCalled();
  });

  it('shows an error and skips enroll when seat creation fails', async () => {
    createSeat.mockImplementation(async () => {
      throw new Error('Сервер недоступен');
    });
    renderScreen();
    submit();
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Сервер недоступен'));
    expect(enrollDevice).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Запустить — убедиться, что падает**

Run: `cd src/AFK4.SetupWizard.Web && ~/.bun/bin/bun test DeviceScreen`
Expected: FAIL — текущий `DeviceScreen` рендерит список мест и требует выбора, поэтому submit для `gaming_pc` не вызывает `createSeat` автоматически (первый тест падает на ожидании `enrollDevice`).

- [ ] **Step 3: Коммит падающего теста**

```bash
git add src/AFK4.SetupWizard.Web/src/DeviceScreen.test.tsx
git commit -m "test(setup-wizard): target slim create-only behavior for step 4"
```

---

## Task 2: Переписать DeviceScreen на create-only

**Files:**
- Modify: `src/AFK4.SetupWizard.Web/src/DeviceScreen.tsx` (полная замена содержимого)

- [ ] **Step 1: Заменить весь файл на slim-вариант**

```tsx
import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { ArrowLeft, ArrowRight, Loader2 } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { isHostBridgeUnavailableError } from './hostBridge';
import {
  type WizardBranch,
  type WizardEnrollResult,
  type WizardInstallClient,
  type WizardRole,
  type WizardSeat,
} from './wizardApi';

interface DeviceScreenProps {
  installClient: WizardInstallClient;
  ownerName: string;
  branch: WizardBranch;
  role: WizardRole;
  defaultDisplayName: string;
  onEnrolled(result: WizardEnrollResult, selectedSeat: WizardSeat | null): void;
  onBusyChange?(installing: boolean): void;
  onBack(): void;
}

type RequestState =
  | { kind: 'idle' }
  | { kind: 'creating' }
  | { kind: 'enrolling' }
  | { kind: 'error'; message: string };

export function DeviceScreen({
  installClient,
  ownerName,
  branch,
  role,
  defaultDisplayName,
  onEnrolled,
  onBusyChange,
  onBack,
}: DeviceScreenProps) {
  const { t } = useI18n();
  const [displayName, setDisplayName] = useState(defaultDisplayName);
  const [request, setRequest] = useState<RequestState>({ kind: 'idle' });

  const requiresSeat = role === 'gaming_pc';
  const trimmedDisplayName = displayName.trim();
  const displayNameValid = trimmedDisplayName.length >= 3 && trimmedDisplayName.length <= 32;
  // Место всегда лежит в зоне; новый филиал сидится с зоной по умолчанию («Main Hall»),
  // поэтому defaultZone практически всегда есть. Guard оставляем на случай пустого филиала.
  const defaultZone = branch.zones[0] ?? null;
  const canEnroll = displayNameValid && (!requiresSeat || defaultZone !== null);

  const busy = request.kind === 'creating' || request.kind === 'enrolling';
  // Титлбар защищает кнопку закрытия, пока идёт enroll (msiexec на хосте).
  const installing = request.kind === 'enrolling';
  useEffect(() => {
    onBusyChange?.(installing);
    return () => onBusyChange?.(false);
  }, [installing, onBusyChange]);

  const handleSubmit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      if (!canEnroll || busy) {
        return;
      }
      try {
        let seat: WizardSeat | null = null;
        if (requiresSeat) {
          setRequest({ kind: 'creating' });
          seat = await installClient.createSeat({
            branchId: branch.branchId,
            zoneId: defaultZone!.zoneId,
            zoneName: defaultZone!.name,
            name: trimmedDisplayName,
          });
        }
        setRequest({ kind: 'enrolling' });
        const result = await installClient.enrollDevice({
          branchId: branch.branchId,
          seatId: seat ? seat.seatId : null,
          role,
          displayName: trimmedDisplayName,
        });
        onEnrolled(result, seat);
      } catch (error) {
        setRequest({ kind: 'error', message: messageForError(error, t) });
      }
    },
    [
      branch.branchId,
      busy,
      canEnroll,
      defaultZone,
      installClient,
      onEnrolled,
      requiresSeat,
      role,
      t,
      trimmedDisplayName,
    ],
  );

  const titleKey: MessageKey = requiresSeat
    ? 'setup.wizard.device.gaming.title'
    : 'setup.wizard.device.manager.title';
  const subtitleKey: MessageKey = requiresSeat
    ? 'setup.wizard.device.gaming.subtitle'
    : 'setup.wizard.device.manager.subtitle';

  return (
    <section className="wizard-screen is-framed is-narrow">
      <div className="wizard-screen-head">
        <span className="wizard-screen-context">{ownerName} · {branch.branchName}</span>
        <div className="wizard-screen-title-row">
          <span className="wizard-screen-step" aria-hidden>4</span>
          <h1>{t(titleKey)}</h1>
        </div>
        <p>{t(subtitleKey)}</p>
      </div>

      <form className="wizard-form" onSubmit={handleSubmit} noValidate>
        <label className="wizard-field">
          <span className="wizard-field-label">{t('setup.wizard.device.field.name')}</span>
          <input
            type="text"
            value={displayName}
            autoComplete="off"
            spellCheck={false}
            minLength={3}
            maxLength={32}
            autoFocus
            onChange={(event) => setDisplayName(event.target.value)}
            placeholder={defaultDisplayName}
            aria-invalid={trimmedDisplayName.length > 0 && !displayNameValid}
            aria-describedby="display-name-hint"
          />
          <span id="display-name-hint" className="wizard-field-hint">
            {t('setup.wizard.device.field.nameHint')}
          </span>
        </label>

        {request.kind === 'error' && (
          <div role="alert" className="wizard-alert">
            {request.message}
          </div>
        )}

        <div className="wizard-actions">
          <button type="button" className="wizard-secondary" onClick={onBack} disabled={busy}>
            <ArrowLeft aria-hidden />
            <span>{t('setup.wizard.common.back')}</span>
          </button>
          <button type="submit" className="wizard-primary" disabled={!canEnroll || busy}>
            {busy ? (
              <>
                <Loader2 className="wizard-spinner" aria-hidden />
                <span>{t('setup.wizard.device.action.enrolling')}</span>
              </>
            ) : (
              <>
                <span>{t('setup.wizard.device.action.enroll')}</span>
                <ArrowRight aria-hidden />
              </>
            )}
          </button>
        </div>
      </form>
    </section>
  );
}

function messageForError(error: unknown, t: (key: MessageKey) => string): string {
  if (isHostBridgeUnavailableError(error)) {
    return t('setup.wizard.device.error.bridgeMissing');
  }
  if (error instanceof Error && error.message) {
    return error.message;
  }
  return t('setup.wizard.device.error.generic');
}
```

- [ ] **Step 2: Запустить тесты — убедиться, что проходят**

Run: `cd src/AFK4.SetupWizard.Web && ~/.bun/bin/bun test DeviceScreen`
Expected: PASS — все 4 теста зелёные.

- [ ] **Step 3: tsc-проверка (нет ссылок на удалённые символы)**

Run: `cd src/AFK4.SetupWizard.Web && ~/.bun/bin/bunx tsc -b`
Expected: без ошибок. (i18n-ключи `device.seats.*`/`device.create.*` ещё существуют в локалях — на типах не ломается, даже если не используются.)

- [ ] **Step 4: Коммит**

```bash
git add src/AFK4.SetupWizard.Web/src/DeviceScreen.tsx
git commit -m "feat(setup-wizard): slim step 4 to name + auto-create seat, enroll in one action"
```

---

## Task 3: Удалить мёртвые стили шага 4

**Files:**
- Modify: `src/AFK4.SetupWizard.Web/src/styles.css`

После переписи `DeviceScreen` следующие классы больше не используются на этом экране. Удалять только те, что не используются нигде ещё.

- [ ] **Step 1: Проверить, что классы нигде больше не нужны**

Run:
```bash
cd src/AFK4.SetupWizard.Web && grep -rEn "wizard-seats-section|wizard-seats-head|wizard-seats-list|wizard-seat-grid|wizard-seat-card|wizard-zone-group|wizard-zone-head|wizard-zone-counter|wizard-device-seats-toolbar|wizard-create-seat|wizard-empty|wizard-link-action|wizard-icon-ghost|wizard-toggle" src --include=*.tsx
```
Expected: ни одного совпадения в `.tsx` (если какой-то класс всё же используется другим экраном — НЕ удалять его блок).

- [ ] **Step 2: Удалить из `styles.css` неиспользуемые блоки**

Удалить CSS-правила для классов, подтверждённых на Step 1 как неиспользуемые (блоки `.wizard-seats-section`, `.wizard-seats-head`, `.wizard-seats-list`, `.wizard-seat-grid`, `.wizard-seat-card` и его модификаторы `.is-free/.is-occupied/.is-online/.is-offline/.is-pending`, `.wizard-zone-group`, `.wizard-zone-head`, `.wizard-zone-counter`, `.wizard-device-seats-toolbar`, `.wizard-create-seat` и связанные, `.wizard-empty`/`.wizard-empty-cta`, `.wizard-link-action`, `.wizard-icon-ghost`, `.wizard-toggle`/`.wizard-toggle-track`). Также убрать правило `.wizard-screen.is-wide .wizard-form > .wizard-field { max-width: 420px; }`, если `is-wide` больше не ставится на шаге 4 и не используется иначе (проверить grep'ом `is-wide`).

- [ ] **Step 3: Сборка — убедиться, что не сломали стили/типы**

Run: `cd src/AFK4.SetupWizard.Web && ~/.bun/bin/bun run build`
Expected: `tsc -b && vite build` без ошибок.

- [ ] **Step 4: Коммит**

```bash
git add src/AFK4.SetupWizard.Web/src/styles.css
git commit -m "chore(setup-wizard): drop dead step-4 seat-list styles"
```

---

## Task 4: Вычистить i18n-ключи и поправить копи подзаголовка

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Regen: `packages/i18n` → `bun run gen`

- [ ] **Step 1: Убедиться, что удаляемые ключи не используются в коде**

Run:
```bash
cd /d/afk4.net && grep -rEn "setup\.wizard\.device\.(seats|create)\." src --include=*.ts --include=*.tsx
```
Expected: ни одного совпадения (после Task 2 ссылок не осталось).

- [ ] **Step 2: Удалить ключи `setup.wizard.device.seats.*` и `setup.wizard.device.create.*` из всех трёх локалей**

В `locales/ru.json`, `locales/en.json`, `locales/tg.json` удалить все строки с ключами, начинающимися на:
- `"setup.wizard.device.seats.` (включая `.empty.*`, `.label.*`)
- `"setup.wizard.device.create.`

Локали — плоский JSON, паритет по ключам обязателен — удалять одинаковый набор из всех трёх файлов.

- [ ] **Step 3: Обновить копи `gaming.subtitle` (упоминание выбора места на карте больше неверно)**

`locales/ru.json`:
```json
"setup.wizard.device.gaming.subtitle": "Задайте имя ПК. Разместить его на карте зала можно в панели клуба.",
```
`locales/en.json`:
```json
"setup.wizard.device.gaming.subtitle": "Set a PC name. You can place it on the floor map later in the club dashboard.",
```
`locales/tg.json` (черновой перевод — отдать носителю на ред-пен, НЕ копия ru):
```json
"setup.wizard.device.gaming.subtitle": "Ба ПК ном диҳед. Онро баъдтар дар панели клуб дар харитаи толор ҷойгир кардан мумкин аст.",
```

- [ ] **Step 4: Регенерировать типы сообщений**

Run: `cd packages/i18n && ~/.bun/bin/bun run gen`
Expected: `MessageKey` пересобран без удалённых ключей, без ошибок.

- [ ] **Step 5: Прогнать i18n-тесты (паритет локалей + guard: no CAPS / no «компьютер» / no tg===ru)**

Run: `cd packages/i18n && ~/.bun/bin/bun test`
Expected: PASS — паритет сохранён (удалили из всех трёх), guard зелёный (tg-подзаголовок не равен ru).

- [ ] **Step 6: tsc визарда — убедиться, что удалённые ключи нигде не торчат в типах**

Run: `cd src/AFK4.SetupWizard.Web && ~/.bun/bin/bunx tsc -b`
Expected: без ошибок.

- [ ] **Step 7: Коммит**

```bash
git add locales/ru.json locales/en.json locales/tg.json packages/i18n
git commit -m "i18n(setup-wizard): drop step-4 seat-list keys, fix gaming subtitle copy"
```

---

## Task 5: Финальная проверка всего шага

- [ ] **Step 1: Тесты визарда целиком**

Run: `cd src/AFK4.SetupWizard.Web && ~/.bun/bin/bun test`
Expected: PASS — `DeviceScreen`, `PhoneLoginScreen`, `ForgotPasswordScreen` зелёные.

- [ ] **Step 2: Полная сборка визард-фронта**

Run: `cd src/AFK4.SetupWizard.Web && ~/.bun/bin/bun run build`
Expected: без ошибок.

- [ ] **Step 3: i18n-тесты (ещё раз, после всех правок)**

Run: `cd packages/i18n && ~/.bun/bin/bun test`
Expected: PASS.

- [ ] **Step 4: Ручная проверка в WPF-превью**

Запустить превью (`bun run dev` в `src/AFK4.SetupWizard.Web` + `dotnet run --project src/AFK4.SetupWizard -c Debug -- --preview` с env `AFK4_SETUP_WIZARD_WEB_DEV_SERVER_URL=http://127.0.0.1:5175`), дойти до шага 4 для роли «Игровой ПК»: видно только поле имени + кнопки; ввод имени → «Зарегистрировать» создаёт место и enroll-ит. Для роли «Админ/кассир» — поле имени без места.

---

## Self-Review

- **Spec coverage:** Одно поле имени (Task 2) ✓; create→enroll поток (Task 2 + тесты Task 1) ✓; зона по умолчанию `branch.zones[0]` ✓; manager без места ✓; удаление списка/поиска/toggle/empty/формы (Task 2 + CSS Task 3) ✓; бэкенд без изменений ✓; чистка i18n (Task 4) ✓; обновление неверного подзаголовка (Task 4 Step 3) ✓; тесты (Task 1) ✓; трейд-офф дублей — поведенческий, отдельной задачи не требует.
- **Placeholders:** код приведён полностью во всех шагах, плейсхолдеров нет.
- **Type consistency:** `WizardSeatDraft {branchId, zoneId, zoneName, name}` и `WizardEnrollDraft {branchId, seatId, role, displayName}` совпадают между тестом (Task 1), реализацией (Task 2) и `wizardApi.ts`. `installClient: { createSeat, enrollDevice }` совпадает с `WizardInstallClient`.
- **Примечание по tg:** черновой перевод подзаголовка помечен на ревью носителю (правило инженерной честности — не копировать ru, отдать на ред-пен).
