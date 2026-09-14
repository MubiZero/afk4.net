import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from '../../../apiErrors';
import { createAuthenticatedOperatorClients, requireBackend } from '../../../operatorHelpers';
import type { Feedback, OperatorBackendContext } from '../../../operatorTypes';
import type { CategoryOption } from './categoryModel';

/**
 * Справочник категорий товара: посмотреть и переименовать.
 *
 * Переименования не было ни на одной стороне — ни маршрута, ни кнопки. Опечатка в названии,
 * сделанная при заведении первого товара, оставалась в меню бара навсегда: единственным выходом
 * было завести категорию заново и перевесить на неё все товары по одному.
 *
 * Удаления здесь нет намеренно: его нет и на сервере, а решать, что делать с товарами удаляемой
 * категории, — не задача кнопки.
 */
export function CategoriesPanel({ backend, categories, canManage, onRenamed, onFeedback }: {
  backend: OperatorBackendContext | null;
  categories: readonly CategoryOption[];
  canManage: boolean;
  onRenamed: () => Promise<void>;
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

  const submit = async () => {
    const label = t('op.management.goods.category.rename');
    const categoryId = editingId;
    const name = draftName.trim();
    if (categoryId === null || name.length === 0) return;
    setBusy(true);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      await createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session)
        .settings.renameProductCategory(nextBackend.branchId, categoryId, {
          organizationId: nextBackend.session.organizationId,
          name
        });
      setEditingId(null);
      await onRenamed();
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="mgmt-drawer-section">
      <div className="mgmt-section-title"><span>{t('op.management.goods.category.title')}</span></div>
      {categories.length === 0 ? (
        <p className="mgmt-drawer-hint">{t('op.management.goods.category.empty')}</p>
      ) : (
        <ul>
          {categories.map((category) => (
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
                    <button type="button" className="ui-btn ui-btn--primary ui-btn--sm" disabled={busy || draftName.trim().length === 0} onClick={() => void submit()}>
                      {t('op.management.goods.category.save')}
                    </button>
                    <button type="button" className="ui-btn ui-btn--sm" disabled={busy} onClick={() => setEditingId(null)}>
                      {t('common.cancel')}
                    </button>
                  </span>
                </>
              ) : (
                <>
                  <span>{category.label}</span>
                  {canManage && (
                    <button type="button" className="ui-btn ui-btn--sm" disabled={busy} onClick={() => startRename(category)}>
                      {t('op.management.goods.category.rename')}
                    </button>
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
