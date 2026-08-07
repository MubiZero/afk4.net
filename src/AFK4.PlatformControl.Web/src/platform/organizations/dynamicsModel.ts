import { minorToMajor } from '@/lib/money';
import type { BranchDynamicsDay } from '@/api/types';

export type DynamicsPoint = { date: string; revenue: number; sessions: number };

export function toDynamicsSeries(days: BranchDynamicsDay[]): DynamicsPoint[] {
  return days.map(day => ({
    date: day.date,
    revenue: minorToMajor(day.revenue.minorUnits),
    sessions: day.sessionCount
  }));
}

// «Не выходил на связь» (false) и «нет данных о связи» (null/undefined) сервер уже считает
// сам (`daysWithoutAgent`/`daysWithUnknownAgent` в BranchDynamicsDto) — эти цифры нужно брать
// оттуда, а не пересчитывать заново на клиенте по тем же `days`: два независимых куска кода,
// считающих одно и то же число, рано или поздно разойдутся, и разницу поймает глаз пользователя,
// а не тест. «Выходил на связь» (true) сервер отдельным полем не отдаёт — это единственная из
// трёх величин, для которой клиентский пересчёт законен, потому что альтернативы нет.
export function countAliveDays(days: BranchDynamicsDay[]): number {
  return days.filter(day => day.agentAlive === true).length;
}
