import { useCallback, useEffect, useState } from 'react';
import type { MoneyDto, PlayerDashboardDto } from '@afk4/contracts';
import { getJson } from '../../api/playerApi';

/**
 * Баланс владельца сессии — для колонки «время и деньги». Раньше его было видно только внутри
 * листа продления и вкладки пополнения: сколько осталось денег, человек узнавал, уже нажав
 * «Продлить». Обновляется после каждого денежного шага на экране — продления, заказа, пополнения.
 */
export function useWalletBalance(baseUrl: string | null, enabled: boolean) {
  const [balance, setBalance] = useState<MoneyDto | null>(null);

  const reload = useCallback(async () => {
    if (!baseUrl) return;
    try {
      setBalance((await getJson<PlayerDashboardDto>(baseUrl, '/api/me/dashboard')).walletBalance);
    } catch {
      // Не узнали — строки нет, а не «0 с.»: ноль был бы враньём.
    }
  }, [baseUrl]);

  useEffect(() => {
    if (enabled) void reload();
  }, [enabled, reload]);

  return { balance: enabled ? balance : null, reload };
}
