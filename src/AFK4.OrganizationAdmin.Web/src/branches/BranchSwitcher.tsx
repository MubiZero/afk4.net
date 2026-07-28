import { Building2 } from 'lucide-react';
import { useI18n } from '@afk4/i18n';

export interface BranchSwitcherOption {
  branchId: string;
  name: string;
}

/**
 * Переключатель активного филиала в шапке shell. Все филиалы показаны сразу как ряд чипов
 * (не спрятаны за раскрывающимся триггером) — у оператора их обычно 2-4, а всегда видимый
 * список даёт мгновенное переключение в один тап, без лишнего шага «сначала открой меню»
 * (важно для будущей мобилки). Активный чип подсвечен и помечен aria-current.
 */
export function BranchSwitcher({ branches, activeBranchId, onSelect }: {
  branches: readonly BranchSwitcherOption[];
  activeBranchId: string | null;
  onSelect: (branchId: string) => void;
}) {
  const { t } = useI18n();
  if (branches.length === 0) return null;

  return (
    <div className="branch-switcher" role="group" aria-label={t('op.branch.switcher.groupLabel')}>
      <Building2 size={14} className="branch-switcher-icon" aria-hidden="true" />
      {branches.map((branch) => {
        const isActive = branch.branchId === activeBranchId;
        return (
          <button
            key={branch.branchId}
            type="button"
            className={`ui-chip ui-chip--filter branch-switcher-item${isActive ? ' is-active' : ''}`}
            aria-current={isActive ? 'true' : undefined}
            onClick={() => onSelect(branch.branchId)}
          >
            {branch.name || t('op.branch.unnamed')}
          </button>
        );
      })}
    </div>
  );
}
