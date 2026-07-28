export type Guid = string;

export interface MoneyDto {
  currencyCode: string;
  minorUnits: number;
}

export interface ReportQuery {
  fromUtc?: string | Date | null;
  toUtc?: string | Date | null;
  limit?: number | null;
}
