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
 *   node tools/record-ivao-fixtures.mjs --list <file.json> <asVid>
 *
 * records chosen flights of several members instead: the file — kept outside the repository, because it
 * holds real VIDs — is an array of { report, vid, callsign, date, departure, arrival, takeoff } (date as
 * yyyy-mm-dd, the flight's UTC day; takeoff as an ISO instant), and the session between the two airports
 * that was connected at takeoff is written under the one <asVid>. Beside the usual files it writes
 * tracker-reports-<asVid>.json, which says which sessions each report flew, without the VID.
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

const listed = process.argv[2] === "--list";
const vid = listed ? 0 : Number(process.argv[2]);
const wanted = listed ? 0 : Number(process.argv[3] ?? 3);
const asVid = Number(process.argv[4] ?? 780001);
if (listed ? !process.argv[3] || !asVid : !vid) {
  console.error(
    "Give the VID to record from: node tools/record-ivao-fixtures.mjs <vid> [flights] [asVid]\n"
      + "or a list of flights:        node tools/record-ivao-fixtures.mjs --list <file.json> <asVid>",
  );
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

/** Every session of a member in an interval, paged fifty at a time. */
const sessionsOf = async (member, from, to) => {
  const query = `userId=${member}&from=${encodeURIComponent(from.toISOString())}&to=${encodeURIComponent(to.toISOString())}`;
  const sessions = [];
  for (let page = 1; ; page++) {
    const body = await get(`/v2/tracker/sessions?${query}&page=${page}&perPage=50`);
    sessions.push(...body.items);
    if (page >= body.pages) break;
  }
  return sessions;
};

/** Writes the plans and the tracks of one session; false when IVAO has already dropped the points. */
const record = async (session) => {
  const tracks = await get(`/v2/tracker/sessions/${session.id}/tracks`);
  if (!Array.isArray(tracks) || tracks.length === 0) return false;

  const plans = await get(`/v2/tracker/sessions/${session.id}/flightPlans`);
  writeFileSync(join(outDir, `tracker-flightplans-${session.id}.json`), JSON.stringify(anonymise(plans), null, 2));
  writeFileSync(join(outDir, `tracker-tracks-${session.id}.json`), JSON.stringify(anonymise(tracks)));

  const plan = plans[0] ?? {};
  console.log(
    `recorded ${session.id} ${session.callsign} ${plan.departureId ?? "?"}-${plan.arrivalId ?? "?"} `
      + `${Math.round(session.time / 60)} min, ${plans.length} plan revision(s), ${tracks.length} track point(s)`,
  );
  return true;
};

mkdirSync(outDir, { recursive: true });
const recorded = [];

if (listed) {
  const flights = JSON.parse(readFileSync(process.argv[3], "utf8"));
  const reports = [];
  for (const flight of flights) {
    // The flight's day, and the day before it: a session that starts before midnight lands after it.
    const day = new Date(`${flight.date}T00:00:00Z`);
    // The session that was in the air at the declared takeoff, between the leg's two airports.
    const takeoff = new Date(flight.takeoff);
    const candidates = (await sessionsOf(flight.vid, new Date(day.getTime() - 86400000), new Date(day.getTime() + 2 * 86400000)))
      .filter((session) => session.callsign === flight.callsign)
      .filter((session) => (session.flightPlans ?? []).some(
        (plan) => plan.departureId === flight.departure && plan.arrivalId === flight.arrival))
      .filter((session) => new Date(session.createdAt) <= takeoff && takeoff <= new Date(session.updatedAt));

    const sessionIds = [];
    for (const session of candidates) {
      if (recorded.some((row) => row.id === session.id)) {
        sessionIds.push(session.id);
      } else if (await record(session)) {
        recorded.push(session);
        sessionIds.push(session.id);
      }
    }

    if (sessionIds.length === 0) console.warn(`report ${flight.report}: no session of ${flight.callsign} on ${flight.date}`);
    reports.push({ report: flight.report, callsign: flight.callsign, date: flight.date, sessionIds });
  }

  writeFileSync(join(outDir, `tracker-reports-${asVid}.json`), JSON.stringify(reports, null, 2));
} else {
  const to = new Date();
  const from = new Date(to.getTime() - 89 * 86400000); // tracks live about ninety days
  const flights = (await sessionsOf(vid, from, to))
    .filter((session) => (session.flightPlans?.length ?? 0) > 0 && session.time > 600)
    .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));

  for (const session of flights) {
    if (recorded.length >= wanted) break;
    if (await record(session)) recorded.push(session);
  }
}

writeFileSync(join(outDir, `tracker-sessions-${asVid}.json`), JSON.stringify(anonymise(recorded), null, 2));
console.log(`\nwrote ${recorded.length} session(s) to tests/fixtures/ivao/tracker-sessions-${asVid}.json as VID ${asVid}`);
