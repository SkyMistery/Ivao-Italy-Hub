// A Cloudflare Worker that keeps an installation of the hub awake: it asks the hub's /health every few seconds, so that the
// host's process manager finds a process already running, or starts one, before a visitor needs it (README.md next to this
// file; note docs/internal/decisions/2026-10-26-il-ping-che-tiene-sveglio-l-hub.md).
//
// The cron of a Worker fires at most once a minute, so every run sends one ping now and the others later in the same run,
// through ctx.waitUntil: waiting costs no CPU time. Every ping is best effort: an error is written to the Worker's log and
// never stops the next one.

/** Never more often than this, whatever the variable says: every ping that finds the hub stopped costs a whole start. */
const SHORTEST_INTERVAL_SECONDS = 5;

/** One run covers one minute of the cron. */
const RUN_SECONDS = 60;

/** A ping waits this long for the hub, which is enough for a start on a slow host and less than the next ping. */
const TIMEOUT_MS = 30_000;

export default {
  async scheduled(controller, env, ctx) {
    const target = targetOf(env);
    const interval = intervalOf(env);

    for (let second = 0; second < RUN_SECONDS; second += interval) {
      ctx.waitUntil(pingAfter(target, second * 1000));
    }
  },
};

/** The address to ask, from the variable TARGET_URL: an https address, the hub's /health. */
function targetOf(env) {
  const value = (env.TARGET_URL ?? '').trim();
  let url;
  try {
    url = new URL(value);
  } catch {
    throw new Error("TARGET_URL is not an address: set it to the https address of the hub's /health.");
  }

  if (url.protocol !== 'https:') {
    throw new Error('TARGET_URL must be an https address.');
  }

  return url;
}

/** Seconds between two pings, from the variable INTERVAL_SECONDS: 10 when it is missing, never under the shortest. */
function intervalOf(env) {
  const value = Number.parseInt(env.INTERVAL_SECONDS ?? '10', 10);
  return Number.isFinite(value) ? Math.min(RUN_SECONDS, Math.max(SHORTEST_INTERVAL_SECONDS, value)) : 10;
}

async function pingAfter(target, delayMs) {
  if (delayMs > 0) {
    await new Promise((resolve) => setTimeout(resolve, delayMs));
  }

  // A new query string every time and no cache at the edge: an answer from Cloudflare's cache would wake nothing.
  const url = new URL(target);
  url.searchParams.set('t', Date.now().toString());

  const started = Date.now();
  try {
    const response = await fetch(url, {
      headers: { 'Cache-Control': 'no-cache', 'User-Agent': 'hub-keep-warm' },
      cf: { cacheTtl: 0, cacheEverything: false },
      signal: AbortSignal.timeout(TIMEOUT_MS),
    });
    await response.arrayBuffer();

    if (!response.ok) {
      console.warn(`The hub answered ${response.status} in ${Date.now() - started} ms.`);
    }
  } catch (error) {
    console.warn(`The hub did not answer: ${error}`);
  }
}
