import { useI18n } from '@afk4/i18n';
import type { OperatorAuthSession } from './authClient';
import type { ShellShiftBadgeData } from './operatorHelpers';
import { navSections, type NavSection } from './operatorData';
import { canOpenWorkspace } from './operatorPermissions';
import type { WorkspaceId } from './operatorTypes';
import { RailAccount } from './RailAccount';

// Левая навигационная рельса: кнопки секций (подсветка активной, замок на недоступных) + аккаунт
// оператора в подвале. Переходы и личность приходят пропсами.
export function WorkspaceRail({
  session,
  visibleWorkspaceIds,
  activeSectionKey,
  displayName,
  shift,
  onNavigateSection,
  onOpenAccount,
  onSignOut
}: {
  session: OperatorAuthSession | null;
  // Extra restriction on top of ordinary permission checks — a support session's writableAreas
  // (see support/supportWorkspaces.ts). `null`/omitted outside support mode: permissions alone decide.
  visibleWorkspaceIds?: ReadonlySet<WorkspaceId> | null;
  activeSectionKey: string;
  displayName: string;
  shift: ShellShiftBadgeData;
  onNavigateSection: (section: NavSection) => void;
  onOpenAccount: () => void;
  onSignOut: () => void;
}) {
  const { t } = useI18n();
  const visibleSections = navSections.filter((section) =>
    section.items.some((item) =>
      canOpenWorkspace(session, item.id) && (visibleWorkspaceIds == null || visibleWorkspaceIds.has(item.id))
    )
  );

  return (
    <nav className="workspace-rail" aria-label={t('op.shell.workspaces')}>
      {visibleSections.map((section) => {
        const Icon = section.icon;
        const label = t(section.labelKey);
        return (
          <button
            key={section.key}
            type="button"
            className={section.key === activeSectionKey ? 'active' : ''}
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
