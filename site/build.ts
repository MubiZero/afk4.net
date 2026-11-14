// Сборка публичного сайта afk4.net (docs/operations/public-site.md): четыре страницы на трёх языках из
// content.ts и home.ts в site/dist. Без фреймворка и зависимостей: сайт должен открываться на плохом канале клуба.
//   bun site/build.ts
import { cpSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { content, links, type Content, type Lang } from './content';
import { contacts, home as homeContent, type Group, type Home } from './home';

const root = import.meta.dir;
const dist = join(root, 'dist');
const langs: Lang[] = ['ru', 'tg', 'en'];
type Page = 'index' | 'download' | 'code-signing' | 'privacy';
const pages: Page[] = ['index', 'download', 'code-signing', 'privacy'];

const escape = (text: string) =>
  text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

const prefix = (lang: Lang) => (lang === 'ru' ? '/' : `/${lang}/`);
const href = (lang: Lang, page: Page) => `${prefix(lang)}${page === 'index' ? '' : `${page}.html`}`;
const homeAnchor = (lang: Lang, anchor: string) => `${href(lang, 'index')}#${anchor}`;

// Лёгкая версия: при слабой связи, экономии трафика и просьбе системы «меньше движения» страница не двигается.
// Скрипт короткий и стоит в начале, чтобы класс появился до отрисовки. Внешний файл: CSP не пускает inline.
const scripts = '<script src="/lite.js"></script>\n  <script src="/app.js" defer></script>';

function layout(lang: Lang, page: Page, title: string, description: string, main: string): string {
  const c = content[lang];
  const h = homeContent[lang];
  const nav = (target: Page, label: string) =>
    `<a href="${href(lang, target)}"${target === page ? ' aria-current="page"' : ''}>${escape(label)}</a>`;
  const switcher = langs
    .map((other) => `<a href="${href(other, page)}" hreflang="${content[other].htmlLang}" lang="${content[other].htmlLang}"${other === lang ? ' aria-current="true"' : ''}>${content[other].langName}</a>`)
    .join('');
  const alternates = langs
    .map((other) => `<link rel="alternate" hreflang="${content[other].htmlLang}" href="https://afk4.net${href(other, page)}">`)
    .join('\n  ');
  return `<!doctype html>
<html lang="${c.htmlLang}">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>${escape(title)}</title>
  <meta name="description" content="${escape(description)}">
  <meta name="color-scheme" content="light">
  <meta name="theme-color" content="#f4f5f0">
  <meta property="og:title" content="${escape(title)}">
  <meta property="og:description" content="${escape(description)}">
  <meta property="og:image" content="https://afk4.net/img/panel.jpg">
  ${alternates}
  <link rel="icon" href="/img/favicon.svg" type="image/svg+xml">
  <link rel="preload" href="/fonts/geologica-cyrillic.woff2" as="font" type="font/woff2" crossorigin>
  <link rel="stylesheet" href="/styles.css">
  ${scripts}
</head>
<body>
  <a class="skip" href="#main">${lang === 'en' ? 'Skip to content' : lang === 'tg' ? 'Ба мундариҷа' : 'К содержанию'}</a>
  <header class="top">
    <div class="wrap top-row">
      <a class="brand" href="${href(lang, 'index')}"><img src="/img/logo-light.svg" alt="AFK4.NET" width="150" height="34"></a>
      <nav class="nav" aria-label="${escape(c.nav.home)}">
        <a href="${homeAnchor(lang, 'inside')}" class="nav-extra">${escape(c.nav.features)}</a>
        <a href="${href(lang, 'download')}" class="nav-download"${page === 'download' ? ' aria-current="page"' : ''}>${escape(c.nav.download)}</a>
        <a href="${links.demo}" class="nav-extra">${escape(c.nav.demo)}</a>
        <a class="btn btn-lamp btn-small" href="${homeAnchor(lang, 'contact')}">${escape(h.hero.write)}</a>
      </nav>
      <nav class="langs" aria-label="Language">${switcher}</nav>
    </div>
  </header>
  <!--email_off--><!-- Cloudflare иначе прячет адрес за скриптом: без JS видно «[email protected]». -->
  <main id="main">
${main}
  </main>
  <footer class="foot">
    <div class="wrap foot-row">
      <div>
        <img src="/img/logo-light.svg" alt="AFK4.NET" width="120" height="27">
        <p>${escape(c.footer.tagline)}</p>
      </div>
      <nav class="foot-links" aria-label="${escape(c.footer.contact)}">
        ${nav('download', c.nav.download)}
        ${nav('code-signing', c.nav.signing)}
        ${nav('privacy', c.nav.privacy)}
        <a href="${links.repo}">${escape(c.footer.license)}</a>
      </nav>
      <nav class="foot-links" aria-label="${escape(h.contact.channels)}">
        <a href="${contacts.telegram}">Telegram ${contacts.telegramHandle}</a>
        <a href="${contacts.phone}">${contacts.phoneShown}</a>
        <a href="${links.email}">${links.email.replace('mailto:', '')}</a>
      </nav>
      <!--/email_off-->
    </div>
  </footer>
</body>
</html>
`;
}

// ---- Главная: доска из живых плиток --------------------------------------------------------------------------

interface Seat {
  n: string;
  state: 'play' | 'free' | 'offline';
  seconds?: number;
}

const seats: Seat[] = [
  { n: '01', state: 'play', seconds: 2592 },
  { n: '02', state: 'free' },
  { n: '03', state: 'play', seconds: 1214 },
  { n: '04', state: 'play', seconds: 5410 },
  { n: '05', state: 'play', seconds: 421 },
  { n: '06', state: 'free' },
  { n: '07', state: 'play', seconds: 3300 },
  { n: '08', state: 'offline' },
  { n: '09', state: 'play', seconds: 905 },
  { n: '10', state: 'play', seconds: 4120 },
  { n: '11', state: 'free' },
  { n: '12', state: 'play', seconds: 1750 }
];

const clock = (total: number) => {
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  const two = (value: number) => String(value).padStart(2, '0');
  return h > 0 ? `${h}:${two(m)}:${two(s)}` : `${two(m)}:${two(s)}`;
};

const chip = (kind: 'have' | 'planned', label: string) => `<span class="chip chip-${kind}">${escape(label)}</span>`;

function wifiIcon(): string {
  return '<svg class="wifi" viewBox="0 0 24 24" width="22" height="22" aria-hidden="true" focusable="false"><path d="M2.5 9a14 14 0 0 1 19 0M5.5 12.5a9.5 9.5 0 0 1 13 0M8.6 16a5 5 0 0 1 6.8 0" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"/><circle cx="12" cy="19.5" r="1.4" fill="currentColor"/><path class="wifi-cut" d="M4 4l16 16" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round"/></svg>';
}

function board(h: Home): string {
  const b = h.board;
  const seat = (item: Seat) => {
    if (item.state === 'play') {
      return `<li><button class="seat seat-play" type="button" data-seat="${escape(b.map.pc)} ${item.n}" aria-pressed="false"><span class="seat-name">${escape(b.map.pc)} ${item.n}</span><span class="seat-time" data-seconds="${item.seconds}">${clock(item.seconds ?? 0)}</span><span class="seat-sub">${escape(b.map.left)}</span></button></li>`;
    }
    if (item.state === 'free') {
      return `<li><button class="seat seat-free" type="button" data-seat="${escape(b.map.pc)} ${item.n}" aria-pressed="false"><span class="seat-name">${escape(b.map.pc)} ${item.n}</span><span class="seat-time seat-quiet">${escape(b.map.free)}</span></button></li>`;
    }
    return `<li><div class="seat seat-offline"><span class="seat-name">${escape(b.map.pc)} ${item.n}</span><span class="seat-time seat-alert">${escape(b.map.offline)}</span></div></li>`;
  };
  const miniSeats = seats
    .filter((item) => item.state === 'play')
    .slice(0, 4)
    .map((item) => `<li class="mini"><span>${escape(b.map.pc)} ${item.n}</span><span class="seat-time" data-seconds="${item.seconds}">${clock(item.seconds ?? 0)}</span></li>`)
    .join('');
  const cachePcs = seats
    .slice(0, 6)
    .map((item) => `<li>${escape(b.map.pc)} ${item.n}</li>`)
    .join('');
  const langRow = (code: 'tg' | 'ru' | 'en', name: string) =>
    `<li class="lang-row" data-lang-row="${code}"><span class="lang-name">${name}</span><span class="lang-phrase" lang="${code}">${escape(b.langs.phrases[code])}</span></li>`;
  return `    <section class="wrap board-wrap" aria-labelledby="board-title">
      <h2 class="board-label" id="board-title">${escape(b.label)} <span class="synthetic">${escape(b.synthetic)}</span></h2>
      <div class="board">
        <article class="tile tile-map">
          <h3>${escape(b.map.title)}</h3>
          <p>${escape(b.map.text)}</p>
          <ul class="seats">${seats.map(seat).join('')}</ul>
          <div class="seat-panel" data-seat-panel data-sent="${escape(b.map.sent)}" hidden>
            <span class="seat-panel-name" data-seat-name></span>
            <button class="act" type="button" data-act="${escape(b.map.actions.lock)}">${escape(b.map.actions.lock)}</button>
            <button class="act" type="button" data-act="${escape(b.map.actions.unlock)}">${escape(b.map.actions.unlock)}</button>
            <button class="act" type="button" data-act="${escape(b.map.actions.message)}">${escape(b.map.actions.message)}</button>
          </div>
          <p class="seat-hint" role="status" aria-live="polite" data-seat-hint>${escape(b.map.hint)}</p>
          ${chip('have', b.have)}
        </article>
        <article class="tile tile-guest tile-mint">
          <h3>${escape(b.guest.title)}</h3>
          <p>${escape(b.guest.text)}</p>
          <figure class="screen"><img src="/img/shell.jpg" alt="${escape(b.guest.imageAlt)}" width="1280" height="761" loading="lazy"></figure>
          ${chip('have', b.have)}
        </article>
        <article class="tile tile-till">
          <h3>${escape(b.till.title)}</h3>
          <p>${escape(b.till.text)}</p>
          <ul class="receipt" aria-hidden="true">
${b.till.lines.map((line) => `            <li><span>${escape(line.name)}</span><span>${escape(line.price)} ${escape(b.till.currency)}</span></li>`).join('\n')}
            <li class="receipt-total"><span>${escape(b.till.total)}</span><span>${b.till.lines.reduce((sum, line) => sum + Number(line.price), 0)} ${escape(b.till.currency)}</span></li>
          </ul>
          ${chip('have', b.have)}
        </article>
        <article class="tile tile-offline tile-lamp" data-offline>
          <h3>${escape(b.offline.title)}</h3>
          <p>${escape(b.offline.text)}</p>
          <div class="offline-panel">
            <button class="btn btn-ink" type="button" data-offline-toggle data-label-off="${escape(b.offline.button)}" data-label-on="${escape(b.offline.buttonBack)}" aria-pressed="false">${wifiIcon()}<span data-offline-label>${escape(b.offline.button)}</span></button>
            <p class="offline-state" role="status" aria-live="polite" data-offline-state data-text-on="${escape(b.offline.on)}" data-text-off="${escape(b.offline.off)}">${escape(b.offline.on)}</p>
            <ul class="minis" aria-hidden="true">${miniSeats}</ul>
            <p class="offline-cash" aria-hidden="true">${escape(b.offline.counting)} · ${escape(b.offline.cashOnly)}</p>
          </div>
          ${chip('have', b.have)}
        </article>
        <article class="tile tile-telegram">
          <h3>${escape(b.telegram.title)}</h3>
          <p>${escape(b.telegram.text)}</p>
          <ol class="chat" aria-hidden="true">
${b.telegram.messages.map((message) => `            <li>${escape(message)}</li>`).join('\n')}
          </ol>
          ${chip('have', b.have)}
        </article>
        <article class="tile tile-app">
          <h3>${escape(b.app.title)}</h3>
          <p>${escape(b.app.text)}</p>
          <div class="phone" aria-hidden="true"><span class="phone-line">${escape(b.app.booking)}</span><span class="phone-btn">${escape(b.app.topUp)}</span></div>
          ${chip('planned', b.planned)}
        </article>
        <article class="tile tile-cache">
          <h3>${escape(b.cache.title)}</h3>
          <p>${escape(b.cache.text)}</p>
          <div class="cache" aria-hidden="true">
            <span class="cache-label">${escape(b.cache.done)}</span>
            <span class="cache-bar"><i></i></span>
            <span class="cache-label">${escape(b.cache.pcs)}</span>
            <ul class="cache-pcs">${cachePcs}</ul>
          </div>
          ${chip('planned', b.planned)}
        </article>
        <article class="tile tile-langs">
          <h3>${escape(b.langs.title)}</h3>
          <p>${escape(b.langs.text)}</p>
          <ul class="lang-rows">${langRow('tg', 'Тоҷикӣ')}${langRow('ru', 'Русский')}${langRow('en', 'English')}</ul>
          ${chip('have', b.have)}
        </article>
        <article class="tile tile-soon">
          <h3>${escape(b.soon.title)}</h3>
          <p>${escape(b.soon.text)}</p>
          <ul class="soon">${b.soon.items.map((item) => `<li>${escape(item)}</li>`).join('')}</ul>
          ${chip('planned', b.planned)}
        </article>
      </div>
    </section>`;
}

function groups(h: Home): string {
  const render = (group: Group) =>
    `        <details class="group group-${group.status}" open>
          <summary><span class="group-title">${escape(group.title)}</span>${chip(group.status, group.status === 'have' ? h.board.have : h.board.planned)}</summary>
          <ul>
${group.items.map((item) => `            <li>${escape(item)}</li>`).join('\n')}
          </ul>
        </details>`;
  return h.inside.groups.map(render).join('\n');
}

function homePage(lang: Lang, c: Content): string {
  const h = homeContent[lang];
  const k = h.contact;
  return `    <section class="wrap hero">
      <h1>${escape(h.hero.title)}</h1>
      <div class="hero-row">
        <div class="hero-text">
          <p class="lead">${escape(h.hero.lead)}</p>
          <p class="name-line">${escape(h.hero.nameLine)}</p>
        </div>
        <div class="hero-cta">
          <div class="actions">
            <a class="btn btn-lamp" href="#contact">${escape(h.hero.write)}</a>
            <a class="btn" href="${links.demo}">${escape(h.hero.demo)}</a>
          </div>
          <p class="note">${escape(h.hero.demoNote)}</p>
          <ul class="trust">
            <li><a href="${links.repo}">${escape(h.hero.trust[0])}</a></li>
            <li><a href="${href(lang, 'download')}#changes">${escape(h.hero.trust[1])}</a></li>
          </ul>
        </div>
      </div>
    </section>
${board(h)}
    <section class="section" id="inside">
      <div class="wrap">
        <h2>${escape(h.inside.title)}</h2>
        <p class="lead">${escape(h.inside.lead)}</p>
        <div class="groups">
${groups(h)}
        </div>
      </div>
    </section>
    <section class="section section-tint">
      <div class="wrap how">
        <div>
          <h2>${escape(h.how.title)}</h2>
          <ol class="steps">
${h.how.steps.map((step) => `            <li><h3>${escape(step.title)}</h3><p>${escape(step.body)}</p></li>`).join('\n')}
          </ol>
          <div class="actions"><a class="btn btn-ink" href="${href(lang, 'download')}">${escape(h.how.cta)}</a></div>
        </div>
        <div class="open">
          <h2>${escape(h.open.title)}</h2>
          <p class="lead">${escape(h.open.body)}</p>
          <p><a class="link" href="${links.repo}">${escape(h.open.link)}</a> · <a class="link" href="${href(lang, 'code-signing')}">${escape(c.nav.signing)}</a></p>
        </div>
      </div>
    </section>
    <section class="section" id="contact">
      <div class="wrap contact">
        <div class="contact-copy">
          <h2>${escape(k.title)}</h2>
          <p class="lead">${escape(k.lead)}</p>
          <p class="channels-label">${escape(k.channels)}</p>
          <ul class="channels">
            <li><a class="btn btn-lamp" href="${contacts.telegram}">${escape(k.telegram)} ${contacts.telegramHandle}</a></li>
            <li><a class="btn" href="${contacts.phone}">${escape(k.call)} ${contacts.phoneShown}</a></li>
            <li><a class="btn btn-quiet" href="${links.email}">${escape(k.email)} ${links.email.replace('mailto:', '')}</a><span class="note">${escape(k.emailNote)}</span></li>
          </ul>
        </div>
        <form class="form" action="/api/contact" method="post" data-contact data-sending="${escape(k.sending)}" data-sent="${escape(k.sent)}" data-error="${escape(k.error)}">
          <label>${escape(k.name)}<input name="name" autocomplete="name" required maxlength="80"></label>
          <label>${escape(k.club)}<input name="club" maxlength="120"></label>
          <label>${escape(k.phone)}<input name="contact" autocomplete="tel" required maxlength="80"></label>
          <label>${escape(k.message)}<textarea name="message" rows="4" maxlength="1000"></textarea></label>
          <label class="trap" aria-hidden="true">Website<input name="website" tabindex="-1" autocomplete="off"></label>
          <input type="hidden" name="lang" value="${lang}">
          <button class="btn btn-lamp" type="submit">${escape(k.send)}</button>
          <p class="form-state" role="status" aria-live="polite" data-form-state></p>
          <p class="form-state is-ok flash" id="sent">${escape(k.sent)}</p>
          <p class="form-state is-error flash" id="error">${escape(k.error)}</p>
          <p class="note">${escape(k.consent)} <a class="link" href="${href(lang, 'privacy')}">${escape(c.nav.privacy)}</a></p>
        </form>
      </div>
    </section>`;
}

// ---- Остальные страницы ---------------------------------------------------------------------------------------

function download(lang: Lang, c: Content): string {
  const d = c.download;
  return `    <section class="section">
      <div class="wrap narrow">
        <h1 class="page-title">${escape(d.title)}</h1>
        <p class="lead">${escape(d.lead)}</p>
        <div class="actions">
          <a class="btn btn-lamp" href="${links.releases}">${escape(d.button)}</a>
        </div>
        <p class="note">${escape(d.buttonNote)}</p>
        <h2>${escape(d.requirementsTitle)}</h2>
        <ul class="plain">
${d.requirements.map((item) => `          <li>${escape(item)}</li>`).join('\n')}
        </ul>
        <h2 id="changes">${escape(d.changesTitle)}</h2>
        <p>${escape(d.changesLead)}</p>
        <dl class="changes">
${d.changes.map((item) => `          <dt>${escape(item.title)}</dt><dd>${escape(item.body)}</dd>`).join('\n')}
        </dl>
        <h2>${escape(d.dataTitle)}</h2>
        <p>${escape(d.dataBody)} <a class="link" href="${href(lang, 'privacy')}">${escape(c.nav.privacy)}</a></p>
        <h2 id="uninstall">${escape(d.uninstallTitle)}</h2>
        <ol class="plain">
${d.uninstallSteps.map((item) => `          <li>${escape(item)}</li>`).join('\n')}
        </ol>
        <p class="note">${escape(d.uninstallLeftovers)}</p>
        <p><a class="link" href="${href(lang, 'code-signing')}">${escape(c.nav.signing)}</a></p>
      </div>
    </section>`;
}

function signing(lang: Lang, c: Content): string {
  const s = c.signing;
  return `    <section class="section">
      <div class="wrap narrow">
        <h1 class="page-title">${escape(s.title)}</h1>
        <p class="lead">${escape(s.lead)}</p>
        <p class="callout">Free code signing provided by <a class="link" href="${links.signpath}">SignPath.io</a>, certificate by <a class="link" href="${links.signpathFoundation}">SignPath Foundation</a>.</p>
        <h2>${escape(s.whatTitle)}</h2>
        <p>${escape(s.whatBody)}</p>
        <h2>${escape(s.rolesTitle)}</h2>
        <dl class="changes">
          <dt>${escape(s.roles[0].title)}</dt><dd>${escape(s.roles[0].body)} <a class="link" href="${links.members}">${links.members.replace('https://', '')}</a></dd>
          <dt>${escape(s.roles[1].title)}</dt><dd>${escape(s.roles[1].body)} <a class="link" href="${links.owner}">${links.owner.replace('https://', '')}</a></dd>
        </dl>
        <h2>${escape(s.processTitle)}</h2>
        <ol class="plain">
${s.process.map((item) => `          <li>${escape(item)}</li>`).join('\n')}
        </ol>
        <h2>${escape(s.privacyTitle)}</h2>
        <p>${escape(s.privacyBody)} <a class="link" href="${href(lang, 'privacy')}">${escape(c.nav.privacy)}</a></p>
      </div>
    </section>`;
}

function privacy(_lang: Lang, c: Content): string {
  const p = c.privacy;
  return `    <section class="section">
      <div class="wrap narrow">
        <h1 class="page-title">${escape(p.title)}</h1>
        <p class="note">${escape(p.updated)}</p>
${p.sections.map((section) => `        <h2>${escape(section.title)}</h2>\n        <p>${escape(section.body)}</p>`).join('\n')}
      </div>
    </section>`;
}

rmSync(dist, { recursive: true, force: true });
for (const lang of langs) {
  const c = content[lang];
  const dir = lang === 'ru' ? dist : join(dist, lang);
  mkdirSync(dir, { recursive: true });
  const render: Record<Page, () => [string, string]> = {
    index: () => [c.meta.title, homePage(lang, c)],
    download: () => [`${c.download.title} · AFK4.NET`, download(lang, c)],
    'code-signing': () => [`${c.signing.title} · AFK4.NET`, signing(lang, c)],
    privacy: () => [`${c.privacy.title} · AFK4.NET`, privacy(lang, c)]
  };
  for (const page of pages) {
    const [title, main] = render[page]();
    writeFileSync(join(dir, `${page}.html`), layout(lang, page, title, c.meta.description, main));
  }
}
cpSync(join(root, 'img'), join(dist, 'img'), { recursive: true });
cpSync(join(root, 'fonts'), join(dist, 'fonts'), { recursive: true });
for (const file of ['styles.css', 'app.js', 'lite.js']) cpSync(join(root, file), join(dist, file));
writeFileSync(
  join(dist, '_headers'),
  "/*\n  X-Content-Type-Options: nosniff\n  Referrer-Policy: strict-origin-when-cross-origin\n  Content-Security-Policy: default-src 'self'; img-src 'self'; style-src 'self'; script-src 'self'; connect-src 'self'; form-action 'self'; base-uri 'self'; frame-ancestors 'none'\n/fonts/*\n  Cache-Control: public, max-age=31536000, immutable\n"
);
console.log(`site built: ${langs.length} languages × ${pages.length} pages → ${dist}`);
