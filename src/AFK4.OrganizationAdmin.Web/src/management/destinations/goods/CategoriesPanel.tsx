import { useState } from 'react';
import { ChevronDown, ChevronUp, Eye, EyeOff, Pencil, Tags } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { Button, IconButton } from '@afk4/ui/react';
import { projectOperatorError } from '../../../apiErrors';
import { createAuthenticatedOperatorClients, requireBackend } from '../../../operatorHelpers';
import type { Feedback, OperatorBackendContext } from '../../../operatorTypes';
import { MgmtTable } from '../../kit/MgmtTable';
import type { CategoryOption } from './categoryModel';
import { moveCategory } from './categoryOrder';

// Колонки списка — одни на список и его заглушку: стрелки порядка (у того, кто может менять) и
// название.
export const categoriesGrid = (canManage: boolean) => (canManage ? '72px minmax(0, 1fr)' : 'minmax(0, 1fr)');

/**
 * Справочник категорий товара: посмотреть, переименовать, скрыть и переставить.
 *
 * Переименования не было ни на одной стороне — ни маршрута, ни кнопки. Опечатка в названии,
 * сделанная при заведении первого товара, оставалась в меню бара навсегда: единственным выходом
 * было завести категорию заново и перевесить на неё все товары по одному.
 *
 * Удаления нет и здесь — вместо него скрытие. Скрытая категория вместе со своими товарами
 * пропадает со стойки и из магазина оболочки, но остаётся в каталоге: история чеков цела, а
 * возврат — один переключатель, а не заведение заново. Настоящее удаление пришлось бы решать
 * за владельца, что делать с товарами, и потерянный товар восстановить было бы нечем.
 *
 * Порядок — не вкусовщина: на стойке он определяет, сколько кассир ищет «Напитки», которые берут
 * в десять раз чаще, чем «Батарейки». Алфавит расставлял их случайно.
 *
 * Строка — строка общего списка: название и действия вместе, а не текст столбиком с кнопками,
 * прижатыми к правому краю через всю ширину. Стрелки порядка стоят на виду слева от названия: их
 * жмут подряд («Напитки» на три места вверх), и в меню каждое нажатие стоило бы двух. Остальное —
 * переименовать, скрыть — в «⋯».
 */
export function CategoriesPanel({ backend, categories, canManage, onChanged, onFeedback }: {
  backend: OperatorBackendContext | null;
  categories: readonly CategoryOption[];
  canManage: boolean;
  onChanged: () => Promise<void>;
  onFeedback: (feedback: Feedback) => void;
}) {
  const { t } = useI18n();
  const [editingId, setEditingId] = useState<string | null>(null);
  const [draftName, setDraftName] = useState('');
  const [busy, setBusy] = useState(false);

  const startRename = (category: CategoryOption) => {
    setEditingId(category.categoryId);
    setDraftName(category.label);
  };

  const run = async (label: string, action: () => Promise<unknown>) => {
    setBusy(true);
    onFeedback({ label, state: 'pending' });
    try {
      await action();
      await onChanged();
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const submitRename = async () => {
    const categoryId = editingId;
    const name = draftName.trim();
    if (categoryId === null || name.length === 0) return;
    await run(t('op.management.goods.category.rename'), async () => {
      const nextBackend = requireBackend(backend, t);
      await createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session)
        .settings.updateProductCategory(nextBackend.branchId, categoryId, {
          organizationId: nextBackend.session.organizationId,
          name
        });
      setEditingId(null);
    });
  };

  const toggleVisibility = async (category: CategoryOption) => {
    const label = category.isActive
      ? t('op.management.goods.category.hide')
      : t('op.management.goods.category.show');
    await run(label, async () => {
      const nextBackend = requireBackend(backend, t);
      await createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session)
        .settings.updateProductCategory(nextBackend.branchId, category.categoryId, {
          organizationId: nextBackend.session.organizationId,
          isActive: !category.isActive
        });
    });
  };

  const move = async (categoryId: string, direction: -1 | 1) => {
    const order = moveCategory(categories, categoryId, direction);
    if (order === null) return;
    await run(t('op.management.goods.category.reorder'), async () => {
      const nextBackend = requireBackend(backend, t);
      await createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session)
        .settings.reorderProductCategories(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          categoryIds: order
        });
    });
  };

  return (
    <MgmtTable<CategoryOption>
      columns={[
        ...(canManage ? [{
          key: 'order',
          header: '',
          render: (category: CategoryOption) => {
            const index = categories.indexOf(category);
            return (
              <span className="mgmt-order-arrows">
                <IconButton
                  size="sm"
                  icon={<ChevronUp aria-hidden size={16} />}
                  label={t('op.management.goods.category.moveUp', { name: category.label })}
                  disabled={busy || index === 0}
                  onClick={() => void move(category.categoryId, -1)}
                />
                <IconButton
                  size="sm"
                  icon={<ChevronDown aria-hidden size={16} />}
                  label={t('op.management.goods.category.moveDown', { name: category.label })}
                  disabled={busy || index === categories.length - 1}
                  onClick={() => void move(category.categoryId, 1)}
                />
              </span>
            );
          }
        }] : []),
        {
          key: 'name',
          header: '',
          render: (category) => editingId === category.categoryId ? (
            <span className="mgmt-inline-edit">
              <input
                type="text"
                aria-label={t('op.management.goods.category.newName')}
                value={draftName}
                disabled={busy}
                autoFocus
                onChange={(event) => setDraftName(event.currentTarget.value)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') void submitRename();
                  if (event.key === 'Escape') setEditingId(null);
                }}
              />
              <Button size="sm" variant="primary" disabled={busy || draftName.trim().length === 0} onClick={() => void submitRename()}>
                {t('op.management.goods.category.save')}
              </Button>
              <Button size="sm" disabled={busy} onClick={() => setEditingId(null)}>{t('common.cancel')}</Button>
            </span>
          ) : (
            <span className={category.isActive ? undefined : 'mgmt-row-muted'}>
              {category.label}
              {!category.isActive && <> · {t('op.management.goods.category.hiddenMark')}</>}
            </span>
          )
        }
      ]}
      rows={[...categories]}
      rowKey={(category) => category.categoryId}
      gridTemplate={categoriesGrid(canManage)}
      rowActions={canManage ? (category) => editingId === category.categoryId ? [] : [
        {
          id: 'rename',
          label: t('op.management.goods.category.rename'),
          icon: <Pencil size={14} aria-hidden="true" />,
          disabled: busy,
          onSelect: () => startRename(category)
        },
        {
          id: 'visibility',
          label: category.isActive ? t('op.management.goods.category.hide') : t('op.management.goods.category.show'),
          icon: category.isActive ? <EyeOff size={14} aria-hidden="true" /> : <Eye size={14} aria-hidden="true" />,
          disabled: busy,
          onSelect: () => void toggleVisibility(category)
        }
      ] : undefined}
      toolbar={{ title: t('op.management.goods.category.title') }}
      empty={{
        icon: <Tags size={22} aria-hidden="true" />,
        title: t('op.management.goods.category.empty'),
        next: { kind: 'elsewhere', hint: t('op.management.goods.category.emptyHint') }
      }}
    />
  );
}
