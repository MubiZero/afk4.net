// Форма связи на главной (docs/operations/public-site.md): сообщение уходит владельцу в Telegram через бота.
// Cloudflare Pages Function; секреты задаются в проекте Pages, в код не попадают:
//   TELEGRAM_BOT_TOKEN — токен бота, TELEGRAM_CHAT_ID — чат владельца.
// Принимает JSON (скрипт страницы) и обычный POST формы (без JavaScript, ответ — переход назад).

interface Env {
  TELEGRAM_BOT_TOKEN?: string;
  TELEGRAM_CHAT_ID?: string;
}

interface Context {
  request: Request;
  env: Env;
}

const clip = (value: unknown, max: number) => (typeof value === 'string' ? value.trim().slice(0, max) : '');

const json = (status: number, body: Record<string, unknown>) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' } });

export const onRequestPost = async ({ request, env }: Context): Promise<Response> => {
  // Только со своего сайта: чужая страница не должна слать сообщения от нашего имени.
  const origin = request.headers.get('Origin');
  if (origin && new URL(origin).host !== new URL(request.url).host) return json(403, { error: 'origin' });

  const wantsJson = (request.headers.get('Content-Type') ?? '').includes('application/json');
  let fields: Record<string, unknown>;
  try {
    fields = wantsJson ? await request.json() : Object.fromEntries(await request.formData());
  } catch {
    return json(400, { error: 'body' });
  }

  const back = (ok: boolean) =>
    wantsJson ? json(ok ? 200 : 502, { ok }) : new Response(null, { status: 303, headers: { Location: `/#${ok ? 'sent' : 'error'}` } });

  // Ловушка для ботов: человек это поле не видит и не заполняет.
  if (clip(fields.website, 200)) return back(true);

  const name = clip(fields.name, 80);
  const contact = clip(fields.contact, 80);
  const club = clip(fields.club, 120);
  const message = clip(fields.message, 1000);
  const lang = clip(fields.lang, 4);
  if (!name || !contact) return json(400, { error: 'required' });

  if (!env.TELEGRAM_BOT_TOKEN || !env.TELEGRAM_CHAT_ID) return json(503, { error: 'not-configured' });

  const text = [
    'Сообщение с сайта afk4.net',
    `Имя: ${name}`,
    `Контакт: ${contact}`,
    club ? `Клуб: ${club}` : '',
    message ? `Сообщение: ${message}` : '',
    lang ? `Язык страницы: ${lang}` : ''
  ]
    .filter(Boolean)
    .join('\n');

  const sent = await fetch(`https://api.telegram.org/bot${env.TELEGRAM_BOT_TOKEN}/sendMessage`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ chat_id: env.TELEGRAM_CHAT_ID, text, disable_web_page_preview: true })
  }).catch(() => null);

  return back(Boolean(sent?.ok));
};
