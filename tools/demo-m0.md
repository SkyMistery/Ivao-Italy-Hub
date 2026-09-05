# Demonstrating M0

M0 is not a feature. It is a backbone — one way to translate a field, one way to decide who may
write a row, one way to expose a resource, one way to draw a list, one way to store a page — and the
only honest way to show a backbone is to use it end to end on something small.

This is that walk-through, from an empty folder to a published page, and it doubles as the
acceptance of the milestone: every step ends with a box to tick, and the boxes are the five points
of the definition of done. It takes about twenty minutes, most of it waiting for `pnpm install`.

Nothing here is scripted, on purpose. A script that passes tells you the script passes; this tells
you the hub works.

> Everything below is also covered by automated tests — the test named at the end of each part is
> the same property, asserted rather than looked at. If a step here fails and its test passes, the
> difference is the interesting part.

---

## What you need

| Tool | Version |
| --- | --- |
| .NET SDK | 10.0.x |
| Node.js | 22 LTS or newer |
| pnpm | 10.x |
| Docker | any recent version |
| An IVAO OAuth client | login URL and redirect URI registered for `http://localhost:5173` |

Without an OAuth client you can still do every part except part 2 — set `Ivao:UseFixtures=true` and
the reference data comes from `tests/fixtures/ivao/`. Parts 3 to 5 need somebody signed in, so with
no client, read them and run the tests of part 7 instead.

---

## Part 1 — From nothing to a running hub

```bash
git clone https://github.com/SkyMistery/Ivao-Italy-Hub.git
cd Ivao-Italy-Hub

cp config/ivao-oauth.example.json config/ivao-oauth.json   # then fill it in
docker compose up -d                                        # MariaDB 11.4.10 and Mailpit
dotnet run --project src/IvaoHub.Web
```

The database is empty and stays that way for about two seconds. Read what the application says on
the way up:

- it validates `config/division.json` and `config/ivao-oauth.json` **before** touching anything.
  Comment out `Ivao.ClientId` and start again: it exits with a non-zero code and names the field,
  rather than starting and failing at the first login;
- it applies its whole migration chain, and says which migrations it applied;
- it seeds the page templates from `seed/content-templates/*.json`, once per file, in the languages
  the division actually speaks;
- it fills `ref_ivao_centers` and `ref_ivao_airports` from the IVAO API because they are empty. For
  `IT` that is 7 centres and 221 airports.

Then, in another shell:

```bash
cd web
pnpm install
pnpm dev
```

Check three things by hand:

```bash
curl -s localhost:5000/health              # 200, and it pings the database
curl -s localhost:5000/api/version         # the version, the commit, the build date
cat diagnostics/startup.txt                # what started, which migrations, which modules — no secret
```

- [ ] **The application configures itself, migrates itself and says so.**

---

## Part 2 — Signing in is the identity of the hub

Open <http://localhost:5173> and use **Sign in with IVAO**. Consent, come back, land on `/me`.

What just happened is worth reading in the database:

```sql
SELECT vid, first_name, is_staff, is_superadmin, locale FROM hub_users;
SELECT vid, position, department, level, fir FROM hub_user_staff_positions;
SELECT vid, LENGTH(access_token_enc), expires_at FROM hub_user_tokens;  -- encrypted, never readable
```

Your IVAO positions became departments and levels — `IT-EC` is the events coordinator, `LIRR-CH` is
a FIR chief, and a position nobody recognises is kept as it is rather than dropped. If your VID is
in `superAdmins` in `division.json` **and the table held no super administrator at all**, you are
one now; editing that file later achieves nothing, because after the first time the database is the
truth.

Now look at what the front end was handed:

```bash
curl -s localhost:5000/api/me | jq            # anonymously: the division, no user
```

Signed in, `/api/me` carries the user, the effective permissions, the navigation, the modules with
`enabled` and `maintenance`, and the registries of widgets and permissions. There is one bootstrap
call and nothing about the hub is hardcoded in the SPA.

- [ ] **Point 1 of the definition of done** — login, the user row, the super administrator
      bootstrap, and a complete `/api/me`.

*Asserted by* `AuthenticationTests`, `UserSyncTests`, `StaffRoleMapTests`.

---

## Part 3 — `links`, the whole backbone on a boring entity

`links` is deliberately the dullest thing in the product. That is what makes it the proof: it is
localised, it belongs to a department, it has a visibility, it is audited, its CRUD is generated,
its screens are generated, and it is projected into the search index — and **none** of that was
written for it.

Go to `/staff`. It opens on the first department you may work in, at `/staff/ed/links` or whichever
yours is.

**Create one.** The form is not a form: it is a zod schema handed to `SchemaForm`. Notice the title
field has a tab per language with an "empty" badge and a "copy from" button — that is
`LocaleFields`, and it is what a `Localized<T>` looks like everywhere.

**Then get it wrong on purpose.** Fill in the title in one language only and save. The server
refuses with `errors.localized.missing` on that field, and the message *names the language that is
missing*, in your language, resolved by the browser. No English sentence crossed the wire.

**Then check the list.** Sort by a column, search a word in your language, page it. All of it is
server-side: `?sort=`, `?q=`, `filter[…]`, and a filter name outside the allow-list is a 400 rather
than a filter silently ignored. Ask for `pageSize=5000` and count the rows: 100.

Now the three things that are the actual point.

**The audit filled itself in.**

```sql
SELECT vid, action, entity, entity_id, at FROM hub_audit_log ORDER BY id DESC LIMIT 5;
```

Nobody wrote that row. `Link` is `IAuditable`, the interceptor does the rest.

**The row was projected in the same transaction.**

```sql
SELECT source_module, source_id, locale, title FROM cms_search_index WHERE source_module = 'links';
```

One row per language, written inside the transaction of the save. Delete the link and look again:
gone.

**A department is a wall, not a filter in a screen.** Sign in as somebody with a coordinator
position in one department and try `/staff/fod/links` — or just call the API:

```bash
curl -s localhost:5000/api/links -H 'Cookie: hub.auth=…'                     # only your departments
curl -s -X PUT localhost:5000/api/links/1 -H 'X-Requested-With: hub' …       # 403 on somebody else's
```

And the wall is not in the endpoint: try it from `dotnet ef` or a script that calls `SaveChanges`
directly and it still throws. The guard is in the interceptor, so forgetting a policy cannot open
a door.

- [ ] **Point 2 of the definition of done** — `links` end to end, with audit, projection,
      the single authorization handler, and no hand-written screen.

*Asserted by* `MapCrudLinksEndToEndTests` (ten tests, five identities), `DomainBackboneTests`.

---

## Part 4 — A page: template, editor, publish, public

Go to `/staff/<your department>/content`.

**New from a template.** Pick one of the three seeded templates, give it a slug, create. What you
get is a deep copy: the sections and blocks of the template are now *yours*, and the template's
rules — which sections are locked, which are required, which blocks a section accepts — are read
from the template by key, live. A template that changes tomorrow does not rewrite your page. That is
the rule, not a limitation.

**Edit it.** Sections and blocks on the left, the properties of whatever is selected on the right —
and those properties are `SchemaForm` again, drawn from the block's zod schema. The backend has
never seen that schema and never will: it stores `body_json` as an opaque document, checks the
envelope, and extracts the strings for search with a generic walker.

Add a **`linkList` block** and point it at the department you made the link in. A Data block starts
`live`; set its render mode to **captured** (`frozen`) in the properties panel on the right.

**Preview it.** The preview uses the same renderer the public site uses — not a second one. The
block shows a badge saying it is live now and will be captured when you publish.

**Try to publish it half translated.** It refuses, with a path per problem, and says which language
is missing where. Both halves of the rule are checked: the title, and every translated value inside
the block properties.

**Publish it.**

```sql
SELECT id, content_id, version, published_at FROM cms_content_versions;
```

**Now the part worth watching.** Open `/<your-slug>` in a private window — anonymous, no cookie.
The page renders. Then:

1. add another link in the back office, in the same department;
2. reload the public page. **Nothing changes.** The block was captured on the day you published and
   still says what it said;
3. go back to the editor, switch the block to `renderMode: live`, publish again;
4. reload. **Now it changes**, and it will keep changing.

That is the whole of "frozen versus live", and it is the reason the capture lives in the version
rather than in the draft.

One more thing to try, because it is the property nobody thinks to check: make a link **visible to
staff only**, put it in a `frozen` block, publish a **public** page, and look at the anonymous
render. It is not there. A capture can never be more visible than the page that carries it.

- [ ] **Point 3 of the definition of done** — created from a template, edited, published, read
      publicly from the published version only, with a Data block captured.

*Asserted by* `ContentEndToEndTests` (nine tests), `VisibilityCeilingTests`.

---

## Part 5 — The core composes modules, and administers itself

Go to `/staff/admin/modules`. `atc` is there, with its department and whether it is on.

**Close it for maintenance.** Then:

```bash
curl -s -o /dev/null -w '%{http_code}\n' localhost:5000/api/atc/ping                          # 200: reads pass
curl -s -o /dev/null -X POST -H 'X-Requested-With: hub' -w '%{http_code}\n' localhost:5000/api/atc/ping   # 503
```

Reads keep working and writes answer 503. Note what the second line actually proves: `atc` has no
`POST /ping` at all — it has one endpoint and it is a `GET`. Open the module again and that same
call answers 405. The maintenance middleware sits **before** routing, so "closed" applies to the
whole `/api/atc` prefix rather than to the list of addresses the module happens to have, which is
the property that matters: a department reorganising its data wants nobody to change anything, and
it does not want its pages to go blank.

Open the module again, then look at the two other administration screens. `/staff/admin/permissions` is the
same list and the same form engine as `/staff/ed/links`, with no department in the address — that
similarity *is* the point of having one engine. `/staff/admin/audit` is the same engine again, read
only.

**Hand somebody a permission** and watch what "at once" really means: their old cookie is
**refused** on the very next request, they sign in again (silently, with IVAO, since they have
already consented), and they come back with the new permission. Rebuilding the session instead of
refusing it would be the weaker of the two.

**Search what you published:**

```bash
curl -s 'localhost:5000/api/search?q=<a word from your page>' | jq
```

Anonymous, and filtered by exactly the same visibility rule as everything else — the index rows
carry an owner and a visibility, so the global filter restricts them like any other table. Words
shorter than three letters return nothing: that is InnoDB, not us.

Finally, `/staff/admin/ui-kit`. Every custom component and every block the hub can draw, on one
page, plus a section at the top that compares what the server declares with what this build
registered and says so in words.

- [ ] **Point 5 of the definition of done** — `atc` exists as a minimal `IModule` and the core
      composes registered contributions without naming any of them.

*Asserted by* `ModuleAndAdminEndToEndTests`, `ModuleCompositionTests`, `SearchEndpointTests`,
`web/src/modules/manifest.test.ts`.

---

## Part 6 — The hub is not Italian

The code knows nothing about any division. To see it rather than believe it, the test suite starts a
second installation of the hub against a fictional division `XX`, on a database of its own, with
`{ code: "XX", locales: ["en"], superAdmins: [] }` and the whole migration chain run from zero. It
then calls `/api/me`, `/` and `/staff/admin/ui-kit` and asserts that no answer contains `IT-`,
`LIRR`, `Italia`, `Italy` or `it.ivao.aero`, and that the seeded templates carry the key `en` and
nothing else.

```bash
dotnet test --project tests/IvaoHub.IntegrationTests --configuration Release \
  --filter-class '*ForkabilityXxDivisionTests'
```

Name the project rather than the solution: a class filter that matches nothing in the unit test
project makes the whole run exit non-zero, which looks like a failure and is not one.

You can do the same by hand: point `IVAOHUB_ROOT` at a folder holding your own `config/`,
`locales/en/` and `seed/`, and start the application there.

- [ ] **The forkability test passes** (part of point 4).

---

## Part 7 — And all of it, asserted

```bash
dotnet build IvaoHub.sln                    # also writes artifacts/openapi/IvaoHub.Web.json
dotnet test --solution IvaoHub.sln --configuration Release

cd web
pnpm lint && pnpm format:check && pnpm typecheck && pnpm test && pnpm i18n:check && pnpm build
pnpm gen:api && git diff --exit-code        # the generated client must not move

pnpm e2e:install                            # once: the browser the smoke suite drives
pnpm e2e                                    # Chromium against the production build
```

Run them in **Release**. On Windows, `dotnet test` in Debug has been seen to report "Zero tests ran"
with exit code 5 while the test binary run by hand passes everything; Release is how CI runs and
Release is what to trust.

Expect **353 .NET tests** (253 unit, 100 integration against a real MariaDB 11.4.10 in a container),
**76 Vitest** and **9 Playwright smokes**. None is skipped, and the backbone tests of the design are
among them.

The smoke suite is the youngest and has the shortest story: `v0.1.0-m0` was first tagged on a build
that did not open in a browser at all — a missing provider took down every screen behind a layout,
while every one of the other tests stayed green, because they mounted pieces and nothing mounted the
tree. Three tests in a real browser now stand between that and a release.

- [ ] **Point 4 of the definition of done** — the backbone tests pass, and so does the
      fictional division.

---

## The package

```bash
dotnet publish src/IvaoHub.Web -c Release -r linux-x64 --self-contained -o artifacts/publish
```

Look at what travelled with it — this is the same tree the release workflow zips: the binaries,
`wwwroot/` with the built SPA,
`locales/{lang}/*.json` at the root — the copy the server itself reads — `seed/content-templates/`,
the `config/*.example.json` files, and `LICENSE` and `NOTICE`. Everything an installation owns —
`config/division.json`, `config/ivao-oauth.json`, `secrets/`, `hub-keys/` — is deliberately **not**
in there, so a deployment never overwrites a configuration or a set of keys.

- [ ] **The published package is a whole installation minus its secrets.**

---

## Definition of done, in one place

Design §0.1, ticked against the parts above:

| # | | Shown in |
| --- | --- | --- |
| 1 | Login with IVAO, the user row, the super administrator bootstrap, `/api/me` complete with menu, permissions, modules and registries | Part 2 |
| 2 | `links` end to end: `Localized<T>`, department, visibility, audit by the interceptor, CRUD from `MapCrud`, generated list and form, one authorization handler, projected in the same transaction | Part 3 |
| 3 | A `cms_contents` page created from a seeded template, edited, published, with a `linkList` captured in `frozen_json`, and rendered publicly from the published version only | Part 4 |
| 4 | The backbone tests pass and the fictional division XX passes | Parts 6 and 7 |
| 5 | `atc` exists as a minimal `IModule` and proves the core composes registered contributions | Part 5 |

If every box above is ticked, M0 is done — which means news, documents and the rest of M1 are meant
to be configuration far more than they are code. That claim is the real output of this milestone,
and this document is how you check it before believing it.
