import type { ReactNode } from 'react';
import { ArrowDownToLine, RotateCcw } from 'lucide-react';

interface Props {
  // «in» = деньги приходят вам (приём), «out» = деньги возвращаются игроку (кэшбэк).
  direction: 'in' | 'out';
  title: string;
  lead: string; // человеческое пояснение «что это и зачем» — для того, кто зашёл впервые за месяцы
  children: ReactNode;
}

// Спокойная секция setup-экрана «Платежи и лояльность»: иконка направления + заголовок + лид,
// затем содержимое. Экран посещают редко, поэтому лид объясняет смысл словами, а не оставляет
// оператора наедине с полями.
export function PaymentsSetupSection({ direction, title, lead, children }: Props) {
  const Icon = direction === 'in' ? ArrowDownToLine : RotateCcw;
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
