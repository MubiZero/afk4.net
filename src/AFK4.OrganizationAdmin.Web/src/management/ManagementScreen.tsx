import { createContext, useContext, useState } from 'react';
import type { JSX, ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { useI18n } from '@afk4/i18n';
import { Button, SectionHeader, type HeaderCounts } from '@afk4/ui/react';
import { projectOperatorError, type OperatorErrorProjection } from '../apiErrors';
import { LoadFailureState } from '../operatorPrimitives';
import { DeferredSkeleton } from '../LoadingSkeleton';
import { ViewOnlyNotice } from './ViewOnlyNotice';

export type SaveState = 'clean' | 'dirty' | 'saving' | 'saved';

interface ManagementScreenBaseProps {
  title: string;
  // Шапка — общая шапка раздела кита: название, до трёх счётчиков и одна главная кнопка раздела
  // («+ Товар», её ставит <ScreenAction> из тела экрана), под ними вкладки. Мелкой строки над
  // названием нет (решение владельца 29.09): она пересказывала название другими словами.
  counts?: HeaderCounts;
  tabs?: ReactNode;
  children: ReactNode; // destination body (panels/forms)
  // Content column width: 'form' (narrow, single-column config forms) keeps fields a
  // comfortable measure instead of stretching edge-to-edge across the canvas; 'wide' is
  // for list/table-heavy screens (catalog, payment cards); 'full' drops the max-width entirely
  // for screens whose content IS a two-pane grid (halls/devices master-detail) that should use
  // the whole workspace width. Defaults to 'form'.
  contentWidth?: 'form' | 'wide' | 'full';
  // What failed and whether «Повторить» can help (projectOperatorError of the load) — the detail
  // is shown as-is, never replaced by generic copy; the retry button appears only when a retry can
  // change the answer (not under a permission refusal, where the access hint takes its place).
  failure?: OperatorErrorProjection;
  onRetry?: () => void;
  // Смотреть экран можно, менять — нет: одна строка «только просмотр» и кто может менять, вместо
  // погашенных без объяснения полей и кнопок.
  viewOnly?: string | null;
  // То, что от ответа не зависит и нужно в любом состоянии: панель периода отчёта. Стоит над
  // телом и при загрузке, и при отказе — иначе поле, в котором только что выбрали дату, пропадало
  // из-под курсора, а после отказа за новый период к рабочему было не вернуться.
  controls?: ReactNode;
  save?: {
    // omit for read-only destinations
    state: SaveState;
    onSave: () => void;
    onDiscard?: () => void; // revert unsaved edits to the last-loaded/saved state; shows «Отменить» when dirty
    disabled?: boolean; // e.g. no backend / no permission
  };
}

// Loading/error swap the body for a skeleton/error affordance instead of children — save bar
// is suppressed in both. Defaults to 'ready' (renders children as before).
//
// Экран, который грузится, обязан сказать, какой формы будет его содержимое: `skeleton` собирается
// из LoadingSkeleton по тем же классам, что и настоящее тело (таблица, форма, плитки, карточки).
// Пока заглушка была одна на всех, четыре строки в карточке стояли на месте таблиц, сеток и
// плиток, и раскладка прыгала при подмене; теперь `tsc` не пропустит `state` без решения о форме.
type ManagementScreenLoadProps =
  | { state?: undefined; skeleton?: undefined }
  | { state: 'loading' | 'error' | 'ready'; skeleton: ReactNode };

export type ManagementScreenProps = ManagementScreenBaseProps & ManagementScreenLoadProps;

export function ManagementScreen({
  title,
  counts,
  tabs,
  children,
  contentWidth = 'form',
  state = 'ready',
  skeleton,
  failure,
  onRetry,
  viewOnly,
  controls,
  save
}: ManagementScreenProps): JSX.Element {
  const { t } = useI18n();
  const [actionSlot, setActionSlot] = useState<HTMLElement | null>(null);

  return (
    <section className="workspace-screen management-screen">
      {/* Шапка — той же ширины, что и колонка содержимого под ней: иначе главная кнопка раздела
          стояла у края окна, правее всего, к чему она относится. */}
      <div className="management-screen-head">
        <div className={`management-content--${contentWidth}`}>
          <SectionHeader
            title={title}
            counts={counts}
            tabs={tabs}
            action={<span ref={setActionSlot} className="management-screen-action" />}
          />
        </div>
      </div>
      <ScreenActionSlot.Provider value={actionSlot}>
        <div className="management-screen-body">
          <div className={`management-content management-content--${contentWidth}`}>
            {/* Право известно до ответа, и строка «только просмотр» стоит над заглушкой так же,
                как встанет над содержимым, — иначе она вдвигалась бы сверху в момент подмены. */}
            {state !== 'error' && <ViewOnlyNotice reason={viewOnly} />}
            {controls}
            {state === 'loading' ? (
              <DeferredSkeleton>{skeleton}</DeferredSkeleton>
            ) : state === 'error' ? (
              <div className="management-error-state">
                <LoadFailureState title={t('op.management.state.errorTitle')} failure={failure ?? projectOperatorError(undefined, t)} onRetry={onRetry} />
              </div>
            ) : (
              <>
                {children}

                {save && (
                  <div className="management-save-bar">
                    <span>{save.state === 'saved' ? t('op.management.save.saved') : save.state === 'clean' ? t('op.management.save.clean') : ''}</span>
                    {save.onDiscard && (
                      <Button disabled={save.state !== 'dirty' || save.disabled} onClick={save.onDiscard}>
                        {t('op.management.save.discard')}
                      </Button>
                    )}
                    <Button
                      variant="primary"
                      disabled={save.state === 'clean' || save.state === 'saving' || save.disabled}
                      onClick={save.onSave}
                    >
                      {t('common.save')}
                    </Button>
                  </div>
                )}
              </>
            )}
          </div>
        </div>
      </ScreenActionSlot.Provider>
    </section>
  );
}

const ScreenActionSlot = createContext<HTMLElement | null>(null);

// Главная кнопка раздела встаёт в шапку экрана, а живёт в списке, который её открывает: там её
// состояние (окно создания, право, выбранная вкладка). Поднимать всё это в каждый экран ради одной
// кнопки — лишняя проводка, поэтому список отдаёт кнопку сюда, а она уходит в шапку порталом.
// Раньше «+ Новость» стояла в карточке списка под заголовком «Новости», повторявшим шапку, а у
// пустого списка рядом с ней появлялась вторая такая же — в пустом состоянии.
export function ScreenAction({ children }: { children: ReactNode }) {
  const slot = useContext(ScreenActionSlot);
  return slot === null ? null : createPortal(children, slot);
}
