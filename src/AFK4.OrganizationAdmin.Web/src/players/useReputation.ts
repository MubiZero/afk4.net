import { useCallback, useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from '../apiErrors';
import { createAuthenticatedOperatorClients } from '../operatorHelpers';
import type { OperatorBackendContext } from '../operatorTypes';
import type { PlayerReputationDto } from '../operatorApiClients';
import { reputationErrorKey, reputationLookupPhone } from './reputationModel';

export type ReputationState =
  | { status: 'noPhone' }
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; reputation: PlayerReputationDto }
  | { status: 'failed'; detail: string };

export interface ReputationController {
  state: ReputationState;
  ask: () => void;
}

// Ответы сети за сессию. Сеть считает числа раз в сутки, поэтому второй запрос по тому же
// номеру не узнал бы ничего нового — зато оставил бы вторую запись в аудите клуба.
const answers = new Map<string, PlayerReputationDto>();

/** Только для тестов: сбросить накопленные за сессию ответы. */
export function clearReputationAnswers(): void {
  answers.clear();
}

/**
 * Репутация одного человека в карточке.
 *
 * Сеть спрашивается по нажатию, а не при открытии карточки: каждый запрос пишется в аудит на
 * сам факт чтения, и «пролистал двадцать клиентов» превратилось бы в двадцать записей о том,
 * что клуб интересовался чужими гостями. Один осознанный клик — одна запись.
 */
export function useReputation(
  backend: OperatorBackendContext | null,
  rawPhoneNumber: string
): ReputationController {
  const { t } = useI18n();
  const phone = reputationLookupPhone(rawPhoneNumber);
  const initial = (): ReputationState => {
    if (phone === null) return { status: 'noPhone' };
    const known = answers.get(phone);
    return known ? { status: 'ready', reputation: known } : { status: 'idle' };
  };
  const [state, setState] = useState<ReputationState>(initial);

  // Карточка переехала на другого человека — показываем его ответ (или предложение спросить),
  // а не оставшийся на экране чужой.
  useEffect(() => { setState(initial()); }, [phone]);

  const ask = useCallback(() => {
    if (backend === null || phone === null) return;
    setState({ status: 'loading' });
    createAuthenticatedOperatorClients(backend.config, backend.session).players
      .lookupReputation(backend.branchId, phone)
      .then((reputation) => {
        answers.set(phone, reputation);
        setState({ status: 'ready', reputation });
      })
      .catch((error) => {
        const key = reputationErrorKey(error);
        setState({ status: 'failed', detail: key ? t(key) : projectOperatorError(error, t).detail });
      });
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, phone, t]);

  return { state, ask };
}
