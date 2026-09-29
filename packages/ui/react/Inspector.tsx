import { Fragment, useId } from 'react';
import type { ReactNode } from 'react';
import { CloseButton } from './Button';
import { RowActions, type RowAction } from './RowActions';
import { cx } from './cx';

// Инспектор — правая колонка карточки записи (игрок, ПК, клуб, товар), ширина --inspector-w.
// Сверху вниз:
//   шапка — название, статус, подзаголовок; справа «⋯» и закрыть;
//   одна главная цифра (`figure`) — баланс игрока, остаток сессии;
//   <Inspector.Actions> — одна главная кнопка во всю ширину и до трёх второстепенных ровным рядом;
//   <Inspector.Facts> — факты «подпись — значение»;
//   <Inspector.Section> — всё остальное, под капс-подписью.
// Опасное и редкое — в «⋯», а не пятой кнопкой в колонке: в инспекторе игрока их было пять видов.
// Чего нельзя по правам — не рисуется; чего нельзя по состоянию — одна строка причины рядом
// (useBlockedReason).
export function Inspector({ title, status, subtitle, menu, close, figure, className, children }: {
  title: ReactNode;
  status?: ReactNode;
  subtitle?: ReactNode;
  menu?: { label: string; actions: RowAction[] };
  /** `disabled` — пока идёт команда: закрыть карточку посреди записи значит потерять её исход. */
  close?: { label: string; onClose: () => void; disabled?: boolean };
  figure?: { label: string; value: ReactNode; hint?: ReactNode };
  className?: string;
  children?: ReactNode;
}) {
  const titleId = useId();
  return (
    <aside className={cx('ui-inspector', className)} aria-labelledby={titleId}>
      <div className="ui-inspector-head">
        <div className="ui-inspector-id">
          <div className="ui-inspector-title-row">
            <h2 id={titleId} className="ui-inspector-title">{title}</h2>
            {status}
          </div>
          {subtitle !== undefined ? <p className="ui-inspector-subtitle">{subtitle}</p> : null}
        </div>
        {menu !== undefined || close !== undefined ? (
          <div className="ui-inspector-head-actions">
            {menu !== undefined ? <RowActions label={menu.label} actions={menu.actions} /> : null}
            {close !== undefined ? <CloseButton label={close.label} disabled={close.disabled} onClick={close.onClose} /> : null}
          </div>
        ) : null}
      </div>
      <div className="ui-inspector-body">
        {figure !== undefined ? (
          <div className="ui-inspector-figure">
            <span className="ui-section-label">{figure.label}</span>
            <span className="ui-inspector-figure-value">{figure.value}</span>
            {figure.hint !== undefined ? <span className="ui-inspector-figure-hint">{figure.hint}</span> : null}
          </div>
        ) : null}
        {children}
      </div>
    </aside>
  );
}

// Второстепенных — не больше трёх, типом: четвёртая кнопка в ряду сжимает все до нечитаемых, а
// место ей — в «⋯».
type Secondary = readonly [] | readonly [ReactNode] | readonly [ReactNode, ReactNode] | readonly [ReactNode, ReactNode, ReactNode];

function Actions({ primary, secondary = [], hint }: {
  /** Главная кнопка — <Button variant="primary" block>. */
  primary?: ReactNode;
  secondary?: Secondary;
  /** Почему главная неактивна — строка useBlockedReason. */
  hint?: ReactNode;
}) {
  return (
    <div className="ui-inspector-actions">
      {primary}
      {hint}
      {secondary.length > 0 ? <div className="ui-inspector-secondary">{secondary.map((node, index) => <Fragment key={index}>{node}</Fragment>)}</div> : null}
    </div>
  );
}

export interface Fact {
  label: ReactNode;
  value: ReactNode;
}

function Facts({ items }: { items: Fact[] }) {
  return (
    <dl className="ui-facts">
      {items.map((item, index) => (
        <div key={index} className="ui-facts-row">
          <dt>{item.label}</dt>
          <dd>{item.value}</dd>
        </div>
      ))}
    </dl>
  );
}

function Section({ label, children }: { label: string; children: ReactNode }) {
  return (
    <section className="ui-inspector-section">
      <h3 className="ui-section-label">{label}</h3>
      {children}
    </section>
  );
}

Inspector.Actions = Actions;
Inspector.Facts = Facts;
Inspector.Section = Section;
