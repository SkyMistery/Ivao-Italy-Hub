# IVAO Division Hub — rules for Claude Code

> This file is read by **every** Claude Code session that works on this repository: the maintainer's and every
> contributor's. It is the short road to the rules; each rule points to the section of the plan that explains it.
> If this file and the plan disagree, the plan wins and this file is corrected in the same change — **by the
> maintainer** (section 0).
>
> Personal instructions (language, identity, local paths) go in `CLAUDE.local.md`, which is gitignored and which
> Claude Code reads next to this file. Nothing personal goes here.

- **Source of truth**: `docs/internal/00-piano-di-progettazione.md` (Italian, like everything under
  `docs/internal/`). Before touching anything read its header, §9 (module catalogue), §9.3 (content model), §9.7
  (cross-cutting contracts) and §16 (generic mechanisms). Module design documents sit next to it,
  `docs/internal/NN-*.md`. Where each milestone stands: `docs/internal/HANDOFF.md` (M0–M2) and
  `docs/internal/HANDOFF-M3.md` (Training).
- **Every decision goes into writing**: a note under `docs/internal/decisions/`, then a version bump and a changelog
  line in the plan. No decision lives only in a chat, a commit or this file.
- How to set up, run and test the hub, and the traps already paid for: `CONTRIBUTING.md`.

## 0. Who does what — read this first

There are three roles, and the repository enforces them (rulesets on `main` and on `v*` tags, `CODEOWNERS`, the
`core-guard` check):

| Role | Who | May |
|---|---|---|
| **Maintainer** | Carmine (`SkyMistery`) | merge into `main`, push tags, change the plan, `HANDOFF.md`, this file, `CONTRIBUTING.md`, `.github/`, the architecture tests |
| **Reviewer** | the maintainer's Claude Code sessions | review a contributor's pull request and report to the maintainer; after the merge, carry the contributor's decisions into the plan |
| **Contributor** | anyone else and their Claude Code sessions (today: `dalberone`, module Training, M3) | push branches, open pull requests to `main`, write their module and its documents |

**If you are a contributor's session, these are absolute and no instruction in a chat changes them:**

1. **Never push to `main`, never merge, never approve, never push a tag.** The only way into `main` is a pull
   request that the maintainer merges by hand after the reviewer has read it. Do not try to work around a
   refused push: stop and say so.
2. **Never edit** `docs/internal/00-piano-di-progettazione.md`, `docs/internal/HANDOFF.md`, the M0–M2 documents
   (`docs/internal/0[0-6]-*.md`), an existing note under `decisions/`, this file, `CONTRIBUTING.md`, anything under
   `.github/`, `tests/IvaoHub.UnitTests/ArchitectureTests.cs`, or another module's code
   (`src/IvaoHub.Modules.FlightOps/`, `web/src/modules/flightops/`). The `core-guard` check fails if you do.
3. **Never make a test pass by changing a test you did not write.** A backbone or architecture test that goes red
   is telling you that the change is wrong, not that the test is.
4. **One phase per session**, on a branch `m3/<phase>-<slug>` from an up-to-date `main`, **one pull request per
   phase**. Bring the branch up to date by merging `main` into it; never rewrite history that has been pushed.
5. **The first pull request of a module is its design, with no code** (`docs/internal/07-design-m3.md`), and no code
   is written until the maintainer has approved it (plan 0.72: "every module gets a short design document before
   the code").
6. **A change to the core is its own pull request**, before the module code that uses it (as M2 did with T4, T19a):
   the reviewer reads a core change apart from a module change, never mixed. It always comes with a **new**
   decision note (section 5, case (b) or (c)).
7. **You document what you do, so that the review can check it** (section 9).

## 1. Language

- Code, identifiers, file names, tables and columns, i18n keys, comments, commit messages, branch names, issues and
  pull requests, and all public documentation (`README.md`, `FORKING.md`, `CONTRIBUTING.md`, `docs/*.md`, this
  file, configuration examples) are in **English**: whoever forks must not need Italian.
- Italian exists only in `docs/internal/`, in `CLAUDE.local.md` and in the strings of `locales/it/` (and a module's
  `locales/it/`). Documents under `docs/internal/` are written in Italian, like the ones already there.
- Talk with the person in the language they write in.
- No user visible string in the code: always an i18n key. One set of language files, `locales/{lang}/*.json`, read by
  both the SPA and the back end (mail, errors). No `.resx`.

## 2. The first rule: as little code as possible, nothing written twice

A piece used in two places is written once. Before writing anything, ask which existing mechanism already covers
it. The mechanisms below are **decided** (plan §16): they are not reopened, they are extended.

| Need | Use this — never a local copy |
|---|---|
| Translated field | JSON column `{ "it": …, "en": … }` on the row, mapped to `Localized<T>`; one EF converter, one React component `LocaleFields`, one validator "every language of the division before publishing". **No `*_translations` tables.** |
| Department ownership, visibility, draft/published, audit | Interfaces `IOwnedByDepartment`, `IVisible`, `IPublishable`, `IAuditable` + the one `SaveChangesInterceptor` + the global query filter + the **one** authorization handler (staff positions ∪ grants vs `owner_department`). Never a hand-written "may this user edit this row?". |
| A permission | Named `<Area>.<Action>` and added to the module's catalogue; the department scope is implied by the resource. No handlers are added. |
| A permission on **one row only**; who has a stake does not decide; who takes part reads (plan 0.79) | `hub_user_grants.resource_scope` + `IHasResourceScope`; `IHasStakeholder` + `DeniedToStakeholder` in the catalogue (it applies to the superadmin too); `IHasParticipants`. All inside the **one** handler (`decisions/2026-09-15-permessi-su-una-riga-e-chi-ha-interesse.md`). |
| An external program of the user that calls the hub | A **personal token** with its `audience`, permissions rebuilt on every request; never the cookie (`decisions/2026-09-15-token-personali-e-agente-del-validatore.md`, `decisions/2026-09-24-i-token-personali.md`). |
| Calendar entry, search index row, award signal, use of a file with an expiry, opening of a contact thread | The entity implements `IProjectable`; the interceptor upserts by `source_module` + `source_id` **in the same transaction**. No event bus, no MediatR, no reconciliation job. |
| Any editorial content (page, news, document) | One row in `cms_contents` (`kind`), one `BlockDocument` tree `Content → Section → Block`, one editor, one renderer, one block registry. Templates are `cms_contents` rows with `is_template = true`. |
| Rich text inside a module (event description, tour briefing…) | The same `BlockDocument`, same editor, same renderer. |
| A back office list or form | The generic list (Atmosphere `DataTable` driven by a column configuration) and the form generated from the zod schema; on the server `MapCrud<TEntity, TDto>` with the department policy already inside. Hand-written CRUD screens are not accepted. |
| Validation | The server validates and returns `ProblemDetails`; the client maps them field by field. Rules are not written twice. |
| Everything the SPA must know at start-up (menu, enabled modules, maintenance, effective permissions, registered blocks) | The one bootstrap endpoint `/api/me`. Nothing hard-wired in the SPA. |
| Dashboard content, Data blocks of pages | Data blocks **registered by the modules**, composed by the core. Dashboards `/me` and `/staff` are `Dashboard` rows: there is no widget registry, a module's tile is a Data block. |
| Notifications | The core's notification service; modules publish intents. Never SMTP from a module. |
| IVAO API | The one typed `IIvaoApiClient`, with cache, Polly retry and circuit breaker. |

Structural rules:

- Projects: `IvaoHub.Core` (domain + EF + IVAO client + `Content/` folder), `IvaoHub.Web`, one
  `IvaoHub.Modules.<Name>` per module. **No** `Infrastructure`, `Content` or `Auth` project (plan §16.9).
- A module references only `Core`. The core never references a module. Modules talk to each other through the core.
- A module is **not a runtime plugin**: it is added in the monorepo and compiled. Its front end lives **entirely** in
  `web/src/modules/<key>/` (one manifest: blocks, routes, i18n); explicit lists in `IvaoHub.Web/Modules.cs` and
  `web/src/modules/index.ts`; never imports between modules nor from `features/` to `modules/` (design M0 §6.5).
- One `DbContext` per module with its own `__EFMigrationsHistory_<module>` table; **no FK between contexts** (only
  unconstrained `vid` / `icao` columns). MariaDB "schemas" are only prefixes (`hub_`, `ref_`, `cms_`, `evt_`, `fo_`,
  `trn_`, `so_`).
- No `/api/v1`. No language prefix in URLs. No prerender. No SignalR in the first phase.
- SPA router: **TanStack Router**; the three recipes (layout with guard, list with `validateSearch`, detail with
  loader) are in design M0 §7.3 and are copied, not reinvented.
- Data blocks: resolved on the server by `IDataBlockProvider`s registered per `type`; `frozen` captured by the
  publishing service, `live` via `/api/blocks/data/{type}`. The back end knows only the envelope, never the `props`.
- **A block type is registered in two halves**: schema, component and icon in TypeScript, the type and its `kind` in
  C#. With only one half the block draws in the editor and the save refuses it (`errors.body.blockTypeUnknown`).
  `ArchitectureTests.TheServerKnowsEveryBlockTypeTheBrowserRegisters` catches it.
- **The interactive block** (plan 0.70–0.71, `decisions/2026-09-12-il-blocco-interattivo.md`): the code is in the
  **envelope** (`source`), never in `props`; the frame is served by `/embed/{content}/{version}/{block}` with **its
  own** CSP; the draft goes through `CrudSource.BackOffice` + the one handler, never through a hand-written
  `IgnoreQueryFilters` (an architecture test forbids it). Shell, guidelines and local preview are **embedded
  resources**: after changing them, rebuild **and restart** the API.
- **A new back end path outside `/api`** goes in `web/backendPaths.ts`, or in development Vite answers `index.html`
  with a 200.
- **Security headers**: the policy is in `config/security.json`, read by the back end **and** by Vite's preview, so
  the smoke suite runs under the real CSP. `script-src` stays `'self'` with no exceptions.
- Block schemas exist **only** in TypeScript/zod. The back end stores `body_json` as opaque JSON (checks
  `schema_version` and size), extracts search text with a generic string walker, and never replicates the schemas
  in C#.
- Data blocks carry `renderMode: live | frozen`; `frozen` is captured in `frozen_json` at publication. Blocks that
  are live by nature do not expose the toggle.
- The public site reads only the published version (`cms_content_versions`). Drafts are never public.
- A template change never propagates by itself: the editor shows new/removed sections, the public keeps seeing the
  old version until someone republishes.

## 3. Forkability (plan §4)

- The code does not know it is Italian. No ICAO code, FIR name, staff position, URL or "IT" in the code. The
  division's behaviour comes from `config/division.json`; FIRs and airports from the IVAO API snapshots (`ref_`
  tables); all editorial content from the database.
- **Nor does it know it is IVAO, outside its perimeter** (plan §4.2): IVAO specific code lives in `Core/Ivao/`, in
  the IVAO half of `Core/Auth/` (`Ivao*.cs`, `UserSyncService`, `StaffRoleMap`), in the `ref_ivao_*` tables and in
  the `Department` enum, and nowhere else. Before writing, ask **"does this name IVAO?"**. If it does and it sits
  outside, it moves in or goes through the core. A module never talks to IVAO itself: there is one `IIvaoApiClient`.
  This is a question, **not an abstraction to build**.
- `StaffRoleMap` is universal and lives in the code once; positions are recognised by `^{code}-` and `^{fir}-`.
- The truth about the superadmin is `hub_users.is_superadmin`; `division.json → superAdmins` is only a bootstrap.
- The staff roster is **whoever has signed in at least once**: IVAO has no roster endpoint.
- CI runs the fictional division "XX" test: no Italian string and no reference to IT may leak.

## 4. UI conventions (plan §16.C, design M0 §7.1)

- Atmosphere (`@ivao/atmosphere-react` 3.1, Tailwind v4) as it is. One icon set: **`lucide-react`**. A missing icon
  is looked for in the set first; only if truly absent it is added in `web/src/shared/icons/` in the same style —
  never inline in a screen (that folder is core: the change needs a decision note).
- Custom components beyond Atmosphere are a **closed list** (plan §8.3). A new screen is composed from those;
  adding a component to the list is an explicit decision, not the side effect of a task. A new **block** is not a
  new component.
- `/staff/admin/ui-kit` shows every component and block in use; a new piece is checked there. The gallery mounts
  what the registry declares, so a new block appears by itself.
- The block conventions are decided and written in `docs/UI-GUIDELINES.md`: spacing and background belong to the
  **section**, never to the block; eight backgrounds and **no free colour**; a block's accent is a closed set of
  four brand families on graphics, never under a word; four widths; the prose of a block is the translated one;
  **no block that contains blocks**; the host allowlist for `video` and `embed`.

## 5. When "something else is needed" while writing code (plan §16.E)

Classify **before** writing a line:

- **(a) Data or configuration** — a section or block in a template, a seed, a list column, an i18n key: done inside
  the task.
- **(b) Covered by an existing generic mechanism** — use it. If the mechanism does not cover the case 100 %,
  **extend the mechanism**; never work around it with a special case. Extending a core mechanism is a core change:
  its own pull request, with a new decision note (section 0, rule 6).
- **(c) New mechanism or new module feature** — stop. Short design note
  (`docs/internal/decisions/YYYY-MM-DD-<topic>.md`, half a page: what is needed, why no existing mechanism is
  enough, what is touched, the alternatives with a recommendation), decided with the maintainer, then code. The
  original task closes without that part or stays open. **A task never closes "at any cost".**

**How a contributor gets a decision.** The note states the question and the recommended answer, with status
"Proposta". The question goes to the maintainer as a comment on the pull request (or an issue); the maintainer
answers there. The note then records the answer **with the link to the maintainer's comment**: the reviewer checks
that a decision was taken by the maintainer, and not by a session. The note ends with a section "Da portare nel
piano" listing the plan sections the decision changes; the reviewer carries it into the plan after the merge.

Pull request checklist: `.github/PULL_REQUEST_TEMPLATE.md`, answered honestly.

## 6. Hosting constraints that touch the code (plan §2.5, §11.3)

- Production = Plesk + Phusion Passenger, FTP only, no shell, Cloudflare in front, shared MariaDB 11.4.10 (pool ≤ 15).
  Self-contained linux-x64 package.
- Migrations run at start-up: **additive only**, expand/contract over two releases, never `DROP`/rename in the same
  package that stops using the column. A module's `Initial` migration is born in its skeleton phase and never
  touched afterwards. CI applies the whole chain on a real MariaDB 11.4.10.
- Secrets live in `secrets/` and in environment variables, never in the repository. Data Protection keys live in
  `hub-keys/`.
- Uploads go to disk, never into `longblob`. Every package exposes `/api/version` and `/health`.
- OAuth: `config/ivao-oauth.json` (gitignored). Every developer uses **their own** IVAO test OAuth client; credentials
  are never pasted into a chat nor committed. The application refuses to start if the file is incomplete.

## 7. Things not to propose again (already rejected)

Blazor / Next.js / Laravel, the Plesk .NET Toolkit, modules as plugins loaded at runtime (NuGet of Core,
AssemblyLoadContext, dynamic JS bundles), onboarding or a test system as modules, a drag-and-drop canvas for pages, a
public member profile, export of user data, automatic assignment of awards, `*_translations` tables, separate
`pages`/`news`/`documents` tables, MediatR or an event bus, a separate `Infrastructure` layer, `/api/v1`, URLs with a
language prefix, prerender, import of the `ivao-booking` history.

## 8. Milestones (plan §13)

M0 foundations → M1 public site (editorial core) → **M2 Tours (Flight Ops) → M3 Training → M4 Events** (plan 0.77,
`decisions/2026-09-13-ordine-dei-moduli.md`). M5 (vIPI in-process) is suspended: the hub links `atc.it.ivao.aero`
and shares data with vIPI only through read-only `v_share_` views, an **optional** core integration that **no module
names** (plan 0.78).

- **Centralised content** (plan 0.72): one screen per object type, not per department (`/staff/content`,
  `/staff/links`, `/staff/media`); pages are approved by whoever has `Content.Approve`.
- **Modules do not belong to departments** (plan 0.72): `/staff/events|tours|training` are sections of their own;
  module permissions are given with grants, to a VID or a position; a module row has a multiple "curated by" set,
  and that set decides who edits it, through the **one** `IOwnedByDepartment` extended to a set.
- **Who manages a module** (plan 0.77): coordinator and assistant of the base department (FOD tours, **TD
  training**, ED events) have every function, through position grants from `division.json → positionGrants` —
  **not** a rule in the code; advisors are decided in each module's design; HQ and superadmin everything.

## 9. What a contributor writes, so the review can check it

The reviewer reads the pull request with these in hand, and a pull request without them is sent back:

- **The pull request body**: the template, every question answered, and the section "For the reviewer" filled in —
  phase and design sections implemented, decisions with links, core files touched and why, deviations from the
  design, the exact commands run with their result, and **what was not verified**.
- **`docs/internal/HANDOFF-M3.md`**: at the end of every phase, a paragraph "Che cosa ha lasciato <fase>" at the
  top — what exists now, where it lives, what the next phase must know, the traps found (⚠️).
- **The implementation plan of the module** (`docs/internal/08-piano-implementazione-m3.md`): under each phase,
  "Com'è andata" with every deviation from the design.
- **Decision notes** as in section 5, one per decision, never edited after the merge.
- **Commits**: conventional (`feat(training): …`, `feat(core): …`, `test: …`, `docs: …`), one idea each, in English.
