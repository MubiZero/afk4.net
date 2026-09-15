import { useState } from 'react';
import { ChevronDown, ChevronUp } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from '../../../apiErrors';
import { createAuthenticatedOperatorClients, requireBackend } from '../../../operatorHelpers';
import type { Feedback, OperatorBackendContext } from '../../../operatorTypes';
import type { CategoryOption } from './categoryModel';
import { moveCategory } from './categoryOrder';

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
    <section className="mgmt-drawer-section">
      <div className="mgmt-section-title"><span>{t('op.management.goods.category.title')}</span></div>
      {categories.length === 0 ? (
        <p className="mgmt-drawer-hint">{t('op.management.goods.category.empty')}</p>
      ) : (
        <ul>
          {categories.map((category, index) => (
            <li key={category.categoryId} className="mgmt-zone-row">
              {editingId === category.categoryId ? (
                <>
                  <input
                    type="text"
                    aria-label={t('op.management.goods.category.newName')}
                    value={draftName}
                    disabled={busy}
                    onChange={(event) => setDraftName(event.currentTarget.value)}
                  />
                  <span className="mgmt-status-pair">
                    <button type="button" className="ui-btn ui-btn--primary ui-btn--sm" disabled={busy || draftName.trim().length === 0} onClick={() => void submitRename()}>
                      {t('op.management.goods.category.save')}
                    </button>
                    <button type="button" className="ui-btn ui-btn--sm" disabled={busy} onClick={() => setEditingId(null)}>
                      {t('common.cancel')}
                    </button>
                  </span>
                </>
              ) : (
                <>
                  <span className={category.isActive ? undefined : 'mgmt-row-muted'}>
                    {category.label}
                    {!category.isActive && <> · {t('op.management.goods.category.hiddenMark')}</>}
                  </span>
                  {canManage && (
                    <span className="mgmt-status-pair">
                      <button
                        type="button"
                        className="ui-btn ui-btn--icon ui-btn--sm"
                        disabled={busy || index === 0}
                        aria-label={t('op.management.goods.category.moveUp', { name: category.label })}
                        onClick={() => void move(category.categoryId, -1)}
                      >
                        <ChevronUp aria-hidden size={16} />
                      </button>
                      <button
                        type="button"
                        className="ui-btn ui-btn--icon ui-btn--sm"
                        disabled={busy || index === categories.length - 1}
                        aria-label={t('op.management.goods.category.moveDown', { name: category.label })}
                        onClick={() => void move(category.categoryId, 1)}
                      >
                        <ChevronDown aria-hidden size={16} />
                      </button>
                      <button type="button" className="ui-btn ui-btn--sm" disabled={busy} onClick={() => void toggleVisibility(category)}>
                        {category.isActive
                          ? t('op.management.goods.category.hide')
                          : t('op.management.goods.category.show')}
                      </button>
                      <button type="button" className="ui-btn ui-btn--sm" disabled={busy} onClick={() => startRename(category)}>
                        {t('op.management.goods.category.rename')}
                      </button>
                    </span>
                  )}
                </>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
