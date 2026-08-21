import type { ReactNode } from 'react';
import type { LucideIcon } from 'lucide-react';

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
