import { useI18n } from '@afk4/i18n';
import type { OperatorAuthSession } from './authClient';
import type { ShellShiftBadgeData } from './operatorHelpers';
import { navSections, type NavSection } from './operatorData';
import { canOpenWorkspace } from './operatorPermissions';
import { RailAccount } from './RailAccount';

// Левая навигационная рельса: кнопки секций (подсветка активной, замок на недоступных) + аккаунт
// оператора в подвале. Переходы и личность приходят пропсами.
export function WorkspaceRail({
  session,
  activeSectionKey,
  displayName,
  shift,
  onNavigateSection,
  onOpenAccount,
  onSignOut
}: {
  session: OperatorAuthSession | null;
  activeSectionKey: string;
  displayName: string;
  shift: ShellShiftBadgeData;
  onNavigateSection: (section: NavSection) => void;
  onOpenAccount: () => void;
  onSignOut: () => void;
}) {
  const { t } = useI18n();
  return (
    <nav className="workspace-rail" aria-label={t('op.shell.workspaces')}>
      {navSections.map((section) => {
        const Icon = section.icon;
        const isAllowed = section.items.some((item) => canOpenWorkspace(session, item.id));
        const label = t(section.labelKey);
        return (
          <button
            key={section.key}
            type="button"
            className={[section.key === activeSectionKey ? 'active' : '', !isAllowed ? 'locked' : ''].filter(Boolean).join(' ')}
            aria-disabled={!isAllowed}
            title={label}
            onClick={() => onNavigateSection(section)}
          >
            <Icon size={20} />
            <span>{label}</span>
          </button>
        );
      })}
      {/* Аккаунт оператора живёт в подвале рейла (margin-top:auto) — привычное место личности
          и выхода. Аватар раскрывает меню с профилем и «Выйти». */}
      <RailAccount displayName={displayName} shift={shift} onOpenAccount={onOpenAccount} onSignOut={onSignOut} />
    </nav>
  );
}
