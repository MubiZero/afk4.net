import type { ApiTransport } from '../apiTransport';
import type {
  CashOperationReport,
  GameplayTimeReport,
  OperatorActionReport,
  SalesReport,
  ShiftReport
} from '../types';

export class ReportsApi {
  public constructor(private readonly transport: ApiTransport) {}

  public getShiftReport(branchId: string, fromUtc?: string, toUtc?: string, limit?: number): Promise<ShiftReport> {
    return this.transport.send<ShiftReport>('GET', `/api/branches/${encodeURIComponent(branchId)}/reports/shifts${reportQuery(fromUtc, toUtc, limit)}`);
  }

  public getSalesReport(branchId: string, fromUtc?: string, toUtc?: string, limit?: number): Promise<SalesReport> {
    return this.transport.send<SalesReport>('GET', `/api/branches/${encodeURIComponent(branchId)}/reports/sales${reportQuery(fromUtc, toUtc, limit)}`);
  }

  public getGameplayTimeReport(branchId: string, fromUtc?: string, toUtc?: string, limit?: number): Promise<GameplayTimeReport> {
    return this.transport.send<GameplayTimeReport>('GET', `/api/branches/${encodeURIComponent(branchId)}/reports/gameplay-time${reportQuery(fromUtc, toUtc, limit)}`);
  }

  public getCashOperationReport(branchId: string, fromUtc?: string, toUtc?: string, limit?: number): Promise<CashOperationReport> {
    return this.transport.send<CashOperationReport>('GET', `/api/branches/${encodeURIComponent(branchId)}/reports/cash-operations${reportQuery(fromUtc, toUtc, limit)}`);
  }

  public getOperatorActionReport(branchId: string, fromUtc?: string, toUtc?: string, limit?: number): Promise<OperatorActionReport> {
    return this.transport.send<OperatorActionReport>('GET', `/api/branches/${encodeURIComponent(branchId)}/reports/operator-actions${reportQuery(fromUtc, toUtc, limit)}`);
  }

  public async fetchReportCsv(branchId: string, name: string, fromUtc?: string, toUtc?: string): Promise<Blob> {
    const response = await this.transport.sendRaw('GET', `/api/branches/${encodeURIComponent(branchId)}/reports/${name}/export.csv${reportQuery(fromUtc, toUtc, undefined)}`);
    return response.blob();
  }
}

function reportQuery(fromUtc?: string, toUtc?: string, limit?: number): string {
  const params = new URLSearchParams();
  if (fromUtc !== undefined) params.set('fromUtc', fromUtc);
  if (toUtc !== undefined) params.set('toUtc', toUtc);
  if (limit !== undefined) params.set('limit', String(limit));
  const qs = params.toString();
  return qs.length > 0 ? `?${qs}` : '';
}
