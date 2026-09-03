# IVAO API fixtures

Answers shaped like the ones `/v2/centers` and `/v2/airports/all` give, used when
`Ivao:UseFixtures=true`. They exist because the OAuth client of a division is not necessarily
allowed those endpoints, and a division that forks must still be able to run the hub locally
without credentials at all.

They are written by hand and deliberately small: three FIRs and three airports are enough to prove
that the synchronisation upserts rather than duplicates, and that a FIR position such as `LIRR-CH`
starts being recognised once the snapshot exists. They are **not** a copy of a real IVAO response.
