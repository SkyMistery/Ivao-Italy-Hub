# IVAO API fixtures

Answers shaped like the ones `/v2/centers` and `/v2/airports/all` give, used when
`Ivao:UseFixtures=true`. They exist because the OAuth client of a division is not necessarily
allowed those endpoints, and a division that forks must still be able to run the hub locally
without credentials at all.

They are written by hand and deliberately small: three FIRs and three airports are enough to prove
that the synchronisation upserts rather than duplicates, and that a FIR position such as `LIRR-CH`
starts being recognised once the snapshot exists. They are **not** a copy of a real IVAO response.

`whazzup.json` is the third one, and it answers "who is connected right now". It is written against
the other two on purpose: four controllers of which three work a station the snapshot knows, and
four flights of which two touch an airport it knows. That is what makes it a test of the rule and
not of the file — change an ICAO here and the figures the block draws change with it.

## The tracker files, and where they differ

`tracker-sessions-780001.json`, `tracker-flightplans-<id>.json` and `tracker-tracks-<id>.json` are
the opposite choice: they are **real flights**, recorded from the live API with
`tools/record-ivao-fixtures.mjs` and anonymised — the VID becomes 780001, the range the integration
tests own, and the member object IVAO embeds is dropped. A parser proved against invented JSON
proves only that the invention was parsed, and the shapes here are full of things nobody would
invent: three revisions of one flight plan, equipment letters that carry digits (`SBDFGJ1RUWXY`),
tracks sampled about every fifteen seconds.

Two limits worth knowing before re-recording: IVAO keeps the points of a session for about **ninety
days**, and the sessions of a member are paged fifty at a time. Both were measured on 16 September
2026, in phase T2.

## The corpus of the checks

`tracker-sessions-780002.json` holds fifteen flights of several pilots, all written under VID 780002:
the flights the tours' reports of August and September 2026 were about, recorded on 24 September 2026
(phase T17) with `node tools/record-ivao-fixtures.mjs --list <file> 780002`. The list the tool reads
holds the real VIDs and stays outside the repository. `tracker-reports-780002.json` says which session
each report flew, by the report's number in the old tour system; the unit tests of the checks read a
report through it, with what the controllers found on it as the expected outcome.

One thing the corpus taught: the revisions of a flight plan share their `createdAt` until the route
changes, and only `updatedAt` tells when each was filed — on one VFR flight two of four revisions came
after the take-off. The reader takes `updatedAt`.

The checks on the tracks (phase T18) need two more things, both public and without any member in them:
`airports-corpus.json` holds the position and the runway ends of the corpus's 24 airports, recorded with
`node tools/record-ivao-fixtures.mjs --airports corpus <ICAO…>`; `metars-corpus.json` holds the METARs NOAA
still had of the three VFR flights (its history reaches thirty days back). Two things they taught: IVAO gives the
length of a runway in metres for some airports and in feet for others, and the thresholds are not always where the
paved runway starts.

`metars.json` is small and written by hand, like the first three files: it is the fallback the
weather chain reaches for when the first source has no observation, so it only has to exist.

## The profile, the ratings and the positions (M3, A1)

`users-me-790001.json` is the profile a sign in reads, `/v2/users/me`, recorded on 25 September 2026 through a real sign in
with `node tools/record-ivao-fixtures.mjs --me 790001` — the one endpoint here that only opens to a member's own token. It
keeps the fields the hub reads and nothing else, with the person taken out: the VID is 790001 (the range the training tests
own), the names and the address are placeholders, the staff positions an empty list. Two things are as IVAO sent them: the
`rating` objects, which are IVAO's vocabulary, and the **shape** of `hours` — an array of `{ type, hours }` rows for `pilot`,
`atc` and `staff`, in seconds — whose values are invented (150 hours as a pilot, 120 as a controller). A test that wrote
`"hours": { "atc": 100, "pilot": 200 }` before the field was read had guessed the shape wrong.

`atc-positions-sample.json` and `subcenters-sample.json` are public reference data, recorded the same day with
`node tools/record-ivao-fixtures.mjs --positions sample LIRF LIMC LIRR`: the positions of two airports as `/v2/ATCPositions/all`
answers them, and the sectors of one FIR as `/v2/subcenters/all` does, both without the outline of the sector
(`regionMap`, `regionMapPolygon`). What they taught: **no position carries a rating**, so the type of position a rating is
trained on belongs to the vocabulary of the ratings; the airports' positions are `DEL`, `GND`, `TWR`, `APP`, `DEP` and `ATIS`,
and the sectors (`CTR`, `FSS`) are only in the second endpoint, which dropped the connection twice out of three attempts.

## The ATC positions of the world (M3, A2)

`atc-positions-world.json` and `subcenters-world.json` are what the fixture client answers for the ATC positions, recorded
on 25 September 2026 with `node tools/record-ivao-fixtures.mjs --positions world LIRF LIMC LIBD LFPG LIBG LIRR LIMM LIBB LFFF`:
the positions of the bench's airports and the sectors of their FIRs as IVAO's two answers of the world give them — so a
French airport and a French FIR are in them, for the division to leave out. Grottaglie (`LIBG`) is there because IVAO lists
its approach twice: four callsigns of the world come twice, the same station under two identifiers, and the table keeps one.

The tool now asks with `mapType=regionMapPolygon`, as the hub does. Without it IVAO sends two outlines with every sector,
and the answer of the world takes longer than the fifteen seconds its gateway waits: a `504`, or the connection closed half
way — what A1 saw twice out of three was that, four times out of four on 25 September. With one outline it comes in about
five seconds, and the outline is dropped all the same.

## The ATC bookings of a day (M4, E15a)

`atc-bookings-day.json` is what the fixture client answers for the network's ATC bookings, recorded on 30 September 2026 with
`node tools/record-ivao-fixtures.mjs --bookings day 761070 <day> LIRF LIMC LIBD LIBG LFPG LIRR LIMM LIBB LFFF SBGR_TWR EDDF_APP`:
the bookings `/v2/atc/bookings/daily` listed for one real day that started that day on the stations of the bench's positions, plus
an exam (`EDDF_APP`) and one across midnight (`SBGR_TWR`, 23:00 to 01:00). Ten bookings, five of them on a sector. The people are
taken out: each member became one of the VIDs 761070–761079, the range of phase E15a, and the user object IVAO embeds — names,
division, rating — keeps only that number. What would find a booking, and its member, again through IVAO's own API is taken out
too: the booking's `id` and `createdAt` are left out, and the day is moved onto 1 January 2001, when IVAO has no bookings, with its
times of day. Which day it was is written nowhere.

The fixture client answers **any day** with it, like `whazzup.json` answers any evening: the recorded bookings moved onto the day
asked, and the one across midnight of the day before. So a bench with no credentials has bookings beside an event of any date.
It lists a day the way IVAO does, as measured that day: every booking that touches the day — one across midnight on both days, one
that ends at midnight on the next day too, one that starts at midnight not on the day before — and a position is the start of a
callsign, in any case (`LIRR` is Rome's three sectors).

## The FRAs of the bench's positions (M4, E10c)

`fras-IT.json` is what the fixture client answers for the FRAs of the division, recorded on 30 September 2026 with
`node tools/record-ivao-fixtures.mjs --fras IT LIRF LIMC LIBD LIBG LIRR LIMM LIBB`: 94 of Italy's 353 FRAs of a position, the
ones on the positions of those airports and the sectors of those FIRs (44 positions), each with the position IVAO expands with
it — the only place an FRA carries a callsign. **No person is in it**: the tool asks for the rows of a position only
(`members=false`) and drops one that names a member all the same. What they taught: a position has a minimum for the day and one
for the night (`LIBD_TWR`, AS2 from 08 to 23 and ADC from 23 to 08), or for the weekdays and the weekend (`LIMC_ANE_APP`); a few
are closed to anyone but a CAI (`LIRF_AWL_APP`); the times come as `23:00:00` and the date as `2026-09-12`, not in the shapes
the documentation shows. A country without a file — France, for the bench — is not answered at all, which keeps whatever the
snapshot holds: no file is not "no FRA".
