# The keep-warm Worker

A Cloudflare Worker that asks an installation's `/health` every ten seconds, so that a visitor finds the hub's process
already started instead of waiting for the start (about eight seconds on a Plesk + Passenger host).

**What it does not do**: keep one process alive for hours. On a host that stops the process within a minute whatever
the traffic, the ping starts the next process before a visitor needs it, and that is all. What it costs and what it
gains, measured: `docs/internal/decisions/2026-10-26-il-ping-che-tiene-sveglio-l-hub.md`; where to read whether it works:
`docs/DEPLOYING.md`, "Keeping the hub awake".

## How it works

- A cron trigger runs the Worker once a minute, the most a cron does. Each run sends a ping at once and one every
  `INTERVAL_SECONDS` after it, inside the same run (`ctx.waitUntil`): a waiting Worker uses no CPU time.
- The address is `TARGET_URL` with `?t=<now>` added, and the request asks Cloudflare not to cache: an answer from the
  cache would wake nothing.
- `/health` is the hub's cheapest address that reaches the process (one `SELECT 1`), and the hub counts it apart from the
  visitors: `probes` in `diagnostics/starts.txt`, no line in `requests-YYYYMMDD.txt`, no line in the log while it is healthy.
- A ping that fails or answers an error writes a warning in the Worker's log and nothing else.
- It needs no secret and has no public address.

## Deploying it

You need a Cloudflare account that holds the installation's domain and Node.js. From the root of the repository:

```bash
npx wrangler@4 login
```

```bash
npx wrangler@4 deploy --config tools/cloudflare/keep-warm/wrangler.jsonc --var TARGET_URL:https://<host>/health
```

`<host>` is the installation's host name. A second installation gets a Worker of its own, with a name of its own:

```bash
npx wrangler@4 deploy --config tools/cloudflare/keep-warm/wrangler.jsonc --name hub-keep-warm-<installation> --var TARGET_URL:https://<host>/health
```

Deploy again, with the same command, after changing `index.js` or `wrangler.jsonc`: a deploy without `--var TARGET_URL`
leaves the Worker without an address, and every run fails with "TARGET_URL is not an address" in its log.

| Variable | Default | |
|---|---|---|
| `TARGET_URL` | none, required | the `https` address of the installation's `/health` |
| `INTERVAL_SECONDS` | `10` | seconds between two pings, from 5 to 60; in `wrangler.jsonc`, or `--var INTERVAL_SECONDS:15` |

## Checking it, and stopping it

- **In the Worker's log** (dashboard: Workers & Pages, the Worker, Logs): one run a minute; a warning only when a ping failed.
- **In the hub**: `diagnostics/starts.txt`, as `docs/DEPLOYING.md` says. The `STOP` lines count `probes`.
- **To stop it**: disable the cron trigger in the dashboard (the Worker, Settings, Triggers), or `npx wrangler@4 delete --config tools/cloudflare/keep-warm/wrangler.jsonc`.
  Nothing in the hub depends on it.
