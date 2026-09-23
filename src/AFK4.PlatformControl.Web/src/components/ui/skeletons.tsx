import { Fragment, useEffect, useState, type ReactNode } from 'react';

// Заглушка загрузки повторяет форму того, что придёт: таблицу, список строк, плитки с цифрами,
// график. Раньше на месте любого содержимого стояли одинаковые полосы по 56px в приподнятой
// панели, и при подмене раскладка прыгала: таблица выше и ниже полос, у графика своя высота,
// у карточки раздела — шапка с кнопкой. Форм здесь немного — по видам содержимого, а не по
// экрану, — и собраны они из тех же классов, что и настоящее содержимое (.pc-table,
// .management-panel, .pc-analytics-tile…): высоту строки, плитки или шапки задаёт сам контейнер,
// поэтому заглушка совпадает с ним без подобранных чисел.
//
// Количество строк — правдоподобное, а не настоящее: его ещё никто не знает. Совпадают каркас и
// первые строки; то, чего окажется больше или меньше, дорастёт вниз, ничего не сдвигая.

// Ожидание показывается не сразу: почти все ответы панели приходят быстрее, чем человек успевает
// прочитать хоть что-то, и заглушка в этом случае только мигает. Задержка ничего не замедляет:
// медленный ответ покажет ожидание так же, просто на пятую долю секунды позже. То же правило и
// то же число, что в Панели AFK4.net.
export const SKELETON_DELAY_MS = 180;

export function Loading({ children }: { children: ReactNode }) {
  const [shown, setShown] = useState(false);
  useEffect(() => {
    const timer = setTimeout(() => setShown(true), SKELETON_DELAY_MS);
    return () => clearTimeout(timer);
  }, []);
  return shown ? <>{children}</> : null;
}

const LINE_WIDTHS = ['72%', '48%', '60%', '36%', '54%'];
const lineWidth = (index: number) => LINE_WIDTHS[index % LINE_WIDTHS.length];
const times = (count: number) => Array.from({ length: count }, (_, index) => index);

// Одна строка текста в шрифте контейнера. Элемент — <i>, а не <span>: правила экранов вида
// `.pc-kv > span:first-child` цепляют любой span и подменили бы заглушке шрифт, а с ним и высоту.
export function SkeletonLine({ width = '60%' }: { width?: string }) {
  return <i className="skeleton-line" style={{ width }} aria-hidden="true">&nbsp;</i>;
}

// Поле ввода или кнопка — высотой контрола; `size="sm"` — кнопка size="sm".
export function SkeletonControl({ width, size }: { width?: string; size?: 'sm' }) {
  return (
    <span
      className={`skeleton-block skeleton-control${size === 'sm' ? ' skeleton-control--sm' : ''}`}
      style={width === undefined ? undefined : { width }}
      aria-hidden="true"
    />
  );
}

// Карточка раздела (Card): шапка с заголовком, при нужде — подпись под ним и кнопка справа (её
// показывает право, а право известно до ответа), тело — любая форма ниже.
export function SkeletonCard({ action = false, description = false, children }: {
  action?: boolean;
  description?: boolean;
  children?: ReactNode;
}) {
  return (
    <section className="management-panel" aria-hidden="true">
      <div className="mgmt-section-title">
        {description
          ? <div><SkeletonLine width="10em" /><p className="mgmt-drawer-hint"><SkeletonLine width="16em" /></p></div>
          : <SkeletonLine width="10em" />}
        {action ? <SkeletonControl width="9rem" /> : null}
      </div>
      {children !== undefined ? <div className="pc-panel-body">{children}</div> : null}
    </section>
  );
}

// Таблица .pc-table: шапка и строки в той же разметке, что у настоящей, — высоту им задают th/td.
export function SkeletonTable({ columns, rows = 4 }: { columns: number; rows?: number }) {
  return (
    <div className="table-panel" data-skeleton="table" aria-hidden="true">
      <table className="pc-table">
        <thead>
          <tr>{times(columns).map(column => <th key={column}><SkeletonLine width="50%" /></th>)}</tr>
        </thead>
        <tbody>
          {times(rows).map(row => (
            <tr key={row}>{times(columns).map(column => <td key={column}><SkeletonLine width={lineWidth(row + column)} /></td>)}</tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// Список строк без шапки: очередь счетов (.pc-queue-row), строки карточки клиента (.pc-list-row),
// пары «метка — значение» (.pc-kv). Класс строки — тот же, что у настоящей; `trailing` — кнопка
// справа, если у настоящей строки она будет. `as="ul"` — строки в своём списке (className — его
// класс); без него строки встают прямо в родителя, как настоящие, и берут его зазоры.
export function SkeletonRows({ rows, className, rowClassName, trailing = false, as }: {
  rows: number;
  className?: string;
  rowClassName: string;
  trailing?: boolean;
  as?: 'ul';
}) {
  const Row = as === 'ul' ? 'li' : 'div';
  const items = times(rows).map(row => (
    <Row key={row} className={rowClassName} data-skeleton={as === 'ul' ? undefined : 'list'} aria-hidden="true">
      <SkeletonLine width={lineWidth(row)} />
      {trailing ? <SkeletonControl width="8rem" size="sm" /> : null}
    </Row>
  ));
  return as === 'ul'
    ? <ul className={className} data-skeleton="list" aria-hidden="true">{items}</ul>
    : <>{items}</>;
}

// Плитки с цифрами. Разметка у экранов двух видов, и заглушка повторяет обе: пары «термин —
// значение» (<dl><div><dt/><dd/></div></dl>, как .pc-facts) или подписанные span'ы внутри <div>
// (labelClassName/valueClassName), как плитки аналитики. Оформление — классы самого экрана.
export function SkeletonTiles({ count, className, tileClassName, labelClassName, valueClassName }: {
  count: number;
  className: string;
  tileClassName: string;
  labelClassName?: string;
  valueClassName?: string;
}) {
  if (labelClassName !== undefined) {
    return (
      <div className={className} data-skeleton="tiles" aria-hidden="true">
        {times(count).map(index => (
          <div key={index} className={tileClassName}>
            <span className={labelClassName}><SkeletonLine width="60%" /></span>
            <span className={valueClassName}><SkeletonLine width="45%" /></span>
          </div>
        ))}
      </div>
    );
  }
  return (
    <dl className={className} data-skeleton="tiles" aria-hidden="true">
      {times(count).map(index => (
        <div key={index} className={tileClassName}>
          <dt><SkeletonLine width="60%" /></dt>
          <dd><SkeletonLine width="45%" /></dd>
        </div>
      ))}
    </dl>
  );
}

// График: та же обёртка .pc-analytics-chart и та же высота, что передаётся ResponsiveContainer.
export function SkeletonChart({ height }: { height: number }) {
  return (
    <div className="pc-analytics-chart" data-skeleton="chart" aria-hidden="true">
      <span className="skeleton-block" style={{ display: 'block', height }} />
    </div>
  );
}

// Вкладки (.mgmt-tabs) — по числу настоящих.
export function SkeletonTabs({ count }: { count: number }) {
  return (
    <div className="mgmt-tabs" aria-hidden="true">
      {times(count).map(index => <span key={index} className="mgmt-tab"><SkeletonLine width="6em" /></span>)}
    </div>
  );
}

// Повторить одну и ту же форму `count` раз — стопка карточек, у каждой свой каркас.
export function SkeletonRepeat({ count, children }: { count: number; children: ReactNode }) {
  return <>{times(count).map(index => <Fragment key={index}>{children}</Fragment>)}</>;
}
