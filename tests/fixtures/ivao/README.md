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
