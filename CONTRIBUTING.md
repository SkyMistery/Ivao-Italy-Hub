# Contributing

The rules are in [`CLAUDE.md`](CLAUDE.md) — read its section 0 first: who may merge, what a contributor never
touches, and how a decision is taken. This file is the practical half: how to work a phase, how to test it, and the
traps this project has already paid for once. How to install and run the hub is in the README
([Running it locally](README.md#running-it-locally), [Checks](README.md#checks)) and is not repeated here.

## Working a phase

1. **Start from an up-to-date `main`**, on a branch `m3/<phase>-<slug>`, or from the branch of the previous phase if
   its pull request is still open (see "Phases in a queue" below). Check `gh pr list` first: another session may be
   working on the same files.
2. **Read before writing**: `docs/internal/HANDOFF-M3.md` (where things stand), the module design, the phase in the
   implementation plan, and the plan sections they point to. Read the plan before calling anything in the existing
   code a defect: many things that look odd are decisions with a note.
3. **Classify before writing** (CLAUDE.md section 5). If the phase needs the core to change, stop: that is a note, a
   question to the maintainer, and a separate pull request.
4. **Open the pull request early as a draft** if you want CI to run; mark it ready when the phase is done.
5. **Keep the branch current** by merging `main` into it, then **build and run the tests again**: a clean textual
   merge proves nothing here, because start-up guards and generated files interact. Never force-push a branch that
   is under review.
6. **Done** means: CI green (`build-test` and `core-guard`), the template's "For the reviewer" filled in,
   `HANDOFF-M3.md` updated. Then the master (the maintainer's reviewing session, CLAUDE.md section 0) reads it and
   posts its findings; when it is ready, the maintainer gives the go and the master merges it — or sends it back.
   If your branch must catch up with `main`, the master asks you on the pull request: it never pushes to your branch.

### Phases in a queue

You do not wait for the merge of one phase to start the next. The master can read several phases in one go, and merges
them in order on the maintainer's go.

- **Branch** the next phase from the branch of the previous one (`git switch -c m3/<next> m3/<previous>`), still one
  phase per session and one pull request per phase.
- **The pull request always targets `main`**, never another branch: that way CI and `core-guard` run on it, and
  nothing has to be retargeted before the merge. Until the phase below is merged its diff also shows the commits
  below; that is expected.
- **Open it as a draft**, with `(after #N)` at the end of the title and `Queued after #N.` as the first line of the
  body, where #N is the pull request of the phase below. A draft cannot be merged, so the order cannot go wrong.
  In "For the reviewer", name the range that is this phase's own: `git diff m3/<previous>...m3/<next>`.
- **A fix asked on a phase below** goes on that phase's branch, and then you merge that branch into every branch
  above it, in order. Merge, never rebase.
- **When #N is merged**: merge `main` into the next branch, build and run the tests again, remove `(after #N)`, and
  mark the pull request ready. The master checks that its diff is now only its own phase.
- **A phase that needs an answer from the maintainer** (a case (c) note, a core change still under review) does not
  queue on top of the question: the parts that depend on it wait for the answer.

## Your own IVAO OAuth client

Use a test OAuth client of your own — not the maintainer's — in `config/ivao-oauth.json` (gitignored; copy
`config/ivao-oauth.example.json`, and see the README for `LoginUrl` and `RedirectUri`). Never paste its secret into a
chat, a commit, an issue or a pull request.

## Tests

- **Everything runs locally before pushing**: the build, the unit tests, the **whole** integration suite with no
  filter, `pnpm lint`, `pnpm typecheck`, `pnpm test`, and `pnpm e2e:full` when the change has a screen. The smoke
  suite alone has gone green while CI went red on the full round.
- **Reserved for the Training module**: VIDs `790001–790099` in integration tests, slugs starting with `trn-test-`.
  Grep a VID before using it (`grep -rn "<vid>" tests/`).
- **Integration tests share one MariaDB**, and xUnit orders the classes differently on your machine and in CI. So:
  seed staff of a department nobody asserts exactly (the contacts tests assert the exact recipients for AOD, ED,
  FOD, MD and **TD** — seeding a TD coordinator in a Training test can break them in CI only); make uploaded bytes
  unique; give slugs a unique stem and query with it; assert "some, then no more" rather than "exactly one".
  Run a new permission test class **alone** too: a VID another class seeds as superadmin can make it pass for the
  wrong reason.
- **The integration sign-in does not write `last_login_at`**: a test that needs it (personal tokens, for one) seeds
  it.
- **The e2e bench database (`ivaohub_e2e`) survives between local runs.** A spec that creates a row takes it back in
  a `finally` and removes its own leftovers at the start. When an unrelated spec suddenly fails, count the leftovers
  before suspecting your change; `DROP DATABASE ivaohub_e2e; CREATE DATABASE ivaohub_e2e;` and the migrations rebuild
  it. Never run a throwaway cleanup spec in parallel with the real one.
- The bench signs in as other people with `/e2e/signin?as=pilot` and `?as=assistant`; mail is read from Mailpit on
  `http://127.0.0.1:8025`.
- **No call to IVAO or any external service in a test**: record fixtures (`tools/record-ivao-fixtures.mjs`) with a
  real token in development, and test against them.
- `dotnet test` sometimes reports "zero tests ran". Run the test executables directly instead:
  `tests/IvaoHub.IntegrationTests/bin/Debug/net10.0/IvaoHub.IntegrationTests.exe` (xUnit v3, `-class` or
  `-method "*Name*"` to filter).

## Generated files are committed

CI fails on a diff after regenerating them, so regenerate before pushing:

- `pnpm gen:api` after changing an endpoint or a payload (`web/src/shared/api/schema.d.ts`, from the OpenAPI document
  the .NET build writes).
- `pnpm i18n:sync` after changing a module's language files: the module keeps them in
  `web/src/modules/<key>/locales/`, and the copy in the root `locales/` is what the back end and the checks read.
- The route tree (`web/src/routeTree.gen.ts`) is written by the Vite plugin.

## Traps already paid for

- **The running API locks the build output.** `dotnet build` fails with MSB3027 while `IvaoHub.Web` runs: stop it,
  build, start it again. Embedded resources (the interactive block's shell) reach the site only after a rebuild
  **and** a restart.
- **Docker must be running** for the integration tests (Testcontainers) and for `pnpm e2e:full`.
- **Windows shells**: heredocs with non-ASCII characters (`§`, `—`, accents) truncate silently, and backticks inside
  `node -e "…"` are run as commands, leaving holes in the file. Write files with an editor or a script file.
- **`Localized<T>` serialized into a column** needs `LocalizedJsonConverterFactory`; only HTTP has it registered.
- **Server-side i18n keys**: in C# a module's key is asked with its namespace, as the browser asks it —
  `flightops:threads.x`, never `threads.x`. A bare module key answers only while no other module declares it, and then
  the catalogue silently answers with the key itself; `ArchitectureTests.AModuleKeyIsAskedWithItsNamespaceOnTheServer`
  refuses it. The core's keys, and the mails of the notification types (`mail.{type}`), stay bare.
- **A new block bumps two counts** written out in tests (`uiKit.test.ts` and `DataBlockEndToEndTests`), and a block
  needs both halves (TypeScript and C#) in the same pull request.
- **A module with a public page reserves its first URL segment** (`IModule.ReservedSegments`), or a page with that
  name becomes unreachable.
- **A grant written signs its holder out** (the security stamp changes): tests and specs sign in again.
- **Module routes**: name a module route parameter `$id`; `useParams({ strict: false })` only types parameters the
  generated tree knows.
- **The `react-hooks/refs` lint** shouts on every property read of an object a custom hook returns with ref setters in
  it: destructure at the call site.
- **Playwright**: `ConfirmDialog` has role `alertdialog`; a `SchemaForm` select keeps its value after submit, so
  scope assertions to the row; list pages add `?page=1…` to their URL.
- **Quartz cron expressions run in local time.**
- **CI does not run `dotnet format`** on the whole solution; format the files you touch
  (`dotnet format --include <files>`).
