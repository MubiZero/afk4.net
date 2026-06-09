import type { PlayerTopUpIntentDto } from './apiTypes';

export type PaymentStatus = 'pending' | 'fulfilled' | 'expired' | 'disputed';

/** Server is authoritative. The shell NEVER infers success from "QR scanned"; only `fulfilled` counts. */
export function toPaymentStatus(intent: Pick<PlayerTopUpIntentDto, 'state' | 'isExpired'> & { disputed?: boolean }): PaymentStatus {
  if (intent.disputed) return 'disputed';
  if (intent.state === 'fulfilled') return 'fulfilled';
  if (intent.state === 'expired' || intent.isExpired) return 'expired';
  return 'pending';
}
