# Contributing

The rules are in [`CLAUDE.md`](CLAUDE.md) — read its section 0 first: who may merge, what a contributor never
touches, and how a decision is taken. This file is the practical half: how to work a phase, how to test it, and the
traps this project has already paid for once. How to install and run the hub is in the README
([Running it locally](README.md#running-it-locally), [Checks](README.md#checks)) and is not repeated here.

## Working a phase

1. **Start from an up-to-date `main`**, on a branch with the milestone's prefix (`m3/<phase>-<slug>` for Training,
   `m4/e<N>-<slug>` for Events), or from the branch of the previous phase if its pull request is still open (see
   "Phases in a queue" below). Check `gh pr list` first: another session may be working on the same files.
2. **Read before writing**: the module's handoff (`docs/internal/HANDOFF-M3.md`, `docs/internal/HANDOFF-M4.md`: where
   things stand), the module design, the phase in the implementation plan (`08-piano-implementazione-m3.md`,
   `10-piano-implementazione-m4.md`), and the plan sections they point to. Read the plan before calling anything in the existing
   code a defect: many things that look odd are decisions with a note.
3. **Classify before writing** (CLAUDE.md section 5). If the phase needs the core to change, stop: that is a note, a
   question to the maintainer, and a separate pull request.
4. **Open the pull request early as a draft** if you want CI to run; mark it ready when the phase is done.
5. **Keep the branch current** by merging `main` into it, then **build and run the tests again**: a clean textual
   merge proves nothing here, because start-up guards and generated files interact. Never force-push a branch that
   is under review.
6. **Done** means: CI green (`build-test` and `core-guard`), the template's "For the reviewer" filled in,
   the module's handoff updated. Then the master (the maintainer's reviewing session, CLAUDE.md section 0) reads it and
   posts its findings; when it is ready, the maintainer gives the go and the master merges it — or sends it back.
   If your branch must catch up with `main`, the master asks you on the pull request: it never pushes to your branch.

### Phases in a queue

You do not wait for the merge of one phase to start the next. The master can read several phases in one go, and merges
them in order on the maintainer's go.

- **Branch** the next phase from the branch of the previous one (`git switch -c m3/<next> m3/<previous>`; `m4/` for
  Events), still one phase per session and one pull request per phase.
- **The pull request always targets `main`**, never another branch: that way CI and `core-guard` run on it, and
  nothing has to be retargeted before the merge. Until the phase below is merged its diff also shows the commits
  below; that is expected.
- **Open it ready, not as a draft**, with `(after #N)` at the end of the title and `Queued after #N.` as the first
  line of the body, where #N is the pull request of the phase below. The master keeps the order: it never proposes a
  pull request for merging while its #N is open. In "For the reviewer", name the range that is this phase's own:
  `git diff m3/<previous>...m3/<next>` (or `m4/…`).
- **A fix asked on a phase below** goes on that phase's branch, and then you merge that branch into every branch
  above it, in order. Merge, never rebase.
- **When #N is merged**: nothing to do unless the master asks. The master checks that the next pull request now shows
  only its own phase against `main`, has no conflict and a green CI, and proposes it to the maintainer; it removes
  `(after #N)` itself when it says so. It asks you to merge `main` into the branch (then build and test again) only
  when there is a conflict, or when `main` changed something the phase relies on (note
  `docs/internal/decisions/2026-09-30-la-coda-senza-bozze.md`).
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
- **Reserved for the Events module**: VIDs `761001–761099` in integration tests, slugs starting with `evt-test-`.
  Grep a VID before using it (`grep -rn "<vid>" tests/`).
- **The contacts tests assert the exact recipients of MD and seed an events coordinator with an address**
  (`ContactsAndNotificationsTests`, position `IT-EC`), so an Events test seeds no ED or MD staff with an email address: it gives
  permissions with grants to a VID, and a position, when one is needed, without an address.
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
- The bench signs in as five people (`web/scripts/e2e-server.mjs`, `src/IvaoHub.Web/E2E/E2ESignIn.cs`):
  - by default the **web coordinator** (VID 999001, `IT-WM`), who reaches every department and holds every permission
    of every module;
  - `/e2e/signin?as=pilot` (999002), a member with a Mailpit mailbox, ratings and hours: the pilot of the tours and the
    trainee of the training;
  - `?as=assistant` (999003, `IT-FOAC`), the assistant coordinator of the tours' department, no mailbox;
  - `?as=trainer` (999004, `IT-T01`), a trainer of the training department with a mailbox and the highest ratings;
  - `?as=events` (999005, `IT-EC`), the coordinator of the events department, no mailbox: the events' permissions come
    only from the division's position grants.

  A spec that proves what a department may do signs in as that department's person, never as the web coordinator, who
  would pass with any grant. Mail is read from Mailpit on `http://127.0.0.1:8025`.
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
- **A module that projects into the search names its kind in its own language file**: `search.kinds.<kind>` in
  `web/src/modules/<key>/locales/{lang}/<key>.json` (then `pnpm i18n:sync`). The badge of a search result asks the module
  that projected the row, never the core's `common.json`; `web/src/modules/manifest.test.ts` fails when the word is missing.
- **Nothing but sections inside the `@container` of `ContentRenderer`.** An extra element there (the "add a section"
  invitation was one) can leave the editor's preview empty on a slow machine: Chromium gives the sections no box when the
  typefaces finish loading just after the first draw (0.6.5, `e2e/full/preview.spec.ts`).
- **An e2e spec says what it puts back with `afterwards(…)`, never in a `finally`.** A `finally` that deletes runs after
  the test's time is over, and the report shows the delete timing out instead of the step that stopped. The fixture of
  `web/e2e/full/bench.ts` (#227) runs as a teardown, with its own time; an `afterEach` does too.
- **A grant written signs its holder out** (the security stamp changes): tests and specs sign in again.
- **Module routes**: name a module route parameter `$id`; `useParams({ strict: false })` only types parameters the
  generated tree knows.
- **The `react-hooks/refs` lint** shouts on every property read of an object a custom hook returns with ref setters in
  it: destructure at the call site.
- **Playwright**: `ConfirmDialog` has role `alertdialog`; a `SchemaForm` select keeps its value after submit, so
  scope assertions to the row; list pages add `?page=1…` to their URL.
- **A start skips the migrations and the seeds when nothing changed** since the last start that ran them: the same
  build, the same division options, the same `seed/` (the row `startup.initialised` of `hub_division_settings`).
  Something moved in the database by hand while the code stayed the same (`dotnet ef database update <older>`, a
  template setting deleted to seed it again) is not seen: delete that row too, and the next start does everything.
  `diagnostics/starts.txt` says which of the two a start did.
- **A MariaDB deadlock is not a `DbUpdateException`**: EF reports it as an `InvalidOperationException` ("likely due to
  a transient failure") with the `MySqlException` two levels down, so a `catch (DbUpdateException)` lets it through. Walk
  the chain for `MySqlErrorCode.LockDeadlock`. Two inserts of the same key meet as one whenever the row was just deleted
  and not purged yet. To wait for a lock in a test, poll `information_schema.INNODB_TRX` no more often than every
  100 ms: read faster, InnoDB keeps answering from its cache.
- **Quartz cron expressions run in local time.**
- **CI does not run `dotnet format`** on the whole solution; format the files you touch
  (`dotnet format --include <files>`).
