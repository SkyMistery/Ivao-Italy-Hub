# Forking this hub for another division

The code knows nothing about any particular division. There is no ICAO code, no FIR name, no staff
position and no URL hardcoded anywhere: a fork is a matter of configuration and content, not of
editing sources.

> **Status: M0 complete (`v0.1.0-m0`).** The foundations and the generic backbone are done and
> proven end to end. What is here works: configuration, sign-in, permissions, the CRUD engine, the
> generated back office screens, pages made from templates and published, the module boundary, and
> the administration screens. What is not here yet is the public site around those pages —
> navigation, news, documents, the search screen and the calendar are M1. Fork it now if you want to
> follow along or build a module; wait for M1 if you want to replace a division website today.
>
> M1 is under way on `main` and is not released: as of 6 September 2026 the media library and the
> form generator's five new kinds of field are there, the rest is not. Everything below describes
> the tag, and holds for `main` too.

## Forking it, start to finish

Ten minutes, and none of it is editing a source file.

**First, take the two copies you are going to fill in.**

```bash
git clone https://github.com/<you>/<your-fork>.git
cd <your-fork>

cp config/division.example.json config/division.json     # versioned: it is behaviour, not a secret
cp config/ivao-oauth.example.json config/ivao-oauth.json # never committed
```

Copy the examples rather than editing the Italian `division.json` that comes with the repository:
the example is the file that explains every field, one comment per key.

**Then fill them in, before the first start.** The application validates both and refuses to come up
if either is incomplete, which is deliberate: an application that cannot behave like your division
should not start and behave like somebody else's.

1. **`config/division.json`.** `code` and `countryId` are yours, `locales` is the languages you
   publish in, `defaultLocale` is the one a reader gets when theirs is not among them, `timezone`
   decides when the nightly jobs run, `icaoPrefixes` is validated at start up so a typo is not
   silent, and **`superAdmins` must be your own VIDs** — see the warning further down. Naming an
   optional module you do not have is a warning at every start rather than an error, so you can keep
   the key for a module you have not merged yet.
2. **`config/ivao-oauth.json`.** The OAuth client of your division, from
   <https://api.ivao.aero>. `LoginUrl` and `RedirectUri` must match, character for character, what
   IVAO has registered for that client — locally `http://localhost:5173/auth/login` and
   `http://localhost:5173/auth/callback`. If the file is incomplete the application names the
   missing field without printing a secret.
3. **Your language, if it is neither English nor Italian.** Copy `locales/en/` to
   `locales/<lang>/`, translate the values, and list `<lang>` in `division.json`. `pnpm i18n:check`
   fails if the key sets have drifted. A language listed with no directory yet does not stop
   anything — readers get the default language until you translate it.

**Now start it.**

```bash
docker compose up -d                    # MariaDB and a fake SMTP server, for development
dotnet run --project src/IvaoHub.Web    # validates, migrates, seeds the templates, pulls your airspace
cd web && pnpm install && pnpm dev      # the single page application
```

**And finish from the browser, not from an editor.**

4. **Sign in for the first time**, at <http://localhost:5173>. Your VID becomes a row in `hub_users`,
   your IVAO positions become departments and levels, and if the table held no super administrator
   at all, the VIDs in `superAdmins` become one. That list is read **once**: after that the database
   is the truth and editing the file achieves nothing. `positionGrants` — permissions your division
   gives to a department at some of its levels — is read **grant by grant**: each one is applied once,
   at the first start that finds it, and a grant you delete from the permissions screen does not come
   back. A grant you add to the file later — because a new module expects it — is applied at the next
   start. The example file carries the grants the tours module expects for its base department.
5. **Translate the seeded templates and pages, or replace them.** `seed/content-templates/*.json`
   and `seed/content-pages/*.json` carry `{ "$t": "seed…" }` rather than sentences, resolved at seed
   time into the languages you listed — so a division that publishes only in Polish gets no English
   text. Each file is applied **once**, remembered by a key in `hub_division_settings`; to change
   what a fresh installation starts with, edit the file before the first start, or edit the page in
   the back office afterwards.

   Your installation therefore comes up with a site rather than with nothing: a home, `/start`,
   `/pilots`, `/atc` and `/about`, published, with filler prose that names no division in
   particular — and one **dashboard per department**, which is the page a department opens its own
   back office on and arranges as it likes.
6. **Write your pages.** `/staff/<department>/content` — new from a template, edit, publish. Nothing
   about a page is in the code.
7. **Put them in the menu.** `/staff/<web department>/menu` is the navigation of your site: a row
   per entry, one level of sub entries, shown to everybody or to members or to staff. Take a row out
   and it leaves the site on the next request — there is nothing to rebuild, because the menu was
   never in the code either.

What you do **not** do at any point: edit a `.cs` or `.tsx` file, add a translations table, write a
screen, or look for the place where the division is hardcoded. There isn't one.

## The three customisation points

1. **`config/division.json`** — behaviour of the division: code, name, languages, default language,
   time zone, whether staff scope follows the FIR, which optional modules are enabled, and the VIDs
   that bootstrap the first super administrators. It is validated before anything touches the
   database: an application that cannot behave like your division refuses to start and says which
   field is wrong, rather than starting and behaving like somebody else's.

   Staff positions need no mapping table of yours either. They are matched as `^{code}-` and read
   with the department codes IVAO itself uses (`HQ`, `SOD`, `FOD`, `AOD`, `TD`, `MD`, `ED`, `PRD`,
   `WD`), so `XX-EC` is the events coordinator of division `XX` the same way `IT-EC` is of `IT`.
   The super administrator list is read once, when the database holds none: after that the database
   is the truth and editing the file on the server achieves nothing.
2. **`locales/{lang}/*.json`** — every string a user ever sees. Add a language directory, keep the
   same keys as the others, and list the language in `division.json`. `pnpm i18n:check` fails when
   the sets diverge. There is one set for the whole product: the browser loads it, and so does the
   server for the few messages it writes itself, such as the title of a validation answer. The API
   never sends prose to the front end — a field that failed validation comes back as the key
   `errors.localized.missing`, and the browser resolves it in the language it is drawing.

   A language listed in `division.json` with no directory yet does not stop the application: the
   reader gets the default language until you translate it.
3. **The database** — every page, news item, document and link is content, created through the back
   office, never through a code change.

The airspace of the division (FIRs and airports) is not configuration either: it is synchronised
from the IVAO API into the `ref_` tables, from your `countryId`, nightly and at the first start.
It needs the OAuth client of your division; while you do not have one, the fixtures under
`tests/fixtures/ivao/` stand in during development (`Ivao:UseFixtures=true`, refused outside it).
If the API is unreachable the last snapshot is kept as it is: a snapshot a day old beats a site
that will not come up.

Since the tours module the snapshot is wider than your own country, because a tour flies anywhere:
the airports of the **world** (about 45 000 rows, with their coordinates), the aircraft types, and
the runways of the airports your tours and reports actually touch, fetched one airport at a time.
Whatever means "the airspace of this division" still reads your `countryId`, so nothing else
changes for you.

Two more things are fetched from outside IVAO, both optional and both named in one folder of the
core each:

- **The weather** (`Core/Weather/`): observations and forecasts for the airports of open tours, and
  for the airports a report touches. No key, no account.
- **The outlines of the flight information regions** (`Core/Airspace/`): a weekly job fills
  `ref_firs` from the VATSpy data project, which is published under **CC BY-SA 4.0**. The file is
  never committed and never served on; what the hub publishes is an answer derived from it, and the
  attribution has to be shown where that answer is shown. If you do not run the job, the table stays
  empty and the feature that uses it degrades to naming airports only, which is a supported state.
  If your fork is commercial, check the licence before switching it on.

## Adding a module

A module is not a plugin loaded at run time: it is added to the monorepo and the application is
recompiled. The boundary is drawn as if it were one anyway, so that nothing in the core ever names
a module and the day it has to become a real plugin the perimeter to extract is already there.

Four things, and the first two are where all of the module's own code lives:

1. **`src/IvaoHub.Modules.<Name>/`** — a project that references `IvaoHub.Core` and nothing else.
   One class implementing `IModule` (start from `ModuleBase`, which makes everything optional):

   ```csharp
   public sealed class RosterModule : ModuleBase
   {
       public const string ModuleKey = "roster";

       public override string Key => ModuleKey;

       public override IReadOnlyList<PermissionDescriptor> Permissions =>
           [new("Roster.View", IsGlobal: false), new("Roster.Edit", IsGlobal: false)];

       public override IReadOnlyList<NavItemDescriptor> PublicNavigation =>
           [new NavItemDescriptor("nav.roster", "/roster")];

       public override void ConfigureServices(IServiceCollection services, IConfiguration configuration) =>
           services.AddModuleDbContext<RosterDbContext>(ModuleKey);

       public override IEnumerable<Type> DbContextTypes => [typeof(RosterDbContext)];

       public override void MapEndpoints(IEndpointRouteBuilder endpoints) =>
           endpoints.MapCrud<Controller, ControllerListDto, ControllerDetailDto, ControllerWriteDto>(
               $"/api/{ModuleKey}/controllers",
               options => { /* … */ });
   }
   ```

   Its permissions join the one catalogue and become policies like the core's; its blocks join the
   one block registry — there is no separate registry of dashboard tiles: a tile of a module is a
   Data block that answers for whoever is looking, placed on `/me`, `/staff` or a department's
   dashboard with the editor; its endpoints live under
   `/api/{Key}` and nowhere else. A context of its own derives from `ModuleDbContext` and is
   registered with `AddModuleDbContext<T>`, which gives it its own `__EFMigrationsHistory_<key>` table
   and attaches the save changes interceptor — audit, the department write guard, the projections and
   the global query filter of who reads what are not something a module opts into. There is never a
   foreign key between two contexts, and never a second authorization handler.

   A module whose department changes something from the interface — a limit, a window — declares
   **settings**: a record with its defaults, a FluentValidation validator and the permission that
   manages them, `Settings => ModuleSettingsDescriptor.Create<RosterSettings, RosterSettingsValidator>("Roster.ManageSettings", new())`.
   The core keeps them in `hub_division_settings` (audited), serves them at
   `/api/modules/{key}/settings` behind that permission on the module's base department, and the
   module's own code reads them with `ModuleSettingsStore.GetAsync<RosterSettings>(key)`. A setting
   added in a later release starts at its default on an installation that saved the others long ago.
   Preferences of a member (the order of a queue) are declared the same way, in `Preferences`.

   A module does not belong to a department. A row of it can be **in the care of several**: it
   implements `IOwnedByDepartment` and declares `public int OwnerDepartmentMask { get; set; }`, which
   becomes its column (`DepartmentMask` turns departments into bits). A permission held on any one of
   those departments is held on the row — to read it, change it, find it in a list — and whoever
   creates one has to include a department they hold the permission on. The division can name a
   **base department** every row of the module always has (below).

2. **`web/src/modules/<key>/`** — all of the module's React code, and no other folder holds any of
   it. `index.ts` exports exactly one `ModuleManifest`: its blocks, its routes and the
   i18n namespaces it brings. A route is public by default; `area: 'staff'` hangs it inside the back
   office, behind the staff guard and the `permission` the route names, and `validateSearch` gives a
   list its typed search parameters. Its language files live in `web/src/modules/<key>/locales/{lang}/`;
   `pnpm i18n:sync` copies them into `locales/`, which is the one set the browser, the back end and
   `pnpm i18n:check` all read, and CI fails if the copies are stale.

3. **One line in `src/IvaoHub.Web/Modules.cs`** and **one in `web/src/modules/index.ts`**. Those two
   lists are the only places a module is named. Nothing is scanned: which modules a build has is a
   question you answer by opening a file.

4. **`config/division.json`**, when there is something to say: an optional module
   (`IsOptional => true`) is switched off with `"modules": { "roster": { "enabled": false } }`, and
   `"baseDepartment": "AOD"` makes every row of the module always in the care of that department,
   whoever else collaborates on it. Silence means on and no base department, so a release that adds
   a module works without every division editing its configuration first. A department module is
   not optional and cannot be switched off.

ESLint keeps the boundary drawn on the front end: nothing under `blocks/`, `features/`, `routes/` or
`shared/` may import from `src/modules`, `app/` may read the list of manifests but not a module's own
files, and no module may import from another. On the back end an architecture test reads the project
files and fails if a module references anything but `IvaoHub.Core`.

A module can be closed for changes without a deploy: `PUT /api/admin/modules/{key}/maintenance`,
behind `Modules.Manage` and with a screen at `/staff/admin/modules`. Reads keep working — a
department reorganising its data wants nobody to change anything, not its pages to go blank — and
every other verb under `/api/{key}` answers 503. A job of the module asks `IsInMaintenanceAsync` at
the top of its run for the same reason.

## The base map of the tours, if you enable them

The tours module draws the legs of a tour on a map, and the map underneath it is **a file of your
installation, not of this repository**: one PMTiles archive of the world, about 180 MB, which this
server serves itself at `/tiles/basemap.pmtiles`. No tile provider, no API key, no quota, and nobody
outside your host learns who is reading a tour page.

```bash
node tools/basemap.mjs
```

That prints the two commands: get the `pmtiles` extractor (one binary, from the Protomaps releases),
and run `pmtiles extract https://build.protomaps.com/<YYYYMMDD>.pmtiles tiles/basemap.pmtiles
--maxzoom=7`. It reads what it needs out of a 130 GB public build over HTTP and writes about 180 MB.
Then put that file in `tiles/` next to the application — in development it is in the repository and
git ignores it; in production it goes over FTP, next to `media/`, and a deployment never overwrites
it. Protomaps keeps roughly a week of daily builds, so pass a recent date.

**You can also skip it.** Without the archive the maps still draw: the legs, the airports and their
codes, on a plain ground — no error, no empty box, no countries. Upload it when you want the world
underneath.

The data is OpenStreetMap's, under the ODbL, and the maps carry the attribution the licence asks for.

## What a division never has to touch

The rules that decide who may read and write what are generic, and none of them names a division:
a row belongs to a department, has a visibility, and is filtered and authorised by one mechanism
each. Adding a permission is a name in a catalogue and a line in a matrix; it is never a new
authorization handler, and never a check written again inside a screen.

## Before it goes live

Two settings are about your server rather than your division, so they live in `secrets/*.json` or in
environment variables, and the application refuses to start in production without them:

- **`AllowedHosts`** — your real host names, `;` separated, without `*`.
- **`ForwardedHeaders:TrustedNetworks`** — the CIDR networks of whatever sits in front of the
  application. Only those senders are believed when they say, through `X-Forwarded-For`, which
  address a request came from; and that address is what the rate limiting on the login counts and
  what the audit log records. Behind Cloudflare, use the ranges Cloudflare publishes; behind a
  reverse proxy on the same machine, `127.0.0.1/32`.

A third file is about your server too, but it ships filled in and enabled: **`config/security.json`**,
the headers every response carries — a content security policy plus `nosniff`, `Referrer-Policy`,
`X-Frame-Options` and `Cross-Origin-Opener-Policy`. Two reasons to touch it, and only two. If your
division frames a host this repository does not know — you added one to the embed allow list in
`web/src/blocks/allowlist.ts` — add its player origin to `frame-src`, or the block will say "allowed"
and the browser will show an empty box; a Vitest fails when those two disagree. And if a policy turns
out to break something on your installation, `contentSecurityPolicy.enabled: false` switches it off by
editing a file, because production is reached by FTP and there is no shell there. Everything else in
it is measured against this application: `web/e2e/security.spec.ts` walks the screens under the real
policy and fails on a single refusal, which is the test to run if you change a directive.

One more thing to change before the first start, and it is easy to miss because it is not a secret:
**`superAdmins` in `config/division.json` still holds the VIDs of the division this repository was
written for**. That list is read once, when the database holds no super administrator at all — so
whoever is in it on your very first start becomes the person who can do everything on your hub. Put
your own VIDs there, or empty the list and add yourself later from the database.

## What stays yours

The OAuth client credentials of your division, your database, your uploads and your Data Protection
keys. None of them belong in the repository.

## The licence

[Apache License 2.0](../LICENSE). You may fork this and run it for your division without asking
anybody; keep the copyright notice and the licence, and say which files you changed. What you write
for your own division — your content, your translations, your modules — is yours.

Keep the [`NOTICE`](../NOTICE) file too, and add your own division under the notices already in it
rather than replacing them: that file is the one thing the licence asks you to carry forward
verbatim, and it stays short for exactly that reason.
