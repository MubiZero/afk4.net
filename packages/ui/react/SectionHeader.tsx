import type { ReactNode } from 'react';
import { CountChip, type CountChipProps } from './chips';

// Не больше трёх счётчиков — типом, а не договорённостью: четвёртый чип в шапке уже не «главное
// о разделе», а приборная доска, и спор об этом должен случиться до вёрстки, а не после.
export type HeaderCounts = readonly [] | readonly [CountChipProps] | readonly [CountChipProps, CountChipProps] | readonly [CountChipProps, CountChipProps, CountChipProps];

// Шапка раздела — один порядок на все разделы обеих админок:
//   1) название слева; справа до трёх счётчиков и одна главная кнопка;
//   2) вкладки раздела (`tabs`, обычно <Tabs>);
//   3) инструменты текущей вкладки — поиск, фильтры (`tools`).
// Мелкой строки над названием нет, и под вкладками имя вкладки не повторяется. Шапка не исчезает
// ни на одной вкладке: иначе вкладки прыгают вверх-вниз при переключении.
export function SectionHeader({ title, counts = [], action, tabs, tools, level = 1 }: {
  title: ReactNode;
  counts?: HeaderCounts;
  /** Одна главная кнопка раздела («Новый клиент»). Вторая главная рядом — уже развилка, а не шаг. */
  action?: ReactNode;
  tabs?: ReactNode;
  tools?: ReactNode;
  /** Уровень заголовка: раздел приложения — h1, раздел внутри страницы — h2. */
  level?: 1 | 2;
}) {
  const Heading = level === 1 ? 'h1' : 'h2';
  return (
    <header className="ui-section-header">
      <div className="ui-section-header-main">
        <Heading className="ui-section-title">{title}</Heading>
        {counts.length > 0 || action !== undefined ? (
          <div className="ui-section-header-aside">
            {counts.map((count) => <CountChip key={count.label} {...count} />)}
            {action}
          </div>
        ) : null}
      </div>
      {tabs}
      {tools !== undefined ? <div className="ui-section-header-tools">{tools}</div> : null}
    </header>
  );
}
