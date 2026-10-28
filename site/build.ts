// Сборка публичного сайта afk4.net (docs/operations/public-site.md): четыре страницы на трёх языках из
// content.ts в site/dist. Без фреймворка и зависимостей: сайт должен открываться на плохом канале клуба.
//   bun site/build.ts
import { cpSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { content, links, type Content, type Lang } from './content';

const root = import.meta.dir;
const dist = join(root, 'dist');
const langs: Lang[] = ['ru', 'tg', 'en'];
type Page = 'index' | 'download' | 'code-signing' | 'privacy';
const pages: Page[] = ['index', 'download', 'code-signing', 'privacy'];

const escape = (text: string) =>
  text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

const prefix = (lang: Lang) => (lang === 'ru' ? '/' : `/${lang}/`);
const href = (lang: Lang, page: Page) => `${prefix(lang)}${page === 'index' ? '' : `${page}.html`}`;

function layout(lang: Lang, page: Page, title: string, description: string, main: string): string {
  const c = content[lang];
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
  <meta name="color-scheme" content="dark">
  <meta property="og:title" content="${escape(title)}">
  <meta property="og:description" content="${escape(description)}">
  <meta property="og:image" content="https://afk4.net/img/panel.jpg">
  ${alternates}
  <link rel="icon" href="/img/favicon.svg" type="image/svg+xml">
  <link rel="stylesheet" href="/styles.css">
</head>
<body>
  <a class="skip" href="#main">${lang === 'en' ? 'Skip to content' : lang === 'tg' ? 'Ба мундариҷа' : 'К содержанию'}</a>
  <header class="top">
    <div class="wrap top-row">
      <a class="brand" href="${href(lang, 'index')}"><img src="/img/logo.svg" alt="AFK4.NET" width="150" height="34"></a>
      <nav class="nav" aria-label="${escape(c.nav.home)}">
        ${nav('download', c.nav.download)}
        <a href="${links.demo}">${escape(c.nav.demo)}</a>
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
        <img src="/img/logo.svg" alt="AFK4.NET" width="120" height="27">
        <p>${escape(c.footer.tagline)}</p>
      </div>
      <nav class="foot-links" aria-label="${escape(c.footer.contact)}">
        ${nav('download', c.nav.download)}
        ${nav('code-signing', c.nav.signing)}
        ${nav('privacy', c.nav.privacy)}
        <a href="${links.repo}">${escape(c.footer.license)}</a>
        <a href="${links.email}">${escape(c.footer.contact)}: ${links.email.replace('mailto:', '')}</a>
      </nav>
      <!--/email_off-->
    </div>
  </footer>
</body>
</html>
`;
}

function home(lang: Lang, c: Content): string {
  return `    <section class="hero">
      <div class="wrap hero-grid">
        <div class="hero-copy">
          <p class="kicker">${escape(c.hero.kicker)}</p>
          <h1>${escape(c.hero.title)}</h1>
          <p class="lead">${escape(c.hero.lead)}</p>
          <div class="actions">
            <a class="btn btn-primary" href="${href(lang, 'download')}">${escape(c.hero.download)}</a>
            <a class="btn" href="${links.demo}">${escape(c.hero.demo)}</a>
          </div>
          <p class="note">${escape(c.hero.note)}</p>
        </div>
        <figure class="shot shot-hero">
          <img src="/img/panel.jpg" alt="${escape(c.hero.imageAlt)}" width="1280" height="764">
        </figure>
      </div>
    </section>
    <section class="section">
      <div class="wrap">
        <h2>${escape(c.features.title)}</h2>
        <ul class="features">
${c.features.items.map((item) => `          <li><h3>${escape(item.title)}</h3><p>${escape(item.body)}</p></li>`).join('\n')}
        </ul>
      </div>
    </section>
    <section class="section section-alt">
      <div class="wrap split">
        <figure class="shot">
          <img src="/img/shell.jpg" alt="${escape(c.player.imageAlt)}" width="1280" height="761" loading="lazy">
        </figure>
        <div>
          <h2>${escape(c.player.title)}</h2>
          <p class="lead">${escape(c.player.body)}</p>
        </div>
      </div>
    </section>
    <section class="section">
      <div class="wrap">
        <h2>${escape(c.start.title)}</h2>
        <ol class="steps">
${c.start.steps.map((step) => `          <li><h3>${escape(step.title)}</h3><p>${escape(step.body)}</p></li>`).join('\n')}
        </ol>
        <div class="actions">
          <a class="btn btn-primary" href="${href(lang, 'download')}">${escape(c.start.cta)}</a>
        </div>
      </div>
    </section>
    <section class="section section-alt">
      <div class="wrap narrow">
        <h2>${escape(c.open.title)}</h2>
        <p class="lead">${escape(c.open.body)}</p>
        <p><a class="link" href="${links.repo}">${escape(c.open.link)}</a> · <a class="link" href="${href(lang, 'code-signing')}">${escape(c.nav.signing)}</a></p>
      </div>
    </section>`;
}

function download(lang: Lang, c: Content): string {
  const d = c.download;
  return `    <section class="section">
      <div class="wrap narrow">
        <h1>${escape(d.title)}</h1>
        <p class="lead">${escape(d.lead)}</p>
        <div class="actions">
          <a class="btn btn-primary" href="${links.releases}">${escape(d.button)}</a>
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
        <h1>${escape(s.title)}</h1>
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
        <h1>${escape(p.title)}</h1>
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
    index: () => [c.meta.title, home(lang, c)],
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
cpSync(join(root, 'styles.css'), join(dist, 'styles.css'));
writeFileSync(join(dist, '_headers'), '/*\n  X-Content-Type-Options: nosniff\n  Referrer-Policy: strict-origin-when-cross-origin\n  Content-Security-Policy: default-src \'self\'; img-src \'self\'; style-src \'self\'; frame-ancestors \'none\'\n');
console.log(`site built: ${langs.length} languages × ${pages.length} pages → ${dist}`);
