import type { ReactNode } from 'react';
import { ArrowDownToLine, RotateCcw } from 'lucide-react';
import { SetupSection } from '../../kit/SetupSection';

interface Props {
  // «in» = деньги приходят вам (приём), «out» = деньги возвращаются игроку (кэшбэк).
  direction: 'in' | 'out';
  title: string;
  lead: string; // человеческое пояснение «что это и зачем» — для того, кто зашёл впервые за месяцы
  children: ReactNode;
}

// Секция экрана «Платежи и лояльность»: та же оболочка, что у остальных setup-экранов
// (SetupSection), плюс иконка направления денег — единственное, что здесь своё.
export function PaymentsSetupSection({ direction, title, lead, children }: Props) {
  return (
    <SetupSection Icon={direction === 'in' ? ArrowDownToLine : RotateCcw} title={title} lead={lead}>
      {children}
    </SetupSection>
  );
}
