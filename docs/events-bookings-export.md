# The bookings export of an event

An event with slots publishes, besides its page, an **export of its slots and of their bookings** for a program outside the
hub: a **gate manager**, which plans the stands of the event's airports from the flights that will use them. The program reads
the export with a member's personal token, and the hub answers with every slot of the event, in the names gate managers already
read.

This page is for whoever writes or configures such a program. It describes **version 1** of the contract.

## 1. A token

The program does not use the browser's session. It uses a **personal token** that a member who reads the bookings of the event
(`EventBookings.View`) creates in the hub:

1. The member signs in to the hub and opens **My tokens** (`/me/tokens`, linked from their dashboard).
2. They create a token for **reading the bookings of the events** (audience `events.bookings`), name it after the program that
   will hold it, and choose how many days it lasts — from 1 to 90.
3. The hub shows the token **once**. It starts with `hubpat_`. The member pastes it into the program's settings.

What to know about a token:

- It carries **no permissions** of its own. On every request the hub rebuilds who the member is from their positions and grants
  of that moment: a member who stops reading the bookings of an event stops exporting it on the next request.
- It works only if its member **signed in to the hub within the last 30 days**.
- It opens this export and **nothing else** in the hub.
- The member can revoke it at any time from the same page. Ten live tokens at most per person.

## 2. Every request

```http
Authorization: Bearer hubpat_…
Hub-Bookings-Contract: 1
```

`Hub-Bookings-Contract` is the version of the contract the program speaks. The hub answers with the same header. A request
without it, or with a version the hub does not speak, is refused with **400** and the versions it accepts:

```json
{
  "status": 400,
  "title": "Say which version of the contract the program speaks, in the Hub-Bookings-Contract header.",
  "code": "bookingsContract",
  "current": 1,
  "accepted": [1]
}
```

The version is in a header and not in the address on purpose: the hub's own pages and its server always ship together and
have no versioned API; a gate manager is a separate program with its own releases.

**Versioning.** Within a version the hub only **adds**: a new field in an answer. A program must ignore fields it does not
know. A change that would break a program is a new version, and the hub accepts the old one beside it for at least one release.

## 3. The export

### `GET /api/events/{slug}/bookings/export`

`{slug}` is the event's address, the one of its public page (`/events/{slug}`). The event must be **published**: a draft is
never exported. A published event is exported in every state — scheduled, over, cancelled.

Answer, **200**: an array of flights, one per slot of the event, **by their time at the airport of the event** — the off block
of a departure from it, the on block of an arrival at it — and then by `slot_id`. All times are UTC, in ISO 8601 with the `Z`.

```json
[
  {
    "slot_id": 3108,
    "callsign": "ABC101",
    "flight_number": "AB101",
    "booked_by": 100001,
    "aircraft_icao": "A320",
    "aircraft_types": ["A320", "A20N"],
    "gate": "B12",
    "eobt": "2026-10-17T17:00:00Z",
    "eat": "2026-10-17T18:10:00Z",
    "origin_icao": "EHAM",
    "destination_icao": "LFPG",
    "rotation": "R1",
    "leg": 1,
    "paired_slot_id": null
  },
  {
    "slot_id": 3109,
    "callsign": "ABC102",
    "flight_number": "AB102",
    "booked_by": null,
    "aircraft_icao": null,
    "aircraft_types": ["A320"],
    "gate": null,
    "eobt": "2026-10-17T18:40:00Z",
    "eat": "2026-10-17T19:40:00Z",
    "origin_icao": "LFPG",
    "destination_icao": "EHAM",
    "rotation": "R1",
    "leg": 2,
    "paired_slot_id": null
  },
  {
    "slot_id": 3120,
    "callsign": null,
    "flight_number": null,
    "booked_by": null,
    "aircraft_icao": null,
    "aircraft_types": [],
    "gate": null,
    "eobt": "2026-10-17T20:00:00Z",
    "eat": null,
    "origin_icao": "EHAM",
    "destination_icao": null,
    "rotation": null,
    "leg": null,
    "paired_slot_id": null
  }
]
```

| Field | What it is |
|---|---|
| `slot_id` | the slot's identity in the hub, the same for as long as the slot exists |
| `callsign` | the flight's callsign; `null` on a private slot |
| `flight_number` | the flight number, when the staff wrote one |
| `booked_by` | the member who booked the slot, by their network number; `null` while the slot is free. A **negative** number stands for a member whose data the hub erased at their request: the booking is kept, the person is not |
| `aircraft_icao` | the aircraft type the pilot chose when booking, among those the slot allows; `null` while the slot is free |
| `aircraft_types` | the aircraft types the slot admits, ICAO codes, **the first the main one** — what the stands are planned for before anybody books; empty on a private slot, whose pilot says the type |
| `gate` | the stand the staff wrote for the slot; `null` when there is none, and on a private slot |
| `eobt` | the off block time at `origin_icao`; `null` on a private arrival |
| `eat` | the on block time at `destination_icao`; `null` on a private departure |
| `origin_icao`, `destination_icao` | the two airports of the flight, ICAO codes |
| `rotation`, `leg` | the rotation the flight belongs to and its place in it, from 1; `null` when it belongs to none |
| `paired_slot_id` | for a private slot, the slot paired with it on the same stand; `null` otherwise |

- A **public** slot is a flight the staff wrote: its callsign, its two airports, its two times. Every slot touches an airport
  of the event: it departs from one, or arrives at one. One pilot books it, with one of the aircraft types it allows; the pilot
  may withdraw until its off block time, and the staff may take the booking away, so a slot booked at one request can be free
  at the next.
- A **private** slot is a time offered at an airport of the event, for a pilot who books it with a flight of their own: until
  then it carries only that airport — as `origin_icao` for a departure, `destination_icao` for an arrival — and its time there.
- The legs of a rotation follow one another: each departs from where the one before arrived, after it arrived.

## 4. Answers that are not 200

The token is checked first, then the version, then the event.

| Status | When | Body |
|---|---|---|
| 400 | no `Hub-Bookings-Contract`, or a version the hub does not speak | `code: "bookingsContract"`, `current`, `accepted` |
| 401 | no token — the browser's session does not count —, or the token is unknown, revoked, expired, or its member has not signed in for 30 days | for a token sent, `code`: `unknown`, `revoked`, `expired` or `signInAgain`, and a sentence saying what to do |
| 403 | a token of another audience (no body), or a member who does not read the bookings of this event (problem details) | — |
| 404 | no event at that address | problem details |
| 409 | the event is a draft: it is exported once published | `code: "draft"` |

## 5. Trying it with curl

```bash
curl -s -H "Authorization: Bearer $HUB_TOKEN" -H "Hub-Bookings-Contract: 1" https://hub.example.org/api/events/autumn-fly-in/bookings/export
```

No `X-Requested-With` header is needed: that guard is for the browser's session, and a request with a token is not one.
