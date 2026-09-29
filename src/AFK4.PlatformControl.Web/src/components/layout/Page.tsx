import type { ReactNode } from 'react';
import { ChevronLeft } from 'lucide-react';
import { SectionHeader } from '@afk4/ui/react';

// Каркас экрана панели. Шапка — общая шапка раздела кита, та же, что у Панели: название и главная
// кнопка → вкладки → содержимое. Мелкой строки над названием больше нет (решение владельца
// 29.09: слоганы убраны): она то повторяла название, то показывала служебный slug.
//
// Мера ширины не косметика: форма во всю ширину монитора читается как недоделанный экран,
// а таблицу, наоборот, нельзя зажимать — отсюда три варианта колонки.
export function Page({ width = 'wide', back, title, actions, tabs, tools, children }: {
  width?: 'form' | 'wide' | 'full';
  back?: { label: string; onBack: () => void };
  // Узел, а не строка: пока карточка грузится, на месте названия стоит его заглушка.
  title?: ReactNode;
  actions?: ReactNode;
  /** Вкладки раздела — второй строкой шапки, под названием. */
  tabs?: ReactNode;
  /** Инструменты раздела — третьей строкой: фильтры, поиск. */
  tools?: ReactNode;
  children: ReactNode;
}) {
  return (
    <section className="pc-screen">
      {back !== undefined ? (
        <button type="button" className="pc-back" onClick={back.onBack}>
          <ChevronLeft size={14} aria-hidden="true" />
          {back.label}
        </button>
      ) : null}

      {title !== undefined ? <SectionHeader title={title} action={actions} tabs={tabs} tools={tools} /> : null}

      <div className={`management-content management-content--${width}`}>{children}</div>
    </section>
  );
}
