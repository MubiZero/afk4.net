# Operator Foundation — Primitives (Toast / Skeleton / EmptyState)

**Дата:** 2026-06-15
**Статус:** дизайн на ревью
**Охват:** AFK4.Operator.App.Web — кусок «Примитивы» Этапа 0 (фундамент, §6.2 спека `2026-06-14-operator-foundation-design.md`)

## Контекст

Этап 0 §6.2 объявляет обязательный набор примитивов и требует завести недостающие **Toast / Skeleton /
EmptyState** по принципу **«канон сейчас, извлечение по ходу» (НЕ big-bang)**. Разведка кода показала:

- `useDeferredFlag(active, 180ms)` — отложенный спиннер (§6.3) **уже реализован** (`useDeferredFlag.ts`). Не трогаем.
- `FeedbackNotice` (`operatorPrimitives.tsx`) — **инлайн** уведомление (`role="status"`, `aria-live="polite"`),
  драйвится типом `Feedback = { label, state, detail? }`, живёт в ~11 воркспейсах. Это контекстное, не
  эфемерное. Остаётся как есть.
- **Skeleton** и **EmptyState** существуют как **разрозненные ad-hoc CSS-классы**: `.skeleton-block` +
  keyframe `skeleton-pulse` (+ геометрия `seat-skeleton`, `dashboard-skeleton-*`), `map-empty-state`,
  `shop-orders-empty`. Каждый воркспейс лепит по-своему.
- **Toast** — отсутствует полностью (net-new).

**Граница куска (решено в брейншторме):** «Ввести + догфуд». Построить 3 примитива с полными состояниями
и тестами, подключить `ToastProvider` в корень, и перевести на них **только очевидные** ad-hoc точки
(map/dashboard/shop-orders) + один реальный вызов Toast — чтобы не было dead code. Массовую миграцию НЕ
делаем (по ходу этапов). Полная консолидация всех loading/empty/feedback и `FeedbackNotice→Toast` —
**вне скоупа** (спек запрещает big-bang, #22).

## 1. Toast (net-new)

**Файл:** новый `src/AFK4.Operator.App.Web/src/operatorToast.tsx` — отдельно от `operatorPrimitives.tsx`,
т.к. это подсистема (провайдер + контекст + очередь + вьюпорт), одна ответственность (#16).

**Публичный API:**

```ts
type ToastTone = 'success' | 'error' | 'info';
interface ToastAction { label: string; onClick: () => void }
interface ToastOptions { tone: ToastTone; message: string; durationMs?: number; action?: ToastAction }

interface ToastApi {
  show: (options: ToastOptions) => string;       // returns toast id
  success: (message: string, options?: Omit<ToastOptions, 'tone' | 'message'>) => string;
  error:   (message: string, options?: Omit<ToastOptions, 'tone' | 'message'>) => string;
  info:    (message: string, options?: Omit<ToastOptions, 'tone' | 'message'>) => string;
  dismiss: (id: string) => void;
}
function useToast(): ToastApi;
function ToastProvider({ children }: { children: ReactNode }): JSX.Element;
```

- **Тоны (3):** `success` / `error` / `info`. **Warning намеренно отсутствует** — предупреждения по природе
  контекстные (инлайн), не эфемерные. Каждый тон = токен-цвет (success / danger / accent) + иконка lucide +
  подпись (статус не только цветом — доступность, §4.1 спека Этапа 0).
- **Очередь:** максимум **3 видимых** одновременно, FIFO; превышение — ждёт, появляется по мере
  освобождения слотов.
- **Авто-dismiss:** `success`/`info` — **4000 мс** (дефолт `durationMs`); **`error` — липкий** (не
  авто-исчезает, `durationMs` игнорируется, закрывается только вручную). У всех тостов есть кнопка закрытия.
- **Позиция:** фиксированный вьюпорт **низ-право**, вертикальный стек снизу вверх (десктоп-конвенция, не
  перекрывает рабочую зону; контекст — тёмный зал, §30 рабочего стиля). Отступы — `--space-*`.
- **Опциональное действие:** `action?: { label, onClick }` рендерится кнопкой в тосте (заготовка под
  Undo/optimistic, §6.3). Сейчас контракт заложен, но реально не дёргается (кроме теста).
- **Motion:** вход/выход — `--duration-medium` + `--ease-out` (transform+opacity). `prefers-reduced-motion`
  → без анимации, мгновенно (§6.3).
- **Доступность:** вьюпорт `role="region"` с `aria-label`; тост `role="status"` (success/info, polite) либо
  `role="alert"` (error, assertive); кнопка закрытия с aria-label.
- **i18n:** `message` передаёт caller уже переведённым (как у `FeedbackNotice`). Служебные подписи (close,
  aria-label вьюпорта) — через `@afk4/i18n` (ключи `op.toast.*`, в ru/en/tg).

**Монтирование:** `ToastProvider` оборачивает корневой компонент приложения **внутри** `I18nProvider`
(чтобы `useToast` и его i18n-подписи работали везде). Вьюпорт рендерится в DOM приложения (внутри темы).

## 2. Skeleton (извлечение)

**Файл:** `operatorPrimitives.tsx` (примитив, не подсистема).

```ts
function Skeleton({ variant = 'block', lines = 1, className }:
  { variant?: 'block' | 'text' | 'circle'; lines?: number; className?: string }): JSX.Element;
```

- Рендерит существующий `.skeleton-block` (+ модификатор формы для `text`/`circle`), всегда `aria-hidden`.
- `variant="text"` с `lines>1` → стопка строк-плейсхолдеров.
- Переиспользует keyframe `skeleton-pulse`; `prefers-reduced-motion` → пульс выключен (статичный блок).
- Контейнер-паттерн без изменений: caller оборачивает группу скелетонов в `role="status"` с `aria-label`
  (как сейчас в map/dashboard).
- **Догфуд:** `MapWorkspace` (сидушки) и `DashboardWorkspace` (блоки) переходят на `<Skeleton>`,
  геометрия-классы (`seat-skeleton`, `dashboard-skeleton-*`) сохраняются как `className`.

## 3. EmptyState (извлечение)

**Файл:** `operatorPrimitives.tsx`.

```ts
function EmptyState({ icon, title, description, action }:
  { icon?: ReactNode; title: string; description?: string; action?: { label: string; onClick: () => void } }): JSX.Element;
```

- Рендер: опц. иконка, `title` (strong), опц. `description` (span), опц. кнопка-действие (primary).
- Покрывает оба текущих ad-hoc варианта: `map-empty-state` (strong+span) и `shop-orders-empty` (одна строка).
- **Догфуд:** `MapWorkspace` (пусто) и `ShopOrdersWorkspace` (пусто) переходят на `<EmptyState>`.

## 4. Догфуд Toast

Один реальный `success`-тост на уже существующем успешном действии, чтобы примитив был живым, а не dead
code (#37). Кандидат: успешное сохранение в `BackendSettingsWorkspace` (или копирование кода). Точное место
фиксируется в плане; критерий — наименее инвазивная точка, где успех сейчас не показывается тостом.

## 5. CSS / токены

- Новые классы `.toast-viewport`, `.toast`, тон-модификаторы — в `styles.css`, цвета/радиусы/моушн только
  токенами (`--duration-*`, `--ease-*`, `--radius-*`, `--space-*`; цвета — success/danger/accent токены).
  Light/dark — паритет (§6.4), ноль hex в `.tsx`.
- Skeleton/EmptyState переиспользуют существующие классы; добавляются только модификаторы вариантов.
- `prefers-reduced-motion` — в CSS (отключает пульс и тост-анимации).

## 6. Тестирование (bun test + happy-dom + jest-dom)

- **Toast** (`operatorToast.test.tsx`): `success`/`error`/`info` рендерят сообщение и роль; авто-dismiss
  success/info по таймеру (fake timers); error НЕ исчезает по таймеру; очередь ограничена 3 видимыми;
  `dismiss(id)` убирает; `action` рендерит кнопку и дёргает `onClick`; `prefers-reduced-motion` — без анимации.
- **Skeleton / EmptyState** (в `operatorPrimitives.test.tsx` или новом): варианты Skeleton рендерят
  `aria-hidden`; EmptyState рендерит title/description/action, кнопка дёргает `onClick`.
- **Регрессия догфуда:** существующие тесты `App.test.tsx` / воркспейсов остаются зелёными.

## 7. Критерии готовности

- Toast/Skeleton/EmptyState заведены, экспортируются, доступны последующим этапам; полный набор состояний
  у каждого (полу-наличие хуже отсутствия, §6.2/#32).
- `ToastProvider` подключён в корень; есть ≥1 реальный вызов Toast (не dead code).
- Догфуд: map/dashboard skeleton + map/shop-orders empty переведены на примитивы, старые экраны не сломаны.
- `prefers-reduced-motion` уважается всеми тремя.
- Гейты зелёные: `~/.bun/bin/bun test` (Operator) + `bun run build`.

## 8. Явно вне скоупа

- Массовая миграция всех loading/empty/feedback по воркспейсам (по ходу этапов).
- Перевод `FeedbackNotice → Toast` (это разные задачи: инлайн-контекст vs глобальная эфемерность).
- Wiring Toast в offline-outbox/optimistic-поток (контракт `action` заложен, использование — позже).
- `useDeferredFlag` (уже есть), warning-тон (намеренно нет).
</content>
