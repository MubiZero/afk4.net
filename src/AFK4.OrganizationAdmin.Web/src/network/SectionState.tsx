import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { LoadFailureState } from '../operatorPrimitives';
import { projectOperatorError } from '../apiErrors';
import type { Section } from './useSection';

// Секция, которая ещё грузится или не пришла, говорит за себя внутри своей панели, а соседняя
// остаётся на экране. Вид ошибки — тот же, что у секций «Платежей и лояльности»: что не
// загрузилось, почему и «Повторить», который перезапрашивает только эту секцию, — если повтор
// может помочь.
export function SectionState<T>({ section, failedTitle }: { section: Section<T>; failedTitle: string }): JSX.Element | null {
  const { t } = useI18n();
  if (section.status === 'loading') {
    return (
      <div className="management-skeleton" aria-hidden="true">
        <div className="management-skeleton-line" />
        <div className="management-skeleton-line" />
      </div>
    );
  }
  if (section.status === 'error') {
    return <LoadFailureState title={failedTitle} failure={projectOperatorError(section.error, t)} onRetry={section.retry} />;
  }
  return null;
}
