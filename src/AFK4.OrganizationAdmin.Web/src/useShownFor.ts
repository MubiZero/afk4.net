import { useRef } from 'react';

// Заглушка — только пока показывать нечего: первая загрузка и смена того, для чего грузим
// (филиал). Повтор после собственного действия — «Внести», «Отменить продажу», «Принять оплату» —
// идёт тихо поверх показанного, как в Platform Control (#377): экран, который человек только что
// читал, пропадал на полсекунды, и это читалось как сбой.
export function useShownFor(key: string): { isShown: () => boolean; markShown: () => void } {
  const shownFor = useRef<string | null>(null);
  return {
    isShown: () => shownFor.current === key,
    markShown: () => { shownFor.current = key; }
  };
}
