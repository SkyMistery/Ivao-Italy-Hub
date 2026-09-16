/**
 * Records tracker fixtures from real flights, so the tests never call IVAO.
 *
 * Usage (from the root of the repository, with config/ivao-oauth.json in place):
 *
 *   node tools/record-ivao-fixtures.mjs <vid> [howManyFlights] [asVid]
 *   node tools/record-ivao-fixtures.mjs 704798 3 780001
 *
 * It writes tests/fixtures/ivao/tracker-sessions-<asVid>.json, and one
 * tracker-flightplans-<sessionId>.json and tracker-tracks-<sessionId>.json per flight.
 *
 * The recorded rows are anonymised: the VID becomes <asVid> (the range the integration tests own),
 * and the member object IVAO embeds — real name, country, staff positions — is dropped. Tracks are
 * only kept by IVAO for about ninety days, so a fixture cannot be re-recorded from an old flight.
 */
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const outDir = join(root, "tests/fixtures/ivao");

const vid = Number(process.argv[2]);
const wanted = Number(process.argv[3] ?? 3);
const asVid = Number(process.argv[4] ?? 780001);
if (!vid) {
  console.error("Give the VID to record from: node tools/record-ivao-fixtures.mjs <vid> [flights] [asVid]");
  process.exit(1);
}

const ivao = JSON.parse(readFileSync(join(root, "config/ivao-oauth.json"), "utf8")).Ivao;
const base = ivao.Authority.replace(/\/$/, "");

const tokenResponse = await fetch(`${base}/v2/oauth/token`, {
  method: "POST",
  headers: { "content-type": "application/x-www-form-urlencoded" },
  body: new URLSearchParams({
    grant_type: "client_credentials",
    client_id: ivao.ClientId,
    client_secret: ivao.ClientSecret,
    ...(ivao.ApiScopes?.length ? { scope: ivao.ApiScopes.join(" ") } : {}),
  }),
});
if (!tokenResponse.ok) {
  console.error(`The token endpoint answered ${tokenResponse.status}.`);
  process.exit(1);
}
const { access_token: token } = await tokenResponse.json();

const get = async (path) => {
  const response = await fetch(`${base}${path}`, { headers: { authorization: `Bearer ${token}` } });
  if (!response.ok) throw new Error(`${path} answered ${response.status}`);
  return response.json();
};

/** The VID, and everything IVAO knows about the person behind it, out of the recorded row. */
const anonymise = (value) => {
  if (Array.isArray(value)) return value.map(anonymise);
  if (value && typeof value === "object") {
    const copy = {};
    for (const [key, inner] of Object.entries(value)) {
      if (key === "user" || key === "userStaffPositions") continue;
      copy[key] = key === "userId" || key === "vid" ? asVid : anonymise(inner);
    }
    return copy;
  }
  return value;
};

const to = new Date();
const from = new Date(to.getTime() - 89 * 86400000); // tracks live about ninety days
const query = `userId=${vid}&from=${encodeURIComponent(from.toISOString())}&to=${encodeURIComponent(to.toISOString())}`;

const sessions = [];
for (let page = 1; ; page++) {
  const body = await get(`${query ? `/v2/tracker/sessions?${query}` : ""}&page=${page}&perPage=50`);
  sessions.push(...body.items);
  if (page >= body.pages) break;
}

const flights = sessions
  .filter((session) => (session.flightPlans?.length ?? 0) > 0 && session.time > 600)
  .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));

const recorded = [];
for (const session of flights) {
  if (recorded.length >= wanted) break;

  const tracks = await get(`/v2/tracker/sessions/${session.id}/tracks`);
  if (!Array.isArray(tracks) || tracks.length === 0) continue; // too old: IVAO has dropped the points

  const plans = await get(`/v2/tracker/sessions/${session.id}/flightPlans`);
  writeFileSync(join(outDir, `tracker-flightplans-${session.id}.json`), JSON.stringify(anonymise(plans), null, 2));
  writeFileSync(join(outDir, `tracker-tracks-${session.id}.json`), JSON.stringify(anonymise(tracks)));
  recorded.push(session);

  const plan = plans[0] ?? {};
  console.log(
    `recorded ${session.id} ${session.callsign} ${plan.departureId ?? "?"}-${plan.arrivalId ?? "?"} `
      + `${Math.round(session.time / 60)} min, ${plans.length} plan revision(s), ${tracks.length} track point(s)`,
  );
}

mkdirSync(outDir, { recursive: true });
writeFileSync(join(outDir, `tracker-sessions-${asVid}.json`), JSON.stringify(anonymise(recorded), null, 2));
console.log(`\nwrote ${recorded.length} session(s) to tests/fixtures/ivao/tracker-sessions-${asVid}.json as VID ${asVid}`);
