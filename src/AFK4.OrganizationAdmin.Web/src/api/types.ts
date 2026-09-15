
export type { Guid, MoneyDto } from '@afk4/contracts';

export interface ReportQuery {
  fromUtc?: string | Date | null;
  toUtc?: string | Date | null;
  limit?: number | null;
}
