import type { JSX, ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';
import { LoadFailureState } from '../operatorPrimitives';
import { projectOperatorError } from '../apiErrors';
import type { Section } from './useSection';
import { DeferredSkeleton } from '../LoadingSkeleton';

// Секция, которая ещё грузится или не пришла, говорит за себя внутри своей панели, а соседняя
// остаётся на экране. Вид ошибки — тот же, что у секций «Платежей и лояльности»: что не
// загрузилось, почему и «Повторить», который перезапрашивает только эту секцию, — если повтор
// может помочь. Пока секция грузится, на её месте стоит `skeleton` — форма того, что в ней появится.
export function SectionState<T>({ section, failedTitle, skeleton }: {
  section: Section<T>;
  failedTitle: string;
  skeleton: ReactNode;
}): JSX.Element | null {
  const { t } = useI18n();
  if (section.status === 'loading') {
    return <DeferredSkeleton>{skeleton}</DeferredSkeleton>;
  }
  if (section.status === 'error') {
    return <LoadFailureState title={failedTitle} failure={projectOperatorError(section.error, t)} onRetry={section.retry} />;
  }
  return null;
}
