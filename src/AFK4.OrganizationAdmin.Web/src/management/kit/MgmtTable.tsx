import type { ReactNode } from 'react';
import { Search } from 'lucide-react';
import { EmptyState, Skeleton, type EmptyStateNext } from '../../operatorPrimitives';
import { RowActionsMenu } from './RowActionsMenu';
import type { MgmtColumn, RowAction } from './types';

interface MgmtTableProps<T> {
  columns: MgmtColumn<T>[];
  rows: T[];
  rowKey: (row: T) => string;
  gridTemplate: string; // grid-template-columns без учёта trailing-колонки ⋯ (её добавляем сами)
  selectedKey?: string | null;
  onSelectRow?: (row: T) => void;
  rowActions?: (row: T) => RowAction[];
  toolbar?: {
    title?: string;
    search?: { value: string; onChange: (value: string) => void; placeholder: string };
    // Второе действие раздела рядом с главным — тихой кнопкой: «Добавить из сети» рядом с
    // «Пригласить».
    secondary?: { label: string; icon?: ReactNode; onClick: () => void; disabled?: boolean };
    primary?: { label: string; icon?: ReactNode; onClick: () => void; disabled?: boolean };
  };
  isLoading?: boolean;
  empty: { icon?: ReactNode; title: string; description?: string; next: EmptyStateNext };
  /** Список внутри другой карточки (места в карточке зала): без своей рамки и подложки — иначе
   *  карточка в карточке. */
  bare?: boolean;
}

// Универсальный список-панель для CRUD-разделов «Управления»: тулбар (заголовок + опц. поиск +
// первичная кнопка), заголовки колонок, строки (render-props по колонкам), skeleton при загрузке,
// честный empty-state. Строка кликабельна (role=button) → onSelectRow; ⋯-меню действий строки —
// отдельная trailing-ячейка (не вложенная кнопка-в-кнопку, поэтому строка — div c role=button, а
// не <button>). Переиспользует принятые классы .table-panel/.ctable-* (визуальный язык раздела
// «Клиенты»), колонки задаёт инлайновым grid-template.
export function MgmtTable<T>({
  columns,
  rows,
  rowKey,
  gridTemplate,
  selectedKey,
  onSelectRow,
  rowActions,
  toolbar,
  isLoading,
  empty,
  bare = false
}: MgmtTableProps<T>) {
  const effectiveGrid = rowActions ? `${gridTemplate} 44px` : gridTemplate;
  // Пустой список — это пустое состояние, а не таблица без строк: заголовки колонок над ним
  // подписывали то, чего нет. И кнопка создания у пустого списка одна — в пустом состоянии, а не
  // ещё и в тулбаре рядом: две одинаковые главные кнопки в одной карточке спорили друг с другом.
  const isEmpty = !isLoading && rows.length === 0;
  const primary = isEmpty && empty.next.kind === 'action' ? undefined : toolbar?.primary;
  const showToolbar = toolbar !== undefined && Boolean(toolbar.title || toolbar.search || toolbar.secondary || primary);

  return (
    <section className={`table-panel mgmt-table${bare ? ' mgmt-table--bare' : ''}`}>
      {toolbar && showToolbar && (
        <div className="table-toolbar">
          {toolbar.title && <span className="mgmt-tt-title">{toolbar.title}</span>}
          {toolbar.search && (
            <label className="tt-search">
              <Search size={14} aria-hidden="true" />
              <input
                placeholder={toolbar.search.placeholder}
                value={toolbar.search.value}
                onChange={(event) => toolbar.search!.onChange(event.currentTarget.value)}
              />
            </label>
          )}
          <div className="tt-spacer" />
          {toolbar.secondary && (
            <button
              type="button"
              className="ui-btn"
              disabled={toolbar.secondary.disabled}
              onClick={toolbar.secondary.onClick}
            >
              {toolbar.secondary.icon}
              {toolbar.secondary.label}
            </button>
          )}
          {primary && (
            <button
              type="button"
              className="ui-btn ui-btn--primary"
              disabled={primary.disabled}
              onClick={primary.onClick}
            >
              {primary.icon}
              {primary.label}
            </button>
          )}
        </div>
      )}

      {/* Подписи колонок нужны, когда колонок несколько; у списка из одних названий («Залы»,
          «Категории») подпись «Зал» под заголовком «Залы» — повтор, и её не задают. */}
      {!isEmpty && columns.some((column) => column.header) && (
        <div className="ctable-head mgmt-grid" style={{ gridTemplateColumns: effectiveGrid }} aria-hidden="true">
          {columns.map((column) => (
            <span key={column.key} className={column.align === 'end' ? 'r' : undefined}>{column.header}</span>
          ))}
          {rowActions && <span />}
        </div>
      )}

      <div className="ctable-body">
        {isLoading ? (
          <div className="ctable-skeleton" aria-hidden="true">
            {Array.from({ length: 6 }).map((_, index) => (
              <Skeleton key={index} className="ctable-row-skel" />
            ))}
          </div>
        ) : rows.length > 0 ? (
          rows.map((row) => {
            const key = rowKey(row);
            const clickable = Boolean(onSelectRow);
            return (
              <div
                key={key}
                className={`ctable-row mgmt-row mgmt-grid${key === selectedKey ? ' selected' : ''}`}
                style={{ gridTemplateColumns: effectiveGrid }}
                role={clickable ? 'button' : undefined}
                tabIndex={clickable ? 0 : undefined}
                onClick={clickable ? () => onSelectRow!(row) : undefined}
                onKeyDown={
                  clickable
                    ? (event) => {
                        if (event.key === 'Enter' || event.key === ' ') {
                          event.preventDefault();
                          onSelectRow!(row);
                        }
                      }
                    : undefined
                }
              >
                {columns.map((column) => (
                  <span key={column.key} className={`mgmt-cell${column.align === 'end' ? ' mgmt-cell--end' : ''}`}>
                    {cellContent(column.render(row))}
                  </span>
                ))}
                {rowActions && (
                  <span className="mgmt-cell mgmt-cell--menu">
                    <RowActionsMenu actions={rowActions(row)} />
                  </span>
                )}
              </div>
            );
          })
        ) : (
          <EmptyState
            icon={empty.icon}
            title={empty.title}
            description={empty.description}
            next={empty.next}
          />
        )}
      </div>
    </section>
  );
}

// Текст, не влезший в колонку, обрезается многоточием, а целиком виден в подсказке.
function cellContent(content: ReactNode): ReactNode {
  return typeof content === 'string' || typeof content === 'number'
    ? <span className="mgmt-cell-text" title={String(content)}>{content}</span>
    : content;
}
