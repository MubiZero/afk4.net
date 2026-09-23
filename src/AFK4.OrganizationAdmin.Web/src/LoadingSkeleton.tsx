import { Fragment, type JSX, type ReactNode } from 'react';
import { useDeferredFlag } from './useDeferredFlag';

// Заглушка загрузки повторяет форму того, что придёт: таблицу, сетку карточек, форму, плитки.
// Одна форма на всё (четыре строки в карточке) ставила на место сетки карточек полоски, и при
// подмене раскладка прыгала. Здесь форм немного — по видам содержимого, а не по экрану, — и
// собраны они из тех же классов, что и настоящее содержимое: высоту строки таблицы, плитки или
// поля задаёт сам контейнер, поэтому заглушка совпадает с ним без подобранных чисел.
//
// Количество строк и карточек — правдоподобное, а не настоящее: его ещё никто не знает. Совпадают
// каркас и первые строки; то, чего окажется больше или меньше, дорастёт вниз, ничего не сдвигая.

const LINE_WIDTHS = ['72%', '48%', '60%', '36%', '54%'];
const lineWidth = (index: number) => LINE_WIDTHS[index % LINE_WIDTHS.length];
const times = (count: number) => Array.from({ length: count }, (_, index) => index);

// Столько колонок, сколько в grid-template-columns, — с учётом minmax(…, …) и repeat-скобок.
export function gridColumnCount(gridTemplate: string): number {
  let depth = 0;
  let count = 0;
  let inToken = false;
  for (const char of gridTemplate) {
    if (char === '(') depth += 1;
    if (char === ')') depth -= 1;
    if (/\s/.test(char) && depth === 0) {
      inToken = false;
    } else if (!inToken) {
      inToken = true;
      count += 1;
    }
  }
  return count;
}

// Ожидание не рисуется сразу: почти все ответы приходят быстрее, чем человек успевает что-то
// прочитать, и заглушка тогда только мигает. Правило то же, что в Platform Control: 180 мс
// тишины, потом форма. Медленный ответ покажет ожидание так же, просто на пятую долю секунды позже.
export function DeferredSkeleton({ children }: { children: ReactNode }): JSX.Element | null {
  const shown = useDeferredFlag(true);
  return shown ? <>{children}</> : null;
}

// Одна строка текста в шрифте контейнера. Элемент — <i>, а не <span>: правила экранов вида
// `.reports-trend span { font-size: 12px }` цепляют любой span внутри и подменили бы заглушке
// шрифт, а с ним и высоту строки.
export function SkeletonLine({ width = '60%' }: { width?: string }): JSX.Element {
  return <i className="skeleton-line" style={{ width }}>&nbsp;</i>;
}

// Поле ввода или кнопка — высотой контрола: `sm` — маленькая кнопка строки (.ui-btn--sm), `lg` —
// крупная (кнопка загрузки картинки).
export function SkeletonControl({ width, size }: { width?: string; size?: 'sm' | 'lg' }): JSX.Element {
  return (
    <span
      className={`skeleton-block skeleton-control${size === undefined ? '' : ` skeleton-control--${size}`}`}
      style={width === undefined ? undefined : { width }}
    />
  );
}

// Вкладки раздела (.mgmt-tabs) — чтобы переключатель не появлялся над содержимым вдогонку.
export function SkeletonTabs({ count }: { count: number }): JSX.Element {
  return (
    <div className="mgmt-tabs" aria-hidden="true">
      {times(count).map((index) => (
        <span key={index} className="mgmt-tab"><SkeletonLine width="6em" /></span>
      ))}
    </div>
  );
}

// Таблица MgmtTable и её родня на .table-panel/.ctable-*: панель инструментов, шапка, строки.
// Колонки — тот же grid-template, что у настоящей таблицы; колонка меню строки и кнопка в панели
// инструментов — только если они будут и у настоящей (их показывает право, а право известно до
// ответа).
export function SkeletonTable({ gridTemplate, rows = 5, rowActions = false, toolbar }: {
  gridTemplate: string;
  rows?: number;
  rowActions?: boolean;
  toolbar?: { title?: boolean; action?: boolean };
}): JSX.Element {
  const columns = gridColumnCount(gridTemplate);
  const grid = rowActions ? `${gridTemplate} 44px` : gridTemplate;
  return (
    <section className="table-panel mgmt-table" data-skeleton="table" aria-hidden="true">
      {toolbar && (
        <div className="table-toolbar">
          {toolbar.title !== false && <span className="mgmt-tt-title"><SkeletonLine width="9em" /></span>}
          <div className="tt-spacer" />
          {toolbar.action && <SkeletonControl width="10rem" />}
        </div>
      )}
      <div className="ctable-head mgmt-grid" style={{ gridTemplateColumns: grid }}>
        {times(columns).map((column) => <span key={column}><SkeletonLine width="45%" /></span>)}
        {rowActions && <span />}
      </div>
      <div className="ctable-body">
        {times(rows).map((row) => (
          <div key={row} className="ctable-row mgmt-row mgmt-grid" style={{ gridTemplateColumns: grid }}>
            {times(columns).map((column) => (
              <span key={column} className="mgmt-cell"><SkeletonLine width={lineWidth(row + column)} /></span>
            ))}
            {rowActions && <span />}
          </div>
        ))}
      </div>
    </section>
  );
}

// Список строк без шапки (ul > li): категории, расписания. Класс строки — тот же, что у настоящей;
// `trailing` — маленькие кнопки справа, если они будут у настоящей строки.
export function SkeletonRows({ rows, rowClassName, trailing = false }: {
  rows: number;
  rowClassName: string;
  trailing?: boolean;
}): JSX.Element {
  return (
    <ul data-skeleton="list" aria-hidden="true">
      {times(rows).map((row) => (
        <li key={row} className={rowClassName}>
          <SkeletonLine width={lineWidth(row)} />
          {trailing && <SkeletonControl width="9rem" size="sm" />}
        </li>
      ))}
    </ul>
  );
}

// Форма .mgmt-form: подпись над полем, поле высотой контрола; `submit` — кнопка под полями.
export function SkeletonForm({ fields, submit = false }: { fields: number; submit?: boolean }): JSX.Element {
  return (
    <div className="mgmt-form" data-skeleton="form" aria-hidden="true">
      {times(fields).map((index) => (
        <label key={index}>
          <SkeletonLine width={index % 2 === 0 ? '8em' : '11em'} />
          <SkeletonControl />
        </label>
      ))}
      {submit && <SkeletonControl />}
    </div>
  );
}

// Плитки с цифрами. Разметка у экранов двух видов, и заглушка повторяет обе: пары «термин —
// значение» (<dl><div><dt/><dd/></div></dl>, как у отчётов) или подписанные span'ы внутри <div>
// (labelClassName/valueClassName), как у итогов сети. Оформление плитки — классы самого экрана.
export function SkeletonTiles({ count, className, tileClassName, labelClassName, valueClassName }: {
  count: number;
  className: string;
  tileClassName?: string;
  labelClassName?: string;
  valueClassName?: string;
}): JSX.Element {
  if (labelClassName !== undefined) {
    return (
      <div className={className} data-skeleton="tiles" aria-hidden="true">
        {times(count).map((index) => (
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
      {times(count).map((index) => (
        <div key={index} className={tileClassName}>
          <dt><SkeletonLine width="60%" /></dt>
          <dd><SkeletonLine width="45%" /></dd>
        </div>
      ))}
    </dl>
  );
}

// Сетка карточек: контейнер сетки экрана (он же задаёт число колонок) и одна карточка, повторённая
// `count` раз. Внутренность карточки у каждого экрана своя и собирается из форм выше.
export function SkeletonCards({ count, className, children }: {
  count: number;
  className: string;
  children: ReactNode;
}): JSX.Element {
  return (
    <div className={className} data-skeleton="cards" aria-hidden="true">
      {times(count).map((index) => <Fragment key={index}>{children}</Fragment>)}
    </div>
  );
}
