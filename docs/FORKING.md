# Forking this hub for another division

The code knows nothing about any particular division. There is no ICAO code, no FIR name, no staff
position and no URL hardcoded anywhere: a fork is a matter of configuration and content, not of
editing sources.

> **Status: M0, phase F8 of nine.** All three customisation points below are now real. The division
> file is read and validated at start up. The language files are the only place a user visible
> string exists — the server reads the same set for the messages it produces itself, the front end
> reads it for every screen, and CI fails when the two languages drift apart or when the code asks
> for a key neither of them has; the legal footer is in there too, so a fork changes those links by
> translating a file. And the content is content: pages live in the database, the templates a fresh
> installation starts with are seeded from `seed/content-templates/*.json` in the languages the
> division actually speaks, and no sentence of any of it is in the code. This guide is filled in
> with the step by step of a real fork at the end of M0.

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
       public override Department? Department => IvaoHub.Core.Division.Department.AOD;

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
   one block registry; its widgets join the one widget registry; its endpoints live under
   `/api/{Key}` and nowhere else. A context of its own is registered with `AddModuleDbContext<T>`,
   which gives it its own `__EFMigrationsHistory_<key>` table and attaches the save changes
   interceptor — audit, the department write guard and the projections are not something a module
   opts into. There is never a foreign key between two contexts, and never a second authorization
   handler.

2. **`web/src/modules/<key>/`** — all of the module's React code, and no other folder holds any of
   it. `index.ts` exports exactly one `ModuleManifest`: its blocks, its widgets, its routes and the
   i18n namespaces it brings. Its language files live in `web/src/modules/<key>/locales/{lang}/`;
   `pnpm i18n:sync` copies them into `locales/`, which is the one set the browser, the back end and
   `pnpm i18n:check` all read, and CI fails if the copies are stale.

3. **One line in `src/IvaoHub.Web/Modules.cs`** and **one in `web/src/modules/index.ts`**. Those two
   lists are the only places a module is named. Nothing is scanned: which modules a build has is a
   question you answer by opening a file.

4. **`config/division.json`** if the module is optional (`IsOptional => true`): a division switches
   one off with `"modules": { "roster": false }`. Silence means on, so a release that adds a module
   works without every division editing its configuration first. A department module is not
   optional and cannot be switched off.

ESLint keeps the boundary drawn on the front end: nothing under `blocks/`, `features/`, `routes/` or
`shared/` may import from `src/modules`, `app/` may read the list of manifests but not a module's own
files, and no module may import from another. On the back end an architecture test reads the project
files and fails if a module references anything but `IvaoHub.Core`.

A module can be closed for changes without a deploy: `PUT /api/admin/modules/{key}/maintenance`,
behind `Modules.Manage` and with a screen at `/staff/admin/modules`. Reads keep working — a
department reorganising its data wants nobody to change anything, not its pages to go blank — and
every other verb under `/api/{key}` answers 503. A job of the module asks `IsInMaintenanceAsync` at
the top of its run for the same reason.

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
