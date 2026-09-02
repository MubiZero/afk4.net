import type { ReactNode } from 'react';
import { Search } from 'lucide-react';
import { EmptyState, Skeleton } from '../../operatorPrimitives';
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
    primary?: { label: string; icon?: ReactNode; onClick: () => void; disabled?: boolean };
  };
  isLoading?: boolean;
  empty: { icon?: ReactNode; title: string; description?: string; action?: { label: string; onClick: () => void } };
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
  empty
}: MgmtTableProps<T>) {
  const effectiveGrid = rowActions ? `${gridTemplate} 44px` : gridTemplate;

  return (
    <section className="table-panel mgmt-table">
      {toolbar && (
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
          {toolbar.primary && (
            <button
              type="button"
              className="ui-btn ui-btn--primary"
              disabled={toolbar.primary.disabled}
              onClick={toolbar.primary.onClick}
            >
              {toolbar.primary.icon}
              {toolbar.primary.label}
            </button>
          )}
        </div>
      )}

      <div className="ctable-head mgmt-grid" style={{ gridTemplateColumns: effectiveGrid }} aria-hidden="true">
        {columns.map((column) => (
          <span key={column.key} className={column.align === 'end' ? 'r' : undefined}>{column.header}</span>
        ))}
        {rowActions && <span />}
      </div>

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
                    {column.render(row)}
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
            action={empty.action}
          />
        )}
      </div>
    </section>
  );
}
