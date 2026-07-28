import { useEffect, useRef } from 'react';
import { useI18n } from '@afk4/i18n';
import { feedbackText } from './operatorHelpers';
import { useToast } from './operatorToast';
import type { Feedback } from './operatorTypes';

// Единый канал результата действия — тост, самодельной пилюли-баннера над экраном больше
// нет (см. memory operator-feedback-toast: пилюля молча зависала до следующего действия,
// у неё даже не было способа её закрыть). Ref-гвард держит идентичность последнего
// обработанного `feedback`, чтобы тост не всплывал повторно на ре-рендерах, где сам объект
// не поменялся — эффект без deps-массива, поэтому не важна стабильность `toast`/`t`.
export function useFeedbackToasts(feedback: Feedback, options?: { notifySuccess?: boolean }) {
  const toast = useToast();
  const { t } = useI18n();
  const lastHandled = useRef<Feedback | null>(null);

  useEffect(() => {
    if (lastHandled.current === feedback) {
      return;
    }
    lastHandled.current = feedback;

    if (feedback.state === 'failed') {
      toast.error(feedbackText(feedback, t));
    } else if (feedback.state === 'confirmed' && options?.notifySuccess !== false) {
      toast.success(feedbackText(feedback, t));
    }
  });
}
