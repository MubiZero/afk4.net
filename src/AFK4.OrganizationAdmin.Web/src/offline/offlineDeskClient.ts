import type { OfflineSeatResponse, SessionLeaseDto } from '@afk4/contracts';
import { isHostBridgeAvailable, postHostRequest } from '../hostBridge';
import type { OfflineJournalRecord } from './offlineSessions';

/**
 * Офлайн у стойки (docs/roadmap/offline-front-desk.md): ключ этого ПК, передача подписанной аренды ПК в
 * зале и журнал кассы на диске — всё у нативного хоста. Подписывает хост: приватный ключ страница не видит.
 * В браузере хоста нет, и офлайн-действий Панель не предлагает.
 */
export const offlineDeskClient = {
  available: (): boolean => isHostBridgeAvailable(),
  key: () => postHostRequest<{ keyId: string; publicKeyPem: string }>('offline:key'),
  sendLease: (address: string, lease: Omit<SessionLeaseDto, 'signatureAlgorithm' | 'signature'>) =>
    postHostRequest<OfflineSeatResponse>('offline:sendLease', {
      address,
      lease: { ...lease, signatureAlgorithm: '', signature: '' }
    }),
  append: (record: OfflineJournalRecord) => postHostRequest<{ appended: boolean }>('offline:journalAppend', { record }),
  read: async (): Promise<OfflineJournalRecord[]> =>
    (await postHostRequest<{ records: OfflineJournalRecord[] }>('offline:journalRead')).records,
  acknowledge: (ids: string[]) => postHostRequest<{ remaining: number }>('offline:journalAck', { ids })
};

export type OfflineDeskClient = typeof offlineDeskClient;
