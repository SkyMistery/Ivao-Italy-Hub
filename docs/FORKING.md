# Forking this hub for another division

The code knows nothing about any particular division. There is no ICAO code, no FIR name, no staff
position and no URL hardcoded anywhere: a fork is a matter of configuration and content, not of
editing sources.

> **Status: M0, phase F5 of nine.** The first two customisation points below are real and in use:
> the division file is read and validated at start up, and the language files are the only place a
> user visible string exists — the server reads the same set for the messages it produces itself.
> The third one, the content, waits for the back office screens. This guide is filled in with the
> step by step of a real fork at the end of M0.

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

Filled in at the end of M0, once the module contract is implemented. In short: one
`IvaoHub.Modules.<Name>` project, one `web/src/modules/<key>/` folder, and one line in each of the
two explicit lists (`IvaoHub.Web/Modules.cs` and `web/src/modules/index.ts`).

## What a division never has to touch

The rules that decide who may read and write what are generic, and none of them names a division:
a row belongs to a department, has a visibility, and is filtered and authorised by one mechanism
each. Adding a permission is a name in a catalogue and a line in a matrix; it is never a new
authorization handler, and never a check written again inside a screen.

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
