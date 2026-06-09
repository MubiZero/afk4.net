import { useEffect, useState } from 'react';
import type { TariffOptionDto } from '../apiTypes';
import { createCachedLoader, indexedDbStore } from '../idbCache';
import { ApiError, OfflineError, type ShellApi } from '../shellApi';

export interface ExtendScreenProps {
  api: ShellApi;
  branchId: string;
  sessionId: string;
  onExtended: () => void;
  onConflict: () => void;
}

export function ExtendScreen({ api, branchId, sessionId, onExtended, onConflict }: ExtendScreenProps) {
  const [tariffs, setTariffs] = useState<TariffOptionDto[]>([]);
  const [selected, setSelected] = useState<TariffOptionDto | null>(null);
  const [minutes, setMinutes] = useState(30);
  const [offline, setOffline] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    const load = createCachedLoader(indexedDbStore(), `tariffs:${branchId}`, () => api.listTariffs(branchId));
    load().then(setTariffs).catch((e) => { if (e instanceof OfflineError) setOffline(true); });
  }, [api, branchId]);

  async function extend() {
    if (!selected) return;
    setBusy(true);
    try {
      await api.extendSession(sessionId, { additionalMinutes: minutes, tariffRuleVersionId: selected.tariffRuleVersionId });
      onExtended();
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) onConflict();
      else if (e instanceof OfflineError) setOffline(true);
    } finally {
      setBusy(false);
    }
  }

  if (offline) return <p role="alert">Временно недоступно — обратитесь к оператору</p>;

  return (
    <section>
      <h1>Продлить время</h1>
      <ul>
        {tariffs.map((t) => (
          <li key={t.tariffVersionId}>
            <button onClick={() => setSelected(t)} aria-pressed={selected?.tariffVersionId === t.tariffVersionId}>
              {t.name}
            </button>
          </li>
        ))}
      </ul>
      <label htmlFor="minutes">Минут</label>
      <input id="minutes" type="number" min={1} value={minutes}
             onChange={(e) => setMinutes(Number(e.target.value))} />
      <button onClick={extend} disabled={!selected || busy}>Продлить</button>
    </section>
  );
}
