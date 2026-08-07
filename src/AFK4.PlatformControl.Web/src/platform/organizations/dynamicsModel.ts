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

// Три состояния связи держатся раздельно: `false` — клуб реально не выходил на связь,
// `null`/`undefined` — данных о связи нет (обычно наш собственный простой), не вина клуба.
// Смешивать их в один "плохой" бакет нельзя — это разные факты с разными формулировками.
export function summarizeAgentDays(days: BranchDynamicsDay[]) {
  return {
    alive: days.filter(day => day.agentAlive === true).length,
    dead: days.filter(day => day.agentAlive === false).length,
    unknown: days.filter(day => day.agentAlive === null || day.agentAlive === undefined).length
  };
}
