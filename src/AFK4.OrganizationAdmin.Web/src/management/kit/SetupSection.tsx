import type { ReactNode } from 'react';
import type { LucideIcon } from 'lucide-react';
import { SkeletonControl, SkeletonLine } from '../../LoadingSkeleton';

interface Props {
  Icon: LucideIcon;
  title: string;
  lead: string; // человеческое пояснение «что это и зачем» — для того, кто зашёл впервые за месяцы
  children: ReactNode;
}

// Спокойная секция setup-экрана: иконка + заголовок + лид, затем содержимое. Такие экраны
// («Платежи и лояльность», «Приём броней») посещают раз в несколько месяцев, поэтому лид
// объясняет смысл словами, а не оставляет оператора наедине с полями.
export function SetupSection({ Icon, title, lead, children }: Props) {
  return (
    <section className="payset-section">
      <header className="payset-section-head">
        <span className="payset-section-icon" aria-hidden="true">
          <Icon size={20} strokeWidth={2} />
        </span>
        <div className="payset-section-heading">
          <h2 className="payset-section-title">{title}</h2>
          <p className="payset-section-lead">{lead}</p>
        </div>
      </header>
      {children}
    </section>
  );
}

// Подсказка под подзаголовком «Ограничения» поджата к нему — одна на настоящую секцию и её заглушку.
export const SETUP_LIMITS_HINT_STYLE = { marginTop: -6, marginBottom: 14 } as const;

// Подзаголовок внутри секции (.payset-subhead).
export function SetupSubheadSkeleton() {
  return <div className="payset-subhead" aria-hidden="true"><SkeletonLine width="10em" /></div>;
}

// Заглушки setup-экрана собраны из тех же классов .payset-*, что и настоящие секции: высоту шапке,
// карточке правила и числовому полю задают они сами. Пояснения под правилами и полями — настоящим
// текстом: они известны до ответа, а их переносы и задают высоту. Не угадать заранее только то,
// что зависит от ответа, — пример начисления под включённым правилом.
export function SetupSectionSkeleton({ lead, children }: { lead: string; children: ReactNode }) {
  return (
    <section className="payset-section" data-skeleton="form" aria-hidden="true">
      <header className="payset-section-head">
        <span className="payset-section-icon skeleton-block" />
        <div className="payset-section-heading">
          <h2 className="payset-section-title"><SkeletonLine width="12em" /></h2>
          <p className="payset-section-lead">{lead}</p>
        </div>
      </header>
      {children}
    </section>
  );
}

// Карточка правила: переключатель, название и пояснение; `percentLabel` — у правила есть поле
// процента справа (кэшбэк), и оно сужает колонку текста.
export function SetupRuleSkeleton({ hint, percentLabel }: { hint: string; percentLabel?: string }) {
  return (
    <div className="payset-rule" data-skeleton="form" aria-hidden="true">
      <div className="payset-rule-top">
        <span className="payset-switch skeleton-block" />
        <div className="payset-rule-text">
          <div className="payset-rule-name"><SkeletonLine width="40%" /></div>
          <div className="payset-rule-hint">{hint}</div>
        </div>
        {percentLabel !== undefined && (
          <span className="payset-pct"><span>{percentLabel}</span><SkeletonControl width="72px" /></span>
        )}
      </div>
    </div>
  );
}

// Числовые поля: подпись, поле и подсказка под ним — по полю на каждую подсказку.
export function SetupFieldsSkeleton({ hints, single = false }: { hints: string[]; single?: boolean }) {
  return (
    <div className={`payset-limits${single ? ' payset-limits--single' : ''}`} data-skeleton="form" aria-hidden="true">
      {hints.map((hint, index) => (
        <div key={index} className="payset-field">
          <label><SkeletonLine width="10em" /></label>
          <div className="payset-field-input"><SkeletonControl width="140px" /></div>
          <p className="payset-field-hint">{hint}</p>
        </div>
      ))}
    </div>
  );
}
