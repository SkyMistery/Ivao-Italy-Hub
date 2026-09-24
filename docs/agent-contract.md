# The validator's agent contract

A tour validator can run a program on their own computer — the **agent** — that does the checks the hub cannot do on the
server: the ones that need navigation data the hub does not have and may not redistribute (where a fix is, which airway a
route follows). The agent reads a report from the hub, runs its checks locally, and sends back **only the outcome and a short
text**. The hub shows that outcome on the validation page next to its own checks, and a failed one suggests the errors of the
catalogue it is linked to. Nothing the agent sends decides anything: the validator does.

This page is for whoever writes an agent. It describes **version 1** of the contract.

- The hub of a division runs without any agent. The checks only an agent runs then show as «not available», and the validator
  judges them by eye, as before.
- The agent sees what its validator sees, and writes only where its validator may decide. It never reads or writes the
  validator's own reports.

## 1. A token

The agent does not use the browser's session. It uses a **personal token** its validator creates in the hub:

1. The validator signs in to the hub and opens **My tokens** (`/me/tokens`, linked from their dashboard; the link appears only
   to members who hold a permission some program needs).
2. They create a token for **The validator's agent** (audience `flightops.agent`), name it after the computer it will live on,
   and choose how many days it lasts — from 1 to 90.
3. The hub shows the token **once**. It starts with `hubpat_`. The validator pastes it into the agent's settings.

What to know about a token:

- It carries **no permissions** of its own. On every request the hub rebuilds who the validator is from their positions and
  grants of that moment: a validator removed from a tour stops seeing its reports on the next request.
- It works only if its validator **signed in to the hub within the last 30 days** (positions are refreshed at sign-in).
- It opens the agent's endpoints below and **nothing else** in the hub.
- The validator can revoke it at any time from the same page. Ten live tokens at most per person.

## 2. Every request

```http
Authorization: Bearer hubpat_…
Hub-Agent-Contract: 1
```

`Hub-Agent-Contract` is the version of the contract the agent speaks. The hub answers with the same header. A request without
it, or with a version the hub does not speak, is refused with **400** and the versions it accepts:

```json
{
  "status": 400,
  "title": "Say which version of the contract the agent speaks, in the Hub-Agent-Contract header.",
  "code": "agentContract",
  "current": 1,
  "accepted": [1]
}
```

The version is in a header and not in the address on purpose: the hub's own pages and its server always ship together and
have no versioned API; the agent is a separate product with its own releases.

**Versioning.** Within a version the hub only **adds**: a new field in an answer, a new optional field in a request. An agent
must ignore fields it does not know. A change that would break an agent is a new version, and the hub accepts the old one
beside it for at least one release.

## 3. The endpoints

All times are UTC, in ISO 8601. Field names are camel case. Statuses and outcomes are strings.

### `GET /api/flightops/agent/contract`

Open to anybody, with or without a token or the version header: an agent can call it before it is configured.

```json
{
  "current": 1,
  "accepted": [1],
  "agentChecks": ["semicircularLevels", "atcCoverage"],
  "serverChecks": ["callsign", "aircraft", "flightRules", "…", "vmc", "repeatedRoute"],
  "maxResults": 50,
  "maxEvidenceCharacters": 2000
}
```

`agentChecks` are the checks of the hub's catalogue that the server does not run. `serverChecks` are the server's: **an agent
may not send them** (§4). A key in neither list is still accepted from an agent (§4).

### `GET /api/flightops/agent/pireps`

The reports waiting for a validator — queued, or in someone's review — that the token's validator may decide, the ones that
have waited longest first, at most 200. With `?pending=true`, only the reports whose rules ask for an agent's check and on
which no agent has sent anything since they were queued.

```json
[
  {
    "id": 1842,
    "tourId": 12,
    "tourTitle": { "en": "Islands tour", "it": "Giro delle isole" },
    "legNumber": 3,
    "departureIcao": "LIRF",
    "arrivalIcao": "LIMC",
    "pilotVid": 123456,
    "takeoffAt": "2026-09-23T14:05:00Z",
    "queuedAt": "2026-09-23T16:40:12Z",
    "status": "Queued",
    "agentChecks": ["semicircularLevels"],
    "agentRanAt": null
  }
]
```

### `GET /api/flightops/agent/pireps/{id}`

One report, with what the agent's checks read. **403** if the validator may not decide it (another tour, or their own report),
**404** if it does not exist.

```json
{
  "id": 1842,
  "tourId": 12,
  "tourTitle": { "en": "Islands tour", "it": "Giro delle isole" },
  "status": "Queued",
  "pilotVid": 123456,
  "flightRules": "I",
  "sid": null, "star": null, "approach": null,
  "leg": { "number": 3, "departureIcao": "LIRF", "arrivalIcao": "LIMC", "callsigns": [] },
  "isDiversion": false,
  "diversionIcao": null,
  "flights": [
    {
      "seq": 1,
      "callsign": "AZA123",
      "aircraft": "A320",
      "departureIcao": "LIRF",
      "arrivalIcao": "LIMC",
      "takeoffAt": "2026-09-23T14:05:00Z",
      "landingAt": "2026-09-23T15:02:00Z",
      "planAtTakeoffRevision": 2,
      "plans": [
        {
          "revision": 2, "filedAt": "2026-09-23T13:31:00Z",
          "departureIcao": "LIRF", "arrivalIcao": "LIMC", "alternateIcao": "LIML", "secondAlternateIcao": null,
          "aircraftIcao": "A320", "wakeTurbulence": "M", "equipment": "SDE1E2FGHIJ1RWY", "transponder": "LB1",
          "flightRules": "I", "flightType": "S", "level": "F360", "speed": "N0450",
          "route": "…", "remarks": "PBN/A1B1C1D1O1S2 …", "departureTimeMinutes": 840, "enrouteMinutes": 58
        }
      ],
      "track": [
        { "at": "2026-09-23T14:05:00Z", "latitude": 41.80, "longitude": 12.24, "altitudeFeet": 50,
          "groundSpeedKnots": 140, "heading": 250, "onGround": false }
      ]
    }
  ],
  "checks": [ { "key": "semicircularLevels", "parameters": {} } ],
  "settings": { "northSouthLevelCountries": [] },
  "airports": [
    { "icao": "LIMC", "name": "…", "countryId": "IT", "latitude": 45.63, "longitude": 8.72, "elevationFeet": 768,
      "runways": [ { "designator": "RW35L", "lengthMetres": 3920, "bearing": 349, "latitude": 45.61, "longitude": 8.72, "elevationFeet": 768 } ] }
  ],
  "atc": {
    "available": true,
    "from": "2026-09-23T13:05:00Z",
    "to": "2026-09-23T16:02:00Z",
    "online": [ { "callsign": "LIRR_CTR", "frequency": "125.500", "startedAt": "…", "endedAt": "…" } ],
    "divisionSince": "2026-01-01T00:00:00Z",
    "worldSince": "2026-06-01T00:00:00Z",
    "divisionPrefixes": ["LI"],
    "declared": [ { "callsign": "LIRR_CTR", "frequency": "125.500", "origin": "Proposed" } ],
    "exemptions": [ { "callsign": "LIRR_CTR", "kind": "DirectRouting", "note": null, "status": "Online", "softens": [] } ]
  },
  "results": []
}
```

- `plans` holds **every** revision the pilot filed; `planAtTakeoffRevision` is the one the checks read (the last filed before
  take-off).
- `track` is about one point every 15 seconds. It is `null` once the hub has deleted it (90 days after the decision).
- `checks` are the checks the report's rules name that the server does not run, each with the parameters of the rule as the
  report froze them at its send. Run those; running others is allowed (§4).
- `settings.northSouthLevelCountries` are the two-letter country codes where semicircular levels go north and south.
- `atc` covers the flight with an hour on either side. `available: false` means the division has no archive of controller
  sessions, or it could not be read: say «not available», never «nobody was online». A position missing from `online` was
  really offline only from `divisionSince` (for callsigns starting with a `divisionPrefixes` entry) or `worldSince` (for the
  rest) on.
- `results` are what agents already sent on this report, one per check.
- ⚠️ IVAO sometimes gives a runway's length in feet as if it were metres.

### `POST /api/flightops/agent/pireps/{id}/checks`

The results of one run on one report.

```json
{
  "agentVersion": "1.4.0",
  "results": [
    {
      "checkKey": "semicircularLevels",
      "outcome": "Failed",
      "evidence": ["DCT ELB to SRN, magnetic track 332, FL360 even: wrong"]
    },
    {
      "checkKey": "atcCoverage",
      "outcome": "Unavailable",
      "evidence": ["No archive of controller sessions for this flight."]
    }
  ]
}
```

Answer, **200**: every result agents have sent on the report, and the errors of the catalogue now suggested on it by any check,
the server's included.

```json
{
  "results": [
    { "checkKey": "atcCoverage", "outcome": "Unavailable", "evidence": ["…"], "ranAt": "…", "byVid": 654321, "agentVersion": "1.4.0" },
    { "checkKey": "semicircularLevels", "outcome": "Failed", "evidence": ["…"], "ranAt": "…", "byVid": 654321, "agentVersion": "1.4.0" }
  ],
  "suggestedErrorIds": [37]
}
```

## 4. The rules of a result

- **`outcome`** is `Passed`, `Failed` or `Unavailable`. `Unavailable` means the check could not run (data missing, a failure of
  the agent): it is never a failure of the pilot.
- **Sending a check again replaces** the agent's previous result for it on that report — whichever validator's agent sent it.
  Send all the results of a run together, or one at a time: each replaces only its own check.
- **`checkKey`** is letters and digits, starting with a small letter, up to 64 characters, and once per request. A key the
  **server** runs (`serverChecks` of the contract) is refused: every check has one runner. A key the hub does not know is kept
  and shown, and suggests nothing until the division links an error of its catalogue to it — a new check of the agent needs no
  change to the contract.
- **`evidence`** is text, at most 50 lines and 2000 characters in all, in the language the validators read. The page shows it
  as it is.
- **`agentVersion`** is required, up to 32 characters. The page shows it next to the result, with the validator's name.
- Results are taken only while the report **waits**: queued or in review. On a decided report the answer is **409**; when a
  decision is reopened the report waits again, and the agent can send again.

### What the evidence may and may not say

The agent reads navigation data licensed to its validator personally. What it sends to the hub must not redistribute it.

| The evidence **may** contain | The evidence **must not** contain |
|---|---|
| the outcome and the check's key | coordinates |
| the points **the pilot wrote** in the plan | distances precise enough to work out positions |
| the magnetic track of a segment, rounded to the degree | points the pilot did not write (those an airway expands to) |
| the level and whether it is odd or even | magnetic variation values |
| the country or FIR code | geometries, maps, images |
| the AIRAC cycle | |

## 5. Answers that are not 200

| Status | When | Body |
|---|---|---|
| 400 | no `Hub-Agent-Contract`, or a version the hub does not speak | `code: "agentContract"`, `current`, `accepted` |
| 400 | a result the contract refuses | a validation problem: `errors` maps a field (`agentVersion`, `results`, `results[0].checkKey`, `results[0].outcome`, `results[0].evidence`) to i18n keys, listed below |
| 401 | the token is unknown, revoked, expired, or its validator has not signed in for 30 days | `code`: `unknown`, `revoked`, `expired` or `signInAgain`, and a sentence saying what to do |
| 403 | the validator may not decide this report: not enabled on its tour, or it is their own | — |
| 404 | no such report | — |
| 409 | the report is no longer waiting (`code: "agentNotWaiting"`), or somebody wrote it at the same moment (`code: "conflict"`): read it again | problem details |

The keys of a refused result:

| Key | Meaning |
|---|---|
| `flightops:errors.agentVersion` | `agentVersion` missing or longer than 32 characters |
| `flightops:errors.agentResults` | no results, or more than 50 |
| `flightops:errors.agentCheckKey` | the key is not letters and digits starting with a small letter, or is longer than 64 |
| `flightops:errors.agentCheckKeyServer` | the server runs this check |
| `flightops:errors.agentCheckKeyTwice` | the same check twice in one request |
| `flightops:errors.agentOutcome` | not `Passed`, `Failed` or `Unavailable` |
| `flightops:errors.agentEvidence` | more than 50 lines or 2000 characters |

## 6. Trying it with curl

```bash
curl -s https://hub.example.org/api/flightops/agent/contract
```

```bash
curl -s -H "Authorization: Bearer $HUB_TOKEN" -H "Hub-Agent-Contract: 1" "https://hub.example.org/api/flightops/agent/pireps?pending=true"
```

```bash
curl -s -X POST -H "Authorization: Bearer $HUB_TOKEN" -H "Hub-Agent-Contract: 1" -H "Content-Type: application/json" -d '{"agentVersion":"curl","results":[{"checkKey":"semicircularLevels","outcome":"Failed","evidence":["FL360 even on an eastbound DCT segment"]}]}' https://hub.example.org/api/flightops/agent/pireps/1842/checks
```

No `X-Requested-With` header is needed: that guard is for the browser's session, and a request with a token is not one.
