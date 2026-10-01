import { useEffect, useSyncExternalStore, type ReactNode } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { DEV_SCENARIOS, type DevHostControl, type DevScenario } from '../host/devHost';
import './demo.css';

// Ключи названы целиком, а не собираются из имени сценария: проверка «мёртвых» ключей каталога
// ищет их в исходниках.
const SCENARIO_LABELS: Record<DevScenario, MessageKey> = {
  idle: 'playerShell.demo.scenario.idle',
  approach: 'playerShell.demo.scenario.approach',
  session: 'playerShell.demo.scenario.session',
  ending: 'playerShell.demo.scenario.ending',
  grace: 'playerShell.demo.scenario.grace',
  offline: 'playerShell.demo.scenario.offline',
  maintenance: 'playerShell.demo.scenario.maintenance',
  error: 'playerShell.demo.scenario.error',
  connecting: 'playerShell.demo.scenario.connecting'
};

/**
 * Публичное демо экрана игрока: деньги и гости выдуманы, ничего не уходит в сеть. Полоса стоит над
 * экраном, а не поверх него: оболочка — киоск на весь монитор, и закрытая полосой кнопка была бы
 * потерянной кнопкой. Закрыть полосу нельзя — посетитель не должен принять пример за чужой клуб.
 */
export function DemoBand({ control, reload = () => window.location.assign(window.location.pathname) }: { control: DevHostControl; reload?: () => void }) {
  const { t } = useI18n();
  const scenario = useSyncExternalStore(control.subscribe, control.getScenario);
  const restart = () => {
    try {
      sessionStorage.clear();
      localStorage.clear();
    } catch {
      // Хранилище закрыто — достаточно перезагрузки: данные демо живут в памяти страницы.
    }
    reload();
  };

  return (
    <div className="demo-band" role="group" aria-label={t('playerShell.demo.badge')}>
      <span className="demo-band__badge">{t('playerShell.demo.badge')}</span>
      <span className="demo-band__text">{t('playerShell.demo.text')}</span>
      <label className="demo-band__scenario">
        {t('playerShell.demo.scenarioLabel')}
        <select value={scenario} onChange={(event) => control.setScenario(event.target.value as DevScenario)}>
          {DEV_SCENARIOS.map((name) => (
            <option key={name} value={name}>{t(SCENARIO_LABELS[name])}</option>
          ))}
        </select>
      </label>
      <button type="button" className="demo-band__action" onClick={restart}>{t('playerShell.demo.restart')}</button>
      <a className="demo-band__action" href="../">{t('playerShell.demo.panelLink')}</a>
    </div>
  );
}

export function DemoFrame({ control, children }: { control: DevHostControl; children: ReactNode }) {
  // Клавиша — «подошли к ПК», как сообщил бы агент; кроме клавиш самой полосы (она не часть ПК) и
  // Tab: им посетитель идёт к переключателю, и окно входа не должно перехватывать фокус по дороге.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== 'Tab' && !(event.target as Element | null)?.closest?.('.demo-band')) control.touch();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [control]);

  return (
    <div className="demo-frame">
      <DemoBand control={control} />
      <div className="demo-stage" onPointerMove={control.touch} onPointerDown={control.touch}>{children}</div>
    </div>
  );
}
