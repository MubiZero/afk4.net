import {
  AlertTriangle,
  ArrowRightLeft,
  Clock3,
  LockKeyhole,
  Maximize2,
  Minus,
  Plus,
  Search,
  Square,
  TimerReset,
  Wifi,
  X
} from 'lucide-react';
import { useState, type MouseEvent } from 'react';
import { postHostWindowCommand } from './hostBridge';
import { getOperatorConfig } from './operatorConfig';
import { navItems, seats, signals, type SeatSummary, type SeatTone } from './operatorData';

type WorkspaceId = 'map' | 'dashboard' | 'booking' | 'pos' | 'players' | 'payments' | 'logs' | 'ops';

const workspaceIds: WorkspaceId[] = ['map', 'dashboard', 'booking', 'pos', 'players', 'payments', 'logs', 'ops'];

const toneLabels: Record<SeatTone, string> = {
  ready: 'Готов',
  active: 'Активно',
  pending: 'Команда',
  warning: 'Внимание',
  blocking: 'Блокер',
  offline: 'Офлайн',
  service: 'Сервис'
};

const problemTones = new Set<SeatTone>(['pending', 'warning', 'blocking', 'offline', 'service']);

function handleWindowDragStart(event: MouseEvent<HTMLElement>) {
  if (event.button !== 0) {
    return;
  }

  const target = event.target as HTMLElement;
  if (target.closest('button, input, .command-search')) {
    return;
  }

  postHostWindowCommand('drag');
}

function countByTone(tone: SeatTone): number {
  return seats.filter((seat) => seat.tone === tone).length;
}

function countProblems(): number {
  return seats.filter((seat) => problemTones.has(seat.tone)).length;
}

function zoneClass(zone: string): string {
  if (zone.includes('VIP')) {
    return 'zone-vip';
  }

  if (zone.includes('Bootcamp')) {
    return 'zone-bootcamp';
  }

  if (zone.includes('C')) {
    return 'zone-c';
  }

  if (zone.includes('B')) {
    return 'zone-b';
  }

  return 'zone-a';
}

function SeatTile({ seat, selected }: { seat: SeatSummary; selected?: boolean }) {
  return (
    <article
      className={`seat-tile ${zoneClass(seat.zone)} state-${seat.tone}${selected ? ' selected' : ''}`}
      aria-label={`${seat.name} ${seat.stateLabel}`}
    >
      <header className="seat-head">
        <div>
          <strong>{seat.name}</strong>
          <span>{seat.zone}</span>
        </div>
        <span className="state-chip">{seat.stateLabel}</span>
      </header>
      <div className="seat-main">
        <span>{seat.player}</span>
        <span>{seat.app}</span>
      </div>
      <footer>
        <strong>{seat.remaining}</strong>
        <span>{seat.command}</span>
      </footer>
    </article>
  );
}

function StateFlag({ label, value, critical }: { label: string; value: string; critical?: boolean }) {
  return (
    <section className={`state-flag${critical ? ' critical' : ''}`}>
      <span>{label}</span>
      <strong>{value}</strong>
    </section>
  );
}

function MapWorkspace({ currencyCode }: { currencyCode: string }) {
  return (
    <main className="floor-workspace">
      <section className="map-toolbar">
        <div>
          <span>Map</span>
          <h1>AFK4 Dushanbe · Floor A</h1>
        </div>
        <div className="filter-row" aria-label="Фильтры">
          <button type="button" className="active">Карта</button>
          <button type="button">Таблица</button>
          <button type="button">Брони</button>
          <button type="button">Тех. режим</button>
        </div>
      </section>

      <section className="state-strip" aria-label="Сводка">
        <StateFlag label="Active sessions" value={String(countByTone('active'))} />
        <StateFlag label="Free hosts" value={String(countByTone('ready'))} />
        <StateFlag label="Commands" value={String(countByTone('pending'))} critical={countByTone('pending') > 0} />
        <StateFlag label="No connection" value={String(countByTone('offline'))} critical={countByTone('offline') > 0} />
        <StateFlag label="Shell alerts" value={String(countProblems())} critical={countProblems() > 0} />
        <StateFlag label="Касса" value={`4 820 ${currencyCode}`} />
      </section>

      <section className="map-board" aria-label="ПК зала">
        <div className="seat-grid">
          {seats.map((seat) => (
            <SeatTile key={seat.id} seat={seat} selected={seat.id === 'pc-01'} />
          ))}
        </div>
      </section>
    </main>
  );
}

function DashboardWorkspace({ currencyCode }: { currencyCode: string }) {
  return (
    <main className="workspace-screen">
      <section className="screen-head">
        <div>
          <span>Dashboard</span>
          <h1>Операционная сводка смены</h1>
        </div>
        <div className="filter-row">
          <button type="button" className="active">Сегодня</button>
          <button type="button">Задачи</button>
          <button type="button">Лицензии</button>
        </div>
      </section>

      <section className="state-strip">
        <StateFlag label="Revenue" value={`4 820 ${currencyCode}`} />
        <StateFlag label="Active users" value="9" />
        <StateFlag label="Open tasks" value="6" critical />
        <StateFlag label="Busy accounts" value="3" />
      </section>

      <section className="smart-grid dashboard-layout">
        <div className="panel">
          <header className="panel-head">
            <strong>Смена</strong>
            <span>оператор, касса, задачи</span>
          </header>
          <div className="task-list">
            {[
              ['14:22', 'Пополнение депозита', `Madina S. · 200 ${currencyCode}`],
              ['14:30', 'Продажа POS', `Guest · 59 ${currencyCode}`],
              ['14:41', 'Команда устройства', 'PC-17 · start pending'],
              ['14:55', 'Возврат', `Yusuf A. · -20 ${currencyCode}`]
            ].map(([time, title, meta]) => (
              <div key={`${time}-${title}`} className="task-row">
                <span>{time}</span>
                <strong>{title}</strong>
                <em>{meta}</em>
              </div>
            ))}
          </div>
        </div>

        <div className="panel">
          <header className="panel-head">
            <strong>Быстрые действия</strong>
            <span>без ухода с рабочего экрана</span>
          </header>
          <div className="quick-actions">
            {['Пополнить депозит', 'Создать бронь', 'Продать товар', 'Открыть платеж', 'Зарегистрировать клиента', 'Показать логи'].map((label) => (
              <button key={label} type="button" className="quick-action">{label}</button>
            ))}
          </div>
        </div>
      </section>
    </main>
  );
}

function BookingWorkspace() {
  return (
    <main className="workspace-screen">
      <section className="screen-head">
        <div>
          <span>Booking</span>
          <h1>Брони · активные и онлайн-заявки</h1>
        </div>
        <div className="filter-row">
          <button type="button" className="active">Сегодня</button>
          <button type="button">12 часов</button>
          <button type="button">Создать</button>
        </div>
      </section>

      <section className="state-strip">
        <StateFlag label="Active bookings" value="5" />
        <StateFlag label="Online requests" value="2" critical />
        <StateFlag label="Strict mode" value="On" />
        <StateFlag label="Next arrival" value="15:40" />
      </section>

      <section className="smart-grid booking-layout">
        <div className="panel">
          <header className="panel-head">
            <strong>Лента бронирований</strong>
            <span>создание, перенос, отмена</span>
          </header>
          <div className="booking-list">
            {[
              ['15:40', 'Aziz P.', '2 PC · Зал C · 90 мин', 'Confirmed'],
              ['16:00', 'Guest +998', '1 PC · VIP · 60 мин', 'Online'],
              ['16:30', 'Team CS2', '5 PC · Bootcamp · 120 мин', 'Strict'],
              ['17:10', 'Madina S.', '1 PC · Зал B · 45 мин', 'Pending']
            ].map(([time, client, meta, status]) => (
              <button key={`${time}-${client}`} type="button" className="booking-card">
                <span>{time}</span>
                <strong>{client}</strong>
                <em>{meta}</em>
                <b>{status}</b>
              </button>
            ))}
          </div>
        </div>

        <div className="panel">
          <header className="panel-head">
            <strong>Новая бронь</strong>
            <span>минимальная форма оператора</span>
          </header>
          <div className="compact-form booking-form">
            <label>Клиент<input value="guest / phone optional" readOnly /></label>
            <label>Старт<input value="Сегодня · 16:00" readOnly /></label>
            <label>Время<input value="60 мин" readOnly /></label>
            <label>Количество ПК<input value="2" readOnly /></label>
          </div>
          <button type="button" className="primary-wide">Book</button>
        </div>
      </section>
    </main>
  );
}

function PosWorkspace({ currencyCode }: { currencyCode: string }) {
  const products = [
    ['Cola 0.5', `12 ${currencyCode}`, 'Drinks'],
    ['Water', `6 ${currencyCode}`, 'Drinks'],
    ['Hot dog', `28 ${currencyCode}`, 'Food'],
    ['Burger', `42 ${currencyCode}`, 'Food'],
    ['Guest pass 1h', `25 ${currencyCode}`, 'Pass'],
    ['VIP pass 1h', `45 ${currencyCode}`, 'Pass'],
    ['Headset rent', `10 ${currencyCode}`, 'Service'],
    ['Combo gamer', `55 ${currencyCode}`, 'Combo']
  ];

  return (
    <main className="workspace-screen">
      <section className="screen-head">
        <div>
          <span>Shop</span>
          <h1>POS · продажа товаров и услуг</h1>
        </div>
        <div className="filter-row">
          <button type="button" className="active">Продажа</button>
          <button type="button">Склад</button>
          <button type="button">Платежи</button>
        </div>
      </section>

      <section className="smart-grid pos-layout">
        <div className="panel">
          <header className="panel-head">
            <strong>Каталог</strong>
            <span>поиск по SKU / названию</span>
          </header>
          <label className="panel-search">
            <Search size={14} />
            <input placeholder="Товар, услуга, combo" />
          </label>
          <div className="catalog-grid">
            {products.map(([name, price, group]) => (
              <button key={name} type="button" className="catalog-item">
                <strong>{name}</strong>
                <span>{group}</span>
                <b>{price}</b>
              </button>
            ))}
          </div>
        </div>

        <div className="panel">
          <header className="panel-head">
            <strong>Корзина</strong>
            <span>SmartShell-like sale flow</span>
          </header>
          <div className="compact-form">
            <label>Клиент<input value="guest / phone optional" readOnly /></label>
            <label>Промокод<input value="-" readOnly /></label>
          </div>
          <table className="dark-table">
            <tbody>
              <tr><td>Cola 0.5</td><td>2</td><td>24 {currencyCode}</td></tr>
              <tr><td>Guest pass 1h</td><td>1</td><td>25 {currencyCode}</td></tr>
              <tr><td>Headset rent</td><td>1</td><td>10 {currencyCode}</td></tr>
            </tbody>
          </table>
          <div className="payment-row">
            <button type="button" className="active">Cash</button>
            <button type="button">Card</button>
            <button type="button">Deposit</button>
          </div>
          <div className="total-line"><span>Итого</span><strong>59 {currencyCode}</strong></div>
          <button type="button" className="primary-wide">Pay</button>
        </div>
      </section>
    </main>
  );
}

function PlayersWorkspace({ currencyCode }: { currencyCode: string }) {
  return (
    <main className="workspace-screen">
      <section className="screen-head">
        <div>
          <span>Clients</span>
          <h1>Игроки · база клиентов</h1>
        </div>
        <div className="filter-row">
          <button type="button" className="active">Клиенты</button>
          <button type="button">Группы</button>
          <button type="button">CSV</button>
        </div>
      </section>

      <section className="state-strip">
        <StateFlag label="Clients" value="12 480" />
        <StateFlag label="Deposit total" value={`84 210 ${currencyCode}`} />
        <StateFlag label="Debt" value={`1 240 ${currencyCode}`} critical />
        <StateFlag label="VIP" value="314" />
      </section>

      <section className="smart-grid players-layout">
        <div className="panel">
          <header className="panel-head">
            <strong>Список клиентов</strong>
            <span>поиск по нику или телефону</span>
          </header>
          <label className="panel-search">
            <Search size={14} />
            <input placeholder="nickname / phone" />
          </label>
          <table className="dark-table">
            <thead>
              <tr><th>Nickname</th><th>Status</th><th>Deposit</th><th>Discount</th><th>Last session</th></tr>
            </thead>
            <tbody>
              <tr><td>Amir K.</td><td>Active</td><td>120 {currencyCode}</td><td>5%</td><td>Today</td></tr>
              <tr><td>Madina S.</td><td>VIP</td><td>460 {currencyCode}</td><td>10%</td><td>Yesterday</td></tr>
              <tr><td>Yusuf A.</td><td>Normal</td><td>0 {currencyCode}</td><td>0%</td><td>May 18</td></tr>
              <tr><td>Olim K.</td><td>Debt</td><td>-35 {currencyCode}</td><td>0%</td><td>Now</td></tr>
            </tbody>
          </table>
        </div>
      </section>
    </main>
  );
}

function PaymentsWorkspace({ currencyCode }: { currencyCode: string }) {
  return (
    <main className="workspace-screen">
      <section className="screen-head">
        <div>
          <span>Payments</span>
          <h1>Платежи · касса текущей смены</h1>
        </div>
        <div className="filter-row">
          <button type="button" className="active">Текущая</button>
          <button type="button">Платежи</button>
          <button type="button">Отчёт</button>
        </div>
      </section>

      <section className="state-strip">
        <StateFlag label="Revenue" value={`4 820 ${currencyCode}`} />
        <StateFlag label="Cash" value={`2 310 ${currencyCode}`} />
        <StateFlag label="Card" value={`1 940 ${currencyCode}`} />
        <StateFlag label="Deposit" value={`570 ${currencyCode}`} />
      </section>

      <section className="smart-grid shift-layout">
        <div className="panel">
          <header className="panel-head">
            <strong>Платежи текущей смены</strong>
            <span>таблица операций</span>
          </header>
          <table className="dark-table">
            <thead>
              <tr><th>Time</th><th>Type</th><th>Client</th><th>Method</th><th>Total</th></tr>
            </thead>
            <tbody>
              <tr><td>14:22</td><td>Pass</td><td>Amir K.</td><td>Wallet</td><td>45 {currencyCode}</td></tr>
              <tr><td>14:30</td><td>Shop</td><td>Guest</td><td>Cash</td><td>59 {currencyCode}</td></tr>
              <tr><td>14:41</td><td>Deposit</td><td>Madina S.</td><td>Card</td><td>200 {currencyCode}</td></tr>
              <tr><td>14:55</td><td>Refund</td><td>Yusuf A.</td><td>Cash</td><td>-20 {currencyCode}</td></tr>
            </tbody>
          </table>
        </div>
      </section>
    </main>
  );
}

function LogsWorkspace() {
  return (
    <main className="workspace-screen">
      <section className="screen-head">
        <div>
          <span>Logs</span>
          <h1>Логи · события клуба</h1>
        </div>
        <div className="filter-row">
          <button type="button" className="active">Logs</button>
          <button type="button">Current shift</button>
          <button type="button">All events</button>
        </div>
      </section>

      <section className="smart-grid logs-layout">
        <div className="panel">
          <header className="panel-head">
            <strong>Журнал событий</strong>
            <span>универсальный поиск</span>
          </header>
          <label className="panel-search">
            <Search size={14} />
            <input placeholder="ПК, клиент, оператор, событие" />
          </label>
          <table className="dark-table">
            <tbody>
              <tr><td>15:01</td><td>PC-01 session extended</td><td>operator</td></tr>
              <tr><td>15:04</td><td>PC-23 heartbeat missed</td><td>agent</td></tr>
              <tr><td>15:06</td><td>Deposit replenished</td><td>cashier</td></tr>
              <tr><td>15:09</td><td>Settings read</td><td>technician</td></tr>
            </tbody>
          </table>
        </div>
      </section>
    </main>
  );
}

function OpsWorkspace() {
  return (
    <main className="workspace-screen">
      <section className="screen-head">
        <div>
          <span>Settings / Devices</span>
          <h1>Ops · настройки, устройства, обновления</h1>
        </div>
        <div className="filter-row">
          <button type="button" className="active">Settings</button>
          <button type="button">Devices</button>
          <button type="button">Updates</button>
        </div>
      </section>

      <section className="smart-grid ops-layout">
        <div className="panel settings-cards">
          {['Admin panel', 'Shell', 'Security', 'Customization', 'Integrations', 'Pilot Setup'].map((item) => (
            <button key={item} type="button" className="settings-card">
              <strong>{item}</strong>
              <span>configured · view</span>
            </button>
          ))}
        </div>
        <div className="panel">
          <header className="panel-head">
            <strong>Устройства и обновления</strong>
            <span>операторская диагностика</span>
          </header>
          <table className="dark-table">
            <tbody>
              <tr><td>Agent</td><td>23 online / 1 offline</td><td>Attention</td></tr>
              <tr><td>Shell</td><td>2 command pending</td><td>Review</td></tr>
              <tr><td>Rollout</td><td>0.1.14 internal</td><td>Installed</td></tr>
              <tr><td>Audit</td><td>cashier actions</td><td>Search</td></tr>
            </tbody>
          </table>
        </div>
      </section>
    </main>
  );
}

function MapSidePanel({ seat, currencyCode }: { seat: SeatSummary; currencyCode: string }) {
  return (
    <aside className="context-panel">
      <header className="context-head">
        <div>
          <span>{seat.zone}</span>
          <h2>{seat.name}</h2>
        </div>
        <span className={`state-chip state-${seat.tone}`}>{toneLabels[seat.tone]}</span>
      </header>

      <section className="context-section">
        <div className="session-timer">
          <Clock3 size={17} />
          <div>
            <span>Активная сессия</span>
            <strong>{seat.remaining}</strong>
          </div>
        </div>
        <div className="detail-row">
          <span>Игрок</span>
          <strong>{seat.player}</strong>
        </div>
        <div className="detail-row">
          <span>Биллинг</span>
          <strong>{seat.billing} · {currencyCode}</strong>
        </div>
      </section>

      <section className="action-grid" aria-label="Быстрые действия">
        <button type="button"><Plus size={15} />15 мин</button>
        <button type="button"><TimerReset size={15} />30 мин</button>
        <button type="button"><ArrowRightLeft size={15} />Перенос</button>
        <button type="button" className="danger"><Square size={15} />Стоп</button>
      </section>

      <section className="context-section">
        <div className="detail-row">
          <span>Устройство</span>
          <strong>{seat.device}</strong>
        </div>
        <div className="detail-row">
          <span>Команда</span>
          <strong>{seat.command}</strong>
        </div>
        <div className="detail-row">
          <span>Подтверждение</span>
          <strong>Backend required</strong>
        </div>
      </section>

      <section className="billing-mode" aria-label="Режим биллинга">
        <button type="button" className="active">Гость</button>
        <button type="button">Wallet</button>
        <button type="button">Package</button>
        <button type="button">Postpaid</button>
      </section>
    </aside>
  );
}

function SummarySidePanel({ workspace, currencyCode }: { workspace: WorkspaceId; currencyCode: string }) {
  const title = {
    map: 'PC-01',
    dashboard: 'Смена #24',
    booking: 'Бронь 16:00',
    pos: 'Корзина',
    players: 'Amir K.',
    payments: 'Платеж 14:30',
    logs: 'Log event',
    ops: 'Ops health'
  }[workspace];

  return (
    <aside className="context-panel">
      <header className="context-head">
        <div>
          <span>Details</span>
          <h2>{title}</h2>
        </div>
        <span className="state-chip state-active">Active</span>
      </header>
      <section className="context-section">
        <div className="detail-row"><span>Revenue</span><strong>4 820 {currencyCode}</strong></div>
        <div className="detail-row"><span>Pending</span><strong>2 actions</strong></div>
        <div className="detail-row"><span>Source</span><strong>SmartShell-like fixture</strong></div>
      </section>
      <button type="button" className="primary-wide">Open action</button>
    </aside>
  );
}

export function App() {
  const config = getOperatorConfig();
  const [workspace, setWorkspace] = useState<WorkspaceId>('map');
  const selectedSeat = seats[0];

  return (
    <div className="operator-shell">
      <header className="top-command" onMouseDown={handleWindowDragStart}>
        <div className="brand-block">
          <strong>AFK4</strong>
          <span>Operator</span>
        </div>
        <label className="command-search">
          <Search size={16} />
          <input placeholder="Игрок, ПК, команда" aria-label="Поиск" />
        </label>
        <div className="top-status">
          <span><Wifi size={14} />Realtime connected</span>
          <span>Смена #24 · открыта</span>
          <span>Dushanbe · {config.shellMode}</span>
        </div>
        <div className="window-controls" aria-label="Окно">
          <button type="button" title="Свернуть" aria-label="Свернуть" onClick={() => postHostWindowCommand('minimize')}>
            <Minus size={15} />
          </button>
          <button type="button" title="Развернуть" aria-label="Развернуть" onClick={() => postHostWindowCommand('maximize')}>
            <Maximize2 size={13} />
          </button>
          <button type="button" title="Закрыть" aria-label="Закрыть" onClick={() => postHostWindowCommand('close')}>
            <X size={15} />
          </button>
        </div>
      </header>

      <nav className="workspace-rail" aria-label="Рабочие места">
        {navItems.map((item, index) => {
          const Icon = item.icon;
          const id = workspaceIds[index];
          return (
            <button
              key={item.label}
              type="button"
              className={workspace === id ? 'active' : ''}
              title={item.label}
              onClick={() => setWorkspace(id)}
            >
              <Icon size={22} />
              <span>{item.label}</span>
            </button>
          );
        })}
      </nav>

      {workspace === 'map' && <MapWorkspace currencyCode={config.currencyCode} />}
      {workspace === 'dashboard' && <DashboardWorkspace currencyCode={config.currencyCode} />}
      {workspace === 'booking' && <BookingWorkspace />}
      {workspace === 'pos' && <PosWorkspace currencyCode={config.currencyCode} />}
      {workspace === 'players' && <PlayersWorkspace currencyCode={config.currencyCode} />}
      {workspace === 'payments' && <PaymentsWorkspace currencyCode={config.currencyCode} />}
      {workspace === 'logs' && <LogsWorkspace />}
      {workspace === 'ops' && <OpsWorkspace />}

      {workspace === 'map'
        ? <MapSidePanel seat={selectedSeat} currencyCode={config.currencyCode} />
        : <SummarySidePanel workspace={workspace} currencyCode={config.currencyCode} />}

      <footer className="signals-strip">
        {signals.map((signal) => {
          const Icon = signal.icon;
          return (
            <span key={signal.label}>
              <Icon size={14} />
              {signal.label}
            </span>
          );
        })}
        <span><LockKeyhole size={14} />Critical actions wait for backend confirmation</span>
      </footer>
    </div>
  );
}
