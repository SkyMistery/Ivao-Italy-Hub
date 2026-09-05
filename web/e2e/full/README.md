# The round, against the real API

Two browser suites live in this repository and they answer different questions.

|                     | `pnpm e2e` (`e2e/*.spec.ts`)                                           | `pnpm e2e:full` (`e2e/full/*.spec.ts`)                  |
| ------------------- | ---------------------------------------------------------------------- | ------------------------------------------------------- |
| What it watches     | the front end assembling itself                                        | the product doing its job end to end                    |
| The API             | stubbed in `e2e/fixtures.ts`; every other `/api` call fails on purpose | real, with a real database                              |
| What serves the SPA | `vite preview`                                                         | the published application, through its own SPA fallback |
| How long            | seconds                                                                | a publish plus seconds                                  |

The second one exists because until M1 the sentence "a member of staff opens the editor, adds a
block and publishes" had never been executed in a browser even once — the first debt of
`docs/internal/HANDOFF.md` §10, and the first phase of M1 by design (`03-design-m1.md` §11.1).

## Running it

You need Docker running, because the bench needs MariaDB:

```bash
docker compose up -d mariadb
```

Then, from `web/`:

```bash
pnpm e2e:full
```

That publishes the application into `artifacts/e2e-bench/`, starts it on
<http://127.0.0.1:5080>, waits for `/health`, and runs the specs. Publishing takes a minute or
two; while iterating on the specs themselves, reuse the last one:

```bash
E2E_SKIP_PUBLISH=1 pnpm e2e:full
```

## What the bench is

- **The published application**, one origin for the API and the SPA, so a deep address such as
  `/staff/wd/content` is served by `MapFallbackToFile` exactly as in production. This is not a
  detail: verifying the M0 package by hand produced four red tests against a perfectly healthy
  build, because the static server used to serve it answered 404 to every deep address. One of the
  specs here asserts that 200 first, so a failing run says which side the problem is on.
- **Its own database** (`ivaohub_e2e`), never the one you develop against. The round writes real
  rows and does not clean up after itself: each run creates its own page.
- **Its own environment**, `E2E`, which is the only place `POST /e2e/signin` exists. It signs the
  caller in as a made up member of staff — a coordinator of the web department — with the same
  application cookie a real IVAO login writes. The environment name is one lock and
  `E2E:Enabled` is the other; the flag anywhere else stops the application (`HubConfiguration`).
- **No IVAO credentials**: the reference data comes from `tests/fixtures/ivao/`.

What it is not: a `--self-contained --runtime linux-x64` package, which is what a release ships and
what a developer on Windows cannot run. Same layout, same fallback, same language files; different
runtime packaging. The CI keeps its own check on the shipped package.

## Writing a spec here

- The moves — signing in, choosing in a select, writing a translated field — are in `bench.ts`.
  Anything that drives the application belongs there; anything that says what the product should do
  belongs in the spec, where it can be read.
- Assert what actually distinguishes the two states. The first version of the draft spec asserted
  the absence of a heading that is not on a public page in either case, and stayed green when the
  draft was deliberately published to check it. **Break the product's promise on purpose and watch
  the test go red**; a test that passes either way is not a test.
- Text comes from `../locales.ts`, which reads the division's own language files. A spec carrying
  its own copy of a sentence passes while the screen shows a raw key.
