# Forking this hub for another division

The code knows nothing about any particular division. There is no ICAO code, no FIR name, no staff
position and no URL hardcoded anywhere: a fork is a matter of configuration and content, not of
editing sources.

> **Status: M0, phase F0.** The three customisation points below are the design contract. The files
> they refer to are created in the phases that follow, and this guide is filled in with the real
> steps at the end of M0.

## The three customisation points

1. **`config/division.json`** — behaviour of the division: code, name, languages, default language,
   time zone, whether staff scope follows the FIR, which optional modules are enabled, and the VIDs
   that bootstrap the first super administrators.
2. **`locales/{lang}/*.json`** — every string a user ever sees. Add a language directory, keep the
   same keys as the others, and list the language in `division.json`. `pnpm i18n:check` fails when
   the sets diverge.
3. **The database** — every page, news item, document and link is content, created through the back
   office, never through a code change.

The airspace of the division (FIRs and airports) is not configuration either: it is synchronised
from the IVAO API into the `ref_` tables.

## Adding a module

Filled in at the end of M0, once the module contract is implemented. In short: one
`IvaoHub.Modules.<Name>` project, one `web/src/modules/<key>/` folder, and one line in each of the
two explicit lists (`IvaoHub.Web/Modules.cs` and `web/src/modules/index.ts`).

## What stays yours

The OAuth client credentials of your division, your database, your uploads and your Data Protection
keys. None of them belong in the repository.
