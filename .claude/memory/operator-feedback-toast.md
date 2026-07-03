---
name: operator-feedback-toast
description: Единый канал обратной связи оператора — тост; самодельные пилюли статуса действия удалены
metadata: 
  node_type: memory
  type: project
  originSessionId: 4b1405c9-73b2-4425-a9e9-3ed96a366b12
---

Консолидация 2026-07-01 (ветка `feat/operator-pos-receipts-panel`, коммиты `57ec3edd`..`92bea360`, брейншторм→спек→план→subagent-driven, финал-ревью opus). Спек `docs/superpowers/specs/2026-07-01-operator-feedback-toast-consolidation-design.md`.

**Инвариант: результаты действий идут ТОЛЬКО в тост `useToast` (`operatorToast.tsx`), самодельных пилюль больше НЕТ.** Удалены `FeedbackNotice`, `ActionFeedback` (был приватный в MapSidePanel), `pc-control-result`, `recv-status` + весь их CSS (`06-map-grid.css`, `22-stock.css`, `10-booking.css`). Если снова тянет отрендерить строку-пилюлю статуса действия — НЕ надо, это тост.

**Мост:** новый хук `useFeedbackToasts(feedback, {notifySuccess?})` (`src/useFeedbackToasts.ts`) следит за `Feedback`-стейтом: `failed`→`toast.error` (sticky), `confirmed`→`toast.success` (авто-4с, если не `notifySuccess:false`), текст через существующий `feedbackText` (НОВЫХ i18n-ключей не заводили). **`Feedback`-стейт-машина СОХРАНЕНА** — она же крутит `isBusy`/спиннеры на кнопках; хук только переливает результат в тост. Складские воркспейсы — исключение: у них `PostState` (не `Feedback`), тостят `toast.success/error` напрямую.

**Правило:** ошибки всегда тостят; успех тостит только у ЗНАЧИМОГО действия (смена/оплата/возврат/бронь/сессия/склад-проведение/экспорт), рутина (товар в корзину, выбор) — молча; во время запроса спиннер на кнопке, «pending»-тоста нет. **Персистентный КОНТЕКСТ остаётся inline** (`workspace-error`, auth, валидация, booking-conflict, `panel-readiness` «нет прав») — тост бы всплыл и исчез, а контекст должен висеть у проблемы. Load-ошибки перевели с пилюли на стандартный `<p className="workspace-error" role="alert">`.

**Durable-грабли (дважды укусило):**
- **parent+child делят один `feedback`-проп → двойной тост.** BookingDrawer получал `feedback` от `BackendBookingWorkspace` и оба звали хук. `notifySuccess:false` глушит только УСПЕХ — ошибки тостят ВСЕГДА → два sticky error-тоста. Фикс: у ребёнка хук убрать совсем, канал — только родитель (он всегда смонтирован, владеет стейтом). Замок: `querySelectorAll('.toast-error').length===1`. Проверяй любой parent/child, делящий feedback-проп.
- **App.test не был в скоупе секционных задач** (рендерит всё приложение) → 6 падений на старом тексте пилюль. При такой миграции интеграционный App.test ОБЯЗАН быть в списке правок. Урок: `notifySuccess:false` ставить только если рутинное действие реально ставит `confirmed` — в POS корзина `setFeedback` не трогает, а оплата ставит → заглушка зря убила тост оплаты (значимо!).
- Тест, рендерящий мигрированный компонент, падает без `<ToastProvider>` (хук зовёт `useToast`, тот кидает вне провайдера). Оборачивать ВНУТРИ `<I18nProvider>` (ToastProvider сам зовёт `useI18n`).

**Follow-up (не сделано, бэклог):** тост-вьюпорт показывает 3 СТАРЫХ (`slice(0,MAX_VISIBLE)`), новые в очереди — под шквалом действий свежий результат прячется; кандидат на newest-first. Копи успеха «{label}: подтверждено» суховато (лучше «Смена открыта»/«ПК заблокирован») — полировка. POS catalog-load/player-search ошибки идут в sticky-тост (pre-existing conflation, не регресс) — можно вынести inline.

См. [[operator-pos-receipts-panel]] (та же ветка), [[afk4-operator-map-epic]] (цвета/expired той же сессии).
