import type { LucideIcon } from 'lucide-react';

export interface SectionNavItem<T extends string> {
  id: T;
  label: string;
  Icon: LucideIcon;
}

export interface SectionNavGroup<T extends string> {
  /** Подпись группы; без неё — просто список (у «Сети» разделов мало, группировать нечего). */
  label?: string;
  items: SectionNavItem<T>[];
}

// Меню разделов «Управления» и «Сети» — одно на оба. Плоский список: пункт — строка текста с
// иконкой, выбранный — подложкой и aria-current. Раньше каждый пункт был плиткой в своей рамке,
// и двенадцать рамок подряд весили больше, чем экран, который они открывают.
export function SectionNav<T extends string>({ label, groups, current, onSelect }: {
  label: string;
  groups: SectionNavGroup<T>[];
  current: T;
  onSelect: (id: T) => void;
}) {
  return (
    <nav className="management-nav" aria-label={label}>
      {groups.filter((group) => group.items.length > 0).map((group, index) => (
        <div key={group.label ?? index} className="management-nav-group">
          {group.label !== undefined && <h2 className="ui-section-label management-nav-group-label">{group.label}</h2>}
          <ul>
            {group.items.map(({ id, label: itemLabel, Icon }) => (
              <li key={id}>
                <button type="button" aria-current={id === current ? 'page' : undefined} onClick={() => onSelect(id)}>
                  <Icon size={16} aria-hidden="true" />
                  <span>{itemLabel}</span>
                </button>
              </li>
            ))}
          </ul>
        </div>
      ))}
    </nav>
  );
}
