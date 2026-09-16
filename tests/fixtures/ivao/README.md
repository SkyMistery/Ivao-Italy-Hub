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

`metars.json` is small and written by hand, like the first three files: it is the fallback the
weather chain reaches for when the first source has no observation, so it only has to exist.
