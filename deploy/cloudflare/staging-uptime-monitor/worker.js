// AFK4 staging uptime monitor.
//
// Cron-triggered Cloudflare Worker. Probes four staging endpoints, tracks
// consecutive failures per monitor in a Workers KV namespace, and sends
// Telegram alerts only on state change (first DOWN + RECOVERED). All other
// runs are silent to keep alert noise low while incidents drag on.
//
// Endpoint list is intentionally inlined in the source so the monitor
// configuration sits with the script that uses it. Editing requires a
// redeploy via scripts/deploy-staging-uptime-monitor.sh.

const FAIL_CONSEC_THRESHOLD = 2;
const PROBE_TIMEOUT_MS = 30_000;
const KV_STATE_TTL_SECONDS = 86_400 * 7;

const MONITORS = [
  {
    name: 'platform-api-health',
    url: 'https://afk4.staging.mubi.dev/api/health',
    method: 'GET',
    expectStatus: 200,
    expectBody: '"status":"ok"',
  },
  {
    name: 'platform-api-auth-401',
    url: 'https://afk4.staging.mubi.dev/api/platform/auth/sign-in',
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: '{}',
    expectStatus: 401,
  },
  {
    name: 'control-plane-spa-healthz',
    url: 'https://platform.afk4.staging.mubi.dev/healthz',
    method: 'GET',
    expectStatus: 200,
    expectBody: 'ok',
  },
  {
    name: 'control-plane-spa-shell',
    url: 'https://platform.afk4.staging.mubi.dev/',
    method: 'GET',
    expectStatus: 200,
    expectBody: 'AFK4 Platform Control Plane',
  },
];

async function probe(monitor) {
  const init = {
    method: monitor.method,
    headers: monitor.headers,
    body: monitor.body,
    signal: AbortSignal.timeout(PROBE_TIMEOUT_MS),
    redirect: 'manual',
    cf: { cacheTtl: 0, cacheEverything: false },
  };

  const start = Date.now();
  try {
    const res = await fetch(monitor.url, init);
    const text = await res.text();
    const elapsedMs = Date.now() - start;

    if (res.status !== monitor.expectStatus) {
      return { ok: false, elapsedMs, reason: `status ${res.status} (expected ${monitor.expectStatus})` };
    }
    if (monitor.expectBody && !text.includes(monitor.expectBody)) {
      return { ok: false, elapsedMs, reason: `body missing keyword "${monitor.expectBody}"` };
    }
    return { ok: true, elapsedMs };
  } catch (err) {
    return { ok: false, elapsedMs: Date.now() - start, reason: `fetch error: ${err && err.message ? err.message : String(err)}` };
  }
}

function escapeHtml(value) {
  return String(value).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

async function sendTelegram(env, text) {
  if (!env.TELEGRAM_BOT_TOKEN || !env.TELEGRAM_CHAT_ID) {
    console.warn('TELEGRAM_BOT_TOKEN / TELEGRAM_CHAT_ID not set; suppressing alert:', text);
    return;
  }

  const res = await fetch(`https://api.telegram.org/bot${env.TELEGRAM_BOT_TOKEN}/sendMessage`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      chat_id: env.TELEGRAM_CHAT_ID,
      text,
      parse_mode: 'HTML',
      disable_web_page_preview: true,
    }),
  });

  if (!res.ok) {
    console.error('Telegram sendMessage failed:', res.status, await res.text());
  }
}

async function processResult(env, monitor, result) {
  const key = `fail-streak:${monitor.name}`;
  const prevRaw = await env.STATE.get(key);
  const prev = prevRaw ? parseInt(prevRaw, 10) : 0;

  if (result.ok) {
    if (prev >= FAIL_CONSEC_THRESHOLD) {
      await sendTelegram(env,
        `✅ <b>AFK4 RECOVERED</b>\n` +
        `Monitor: <b>${escapeHtml(monitor.name)}</b>\n` +
        `URL: <code>${escapeHtml(monitor.url)}</code>\n` +
        `Response: ${result.elapsedMs} ms`);
    }
    if (prev > 0) {
      await env.STATE.delete(key);
    }
    return;
  }

  const next = prev + 1;
  await env.STATE.put(key, String(next), { expirationTtl: KV_STATE_TTL_SECONDS });

  if (next === FAIL_CONSEC_THRESHOLD) {
    await sendTelegram(env,
      `🚨 <b>AFK4 DOWN</b>\n` +
      `Monitor: <b>${escapeHtml(monitor.name)}</b>\n` +
      `Request: <code>${escapeHtml(monitor.method)} ${escapeHtml(monitor.url)}</code>\n` +
      `Reason: ${escapeHtml(result.reason)}\n` +
      `Consecutive failures: ${next}`);
  }
}

export default {
  async scheduled(controller, env, ctx) {
    const results = await Promise.all(
      MONITORS.map(async (monitor) => ({ monitor, result: await probe(monitor) }))
    );
    ctx.waitUntil(
      Promise.all(results.map(({ monitor, result }) => processResult(env, monitor, result)))
    );
  },

  async fetch(req, env, ctx) {
    return new Response(
      `AFK4 staging uptime monitor.\n` +
      `Cron-triggered; no HTTP surface.\n` +
      `Monitors: ${MONITORS.length}\n`,
      { headers: { 'content-type': 'text/plain; charset=utf-8' } }
    );
  },
};
