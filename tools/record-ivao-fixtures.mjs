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
 *   node tools/record-ivao-fixtures.mjs --airports <name> <ICAO> [ICAO...]
 *
 * records public reference data instead: each airport as /v2/airports/{icao} answers it, with the runway ends
 * /v2/airports/{icao}/runways gives, into tests/fixtures/ivao/airports-<name>.json. The checks on the tracks read
 * the airport's position and the thresholds from there (T18); no member is in it.
 *
 * The recorded rows are anonymised: the VID becomes <asVid> (the range the integration tests own),
 * and the member object IVAO embeds — real name, country, staff positions — is dropped. Tracks are
 * only kept by IVAO for about ninety days, so a fixture cannot be re-recorded from an old flight.
 *
 *   node tools/record-ivao-fixtures.mjs --me <asVid>
 *
 * records the profile a sign in reads, /v2/users/me, which only opens to a member's own token (M3, A1). So it signs
 * in the way the hub does — authorization code with PKCE, the client and the scopes of config/ivao-oauth.json — and
 * listens for IVAO's answer on the redirect address registered for that client: nothing else may be listening there
 * while it runs, the development server of the SPA included. A browser opens on IVAO's own sign in page, where the
 * member types their own credentials; the token never leaves this process. It writes users-me-<asVid>.json with the
 * fields the hub reads and nothing else, the person taken out: the VID, the names and the address become <asVid> and
 * placeholders, the staff positions an empty list, and the connection times keep their shape and lose their values.
 * What IVAO sent for the ratings and the times is printed, so that the unit can be compared with the member's page.
 *
 *   node tools/record-ivao-fixtures.mjs --positions <name> <ICAO> [ICAO...]
 *
 * records public reference data: the ATC positions of the airports named, as /v2/ATCPositions/all answers them, into
 * atc-positions-<name>.json, and the sectors of the FIRs named, as /v2/subcenters/all answers them, into
 * subcenters-<name>.json (M3, A1). Both without the outline of the sector (regionMap, regionMapPolygon), which is most
 * of what the two answers weigh for the world. They are asked with mapType=regionMapPolygon, one outline instead of two,
 * as the hub asks them (M3, A2): without it the sectors of the world take longer than IVAO's gateway waits, and the
 * answer is a 504 or a connection closed half way.
 */
import { spawn } from "node:child_process";
import { createHash, randomBytes } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { createServer } from "node:http";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const outDir = join(root, "tests/fixtures/ivao");

const listed = process.argv[2] === "--list";
const airportsOnly = process.argv[2] === "--airports";
const profileOnly = process.argv[2] === "--me";
const positionsOnly = process.argv[2] === "--positions";
const referenceOnly = airportsOnly || positionsOnly;
const vid = listed || referenceOnly || profileOnly ? 0 : Number(process.argv[2]);
const wanted = listed ? 0 : Number(process.argv[3] ?? 3);
const asVid = Number(profileOnly ? process.argv[3] : process.argv[4] ?? 780001);
if (referenceOnly ? process.argv.length < 5 : profileOnly ? !asVid : listed ? !process.argv[3] || !asVid : !vid) {
  console.error(
    "Give the VID to record from: node tools/record-ivao-fixtures.mjs <vid> [flights] [asVid]\n"
      + "or a list of flights:        node tools/record-ivao-fixtures.mjs --list <file.json> <asVid>\n"
      + "or airports and runways:     node tools/record-ivao-fixtures.mjs --airports <name> <ICAO> [ICAO...]\n"
      + "or your own profile:         node tools/record-ivao-fixtures.mjs --me <asVid>\n"
      + "or ATC positions and sectors: node tools/record-ivao-fixtures.mjs --positions <name> <ICAO> [ICAO...]",
  );
  process.exit(1);
}

const ivao = JSON.parse(readFileSync(join(root, "config/ivao-oauth.json"), "utf8")).Ivao;
const base = ivao.Authority.replace(/\/$/, "");

/** Opens the default browser without a shell, so the address is never parsed by one. */
const openBrowser = (address) => {
  const [command, args] = process.platform === "win32"
    ? ["rundll32", ["url.dll,FileProtocolHandler", address]]
    : [process.platform === "darwin" ? "open" : "xdg-open", [address]];
  const child = spawn(command, args, { detached: true, stdio: "ignore" });
  child.on("error", () => console.log(`No browser could be opened; sign in at:\n${address}`));
  child.unref();
};

/** A member's own token, the way the hub gets one at a sign in (IvaoAuthenticationExtensions). */
const signIn = async (discovery) => {
  const redirect = new URL(ivao.RedirectUri);
  const verifier = randomBytes(32).toString("base64url");
  const state = randomBytes(16).toString("base64url");
  const authorize = new URL(discovery.authorization_endpoint);
  authorize.search = new URLSearchParams({
    response_type: "code",
    client_id: ivao.ClientId,
    redirect_uri: ivao.RedirectUri,
    scope: ivao.Scopes.join(" "),
    state,
    code_challenge: createHash("sha256").update(verifier).digest("base64url"),
    code_challenge_method: "S256",
  }).toString();

  const code = await new Promise((resolve, reject) => {
    const server = createServer((request, response) => {
      const answer = new URL(request.url, redirect);
      if (answer.pathname !== redirect.pathname) {
        response.writeHead(404).end();
        return;
      }
      const returned = answer.searchParams.get("state") === state ? answer.searchParams.get("code") : null;
      response
        .writeHead(returned ? 200 : 400, { "content-type": "text/plain; charset=utf-8" })
        .end(returned ? "Recorded. This tab can be closed." : "The sign in did not come back as expected.");
      server.close();
      if (returned) resolve(returned);
      else reject(new Error(`IVAO came back without a code (${answer.searchParams.get("error") ?? "no error given"}).`));
    });
    server.on("error", reject);
    server.listen(Number(redirect.port || 80), redirect.hostname, () => {
      console.log(`Waiting on ${redirect.origin}${redirect.pathname}; a browser opens on IVAO's sign in.`);
      openBrowser(authorize.href);
    });
  });

  const tokenResponse = await fetch(discovery.token_endpoint, {
    method: "POST",
    headers: { "content-type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      grant_type: "authorization_code",
      code,
      redirect_uri: ivao.RedirectUri,
      client_id: ivao.ClientId,
      client_secret: ivao.ClientSecret,
      code_verifier: verifier,
    }),
  });
  if (!tokenResponse.ok) throw new Error(`The token endpoint answered ${tokenResponse.status}.`);
  return (await tokenResponse.json()).access_token;
};

/**
 * What the hub reads of a profile (IvaoUserProfileReader), with the person taken out. The connection times keep their
 * shape and IVAO's unit and lose their values: 120 hours as a controller and 150 as a pilot, whatever the member has.
 */
const anonymiseProfile = (me) => {
  const invented = { atc: 120 * 3600, pilot: 150 * 3600 };
  const kept = {
    id: asVid,
    sub: String(asVid),
    firstName: "Test",
    lastName: "Member",
    given_name: "Test",
    family_name: "Member",
    nickname: "Test",
    publicNickname: `Test (${asVid})`,
    email: `member-${asVid}@example.invalid`,
    divisionId: me.divisionId,
    countryId: me.countryId,
    languageId: me.languageId,
    isStaff: me.isStaff,
    isSupervisor: me.isSupervisor,
    rating: me.rating,
    hours: Array.isArray(me.hours)
      ? me.hours.map((row) => ({ ...row, hours: (invented[row.type] ?? 0) + (Number.isInteger(row.hours) ? 0 : 0.25) }))
      : undefined,
    userStaffPositions: [],
  };
  return Object.fromEntries(Object.entries(kept).filter(([key, value]) => key in me && value !== undefined));
};

if (profileOnly) {
  const discovery = await (await fetch(`${base}/.well-known/openid-configuration`)).json();
  const accessToken = await signIn(discovery);
  const profileResponse = await fetch(discovery.userinfo_endpoint ?? `${base}/v2/users/me`, {
    headers: { authorization: `Bearer ${accessToken}` },
  });
  if (!profileResponse.ok) throw new Error(`/v2/users/me answered ${profileResponse.status}.`);
  const me = await profileResponse.json();

  const fixture = anonymiseProfile(me);
  console.log(`fields IVAO sent: ${Object.keys(me).join(", ")}`);
  console.log(`dropped: ${Object.keys(me).filter((key) => !(key in fixture)).join(", ")}`);
  for (const kind of ["atcRating", "pilotRating"]) {
    const { id, shortName, name } = me.rating?.[kind] ?? {};
    console.log(`${kind}: id ${id}, ${shortName}, ${name}`);
  }
  if (Array.isArray(me.hours)) {
    for (const row of me.hours) {
      console.log(`hours ${row.type}: ${row.hours} (${(row.hours / 3600).toFixed(2)} if seconds)`);
    }
  } else {
    console.warn(`hours is not the array IVAO documents: ${JSON.stringify(me.hours)}; it was not written.`);
  }

  mkdirSync(outDir, { recursive: true });
  writeFileSync(join(outDir, `users-me-${asVid}.json`), JSON.stringify(fixture, null, 2));
  console.log(`wrote tests/fixtures/ivao/users-me-${asVid}.json as VID ${asVid}`);
  process.exit(0);
}

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

if (airportsOnly) {
  const airports = [];
  for (const icao of process.argv.slice(4).map((code) => code.toUpperCase())) {
    const airport = await get(`/v2/airports/${icao}`);
    const runways = await get(`/v2/airports/${icao}/runways`);
    airports.push({
      icao,
      latitude: airport.latitude,
      longitude: airport.longitude,
      elevation: airport.elevation,
      runways: (Array.isArray(runways) ? runways : runways.items ?? []).map(
        ({ runway, length, width, bearing, latitude, longitude, elevation }) =>
          ({ runway, length, width, bearing, latitude, longitude, elevation })),
    });
    console.log(`recorded ${icao}: ${airports.at(-1).runways.length} runway end(s)`);
  }
  writeFileSync(join(outDir, `airports-${process.argv[3]}.json`), JSON.stringify(airports, null, 2));
  process.exit(0);
}

if (positionsOnly) {
  // Ten and twelve megabytes with one outline; still big enough that a second attempt is worth having.
  const getWorld = async (path) => {
    for (let attempt = 1; ; attempt++) {
      try {
        return await get(path);
      } catch (error) {
        if (attempt === 3) throw error;
        console.warn(`${path}: ${error.cause?.code ?? error.message}, again`);
      }
    }
  };
  const codes = new Set(process.argv.slice(4).map((code) => code.toUpperCase()));
  const withoutOutline = ({ regionMap, regionMapPolygon, ...position }) => position;
  const positions = (await getWorld("/v2/ATCPositions/all?mapType=regionMapPolygon"))
    .filter((row) => codes.has(row.airportId))
    .map(withoutOutline);
  const subcenters = (await getWorld("/v2/subcenters/all?mapType=regionMapPolygon"))
    .filter((row) => codes.has(row.centerId))
    .map(withoutOutline);
  writeFileSync(join(outDir, `atc-positions-${process.argv[3]}.json`), JSON.stringify(positions, null, 2));
  writeFileSync(join(outDir, `subcenters-${process.argv[3]}.json`), JSON.stringify(subcenters, null, 2));
  const types = (rows) => [...new Set(rows.map((row) => row.position))].join(", ");
  console.log(`recorded ${positions.length} position(s) (${types(positions)}) and ${subcenters.length} sector(s) (${types(subcenters)})`);
  process.exit(0);
}

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
