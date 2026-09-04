# UI guidelines

How screens are built in this hub. Four rules, and each is enforced by something that fails a build
rather than by a reviewer remembering it.

## 1. No user facing string in the code

Every sentence, label and button comes from `locales/{lng}/*.json`, which the SPA and the backend
both read. There is one set of language files, not one per side.

`pnpm i18n:check` fails when the languages of the division do not carry the same keys, and when a
key the code asks for by name does not exist in all of them.

Keys built at runtime — `` t(`${labels}.fields.${path}`) `` in the form generator — cannot be
checked that way. What keeps those honest is a test that renders the component and reads the text
back; if you add a dynamic key, add the test with it.

The server never sends prose in the machine readable part of an answer either: `errors[field]` is a
list of i18n keys, and `useProblemDetails` is the one place that resolves them.

## 2. Icons come from lucide

`lucide-react` ships with Atmosphere, so the set is already there.

If an icon is genuinely missing, add it to `web/src/shared/icons/<Name>.tsx` — a 24×24 SVG,
`stroke-width` 2, `currentColor` — and export it from `shared/icons/index.ts`.

Never inline an `<svg>` in a screen. ESLint refuses one anywhere outside `shared/icons/` and
`blocks/`, where a block may draw its own decoration.

## 3. The custom components are a closed list

For M0 it is exactly:

`Hero`, `SectionHeader`, `StatTile`, `PageShell`, `EmptyState`, `LocaleSwitcher`, `LocaleFields`,
`MarkdownContent`, `DataList`, `SchemaForm`, `ProblemAlert`, `DepartmentBadge`, `VisibilityBadge`,
`StatusBadge`, `ConfirmDialog`.

The list lives in `web/src/shared/ui/catalog.ts`. Everything else is Atmosphere.

A screen of a feature — the section tree of the content editor, the template picker — is not on the
list and does not belong on it: the list is the pieces that are meant to be reused, and a component
that only one feature has any use for lives in `features/<x>/` where it can change without anybody
else noticing. The question to ask is "would a second screen mount this?", not "is it a component?".

Adding one is a decision: write it in `docs/internal/decisions/`, add it to the catalogue, add a
section to `/staff/admin/ui-kit`, and add a line to this file saying what it is for. The test next
to the gallery fails until the section exists, so a component cannot quietly stop being shown.

When Atmosphere nearly does what is needed, wrap it rather than replace it — `DataList` is
Atmosphere's `DataTable` in server side mode, with the paging drawn by us because Atmosphere's own
writes "Rows per page" in English. A component that does something genuinely new is a decision.

`LiveStatusStrip`, `RatingBadge`, `AirportCard`, `EventTimeline` and `ContactForm` are M1 and are
not to be started early.

## 4. Colours are tokens, and dark mode is not optional

Use the semantic classes of the Atmosphere theme: `bg-body`, `bg-card`, `text-foreground`,
`text-muted-foreground`, `border-border`, `text-destructive`, `bg-primary`, and the rest of the same
family. Never a hex value, never a raw Tailwind palette colour such as `bg-slate-800`.

Both themes are the same design, so a component is finished when it reads correctly in both. There
is no light-only screen and no dark-only screen.

`DarkModeToggle` sits in the header of every layout; `ThemeProvider` in `main.tsx` is what decides.

## Screens are configuration, not markup

A back office screen does not contain a table or a form.

A list is a set of column descriptions in `features/<x>/list.ts` (`col.localized('title')`,
`col.date('updatedAt', { sortable: true })`) handed to `DataList`. `sortable` says what the server
declared in `CrudOptions.Sortable`; a column that claims more gets a 400.

A form is a zod schema in `features/<x>/schema.ts` mirroring the write DTO, handed to `SchemaForm`.
The schema carries types and what is required, and nothing else: every real rule belongs to the
server, which answers with it anyway. `.meta({ multiline: true })` asks for a textarea,
`.meta({ hidden: true })` keeps a field in the payload and off the screen, and `localized()` marks a
translated field, which becomes one tab per language.

If the generator does not cover a case, extend the generator. Writing the form by hand is what this
whole mechanism exists to avoid, and the reviewer's checklist asks about it.

## A block is a schema, a component and an example

A page is a tree of sections and blocks. What a block *means* exists in exactly one place, and that
place is TypeScript: the server stores `body_json` as an opaque document, checks its envelope — the
identifiers, the depth, the type against the registry — and never reads a property.

So a block is three things, in three files under `web/src/blocks/`:

- a zod schema in `schemas.ts`, which is what `SchemaForm` turns into the property form an editor
  fills in. The annotations are the same ones an entity form uses: `localized()`, `.meta({
  multiline: true })`, `.meta({ hidden: true })`;
- a component in `blocks.tsx`, handed `props` and — for a data block — `data`. It decides nothing
  about the page around it and never takes a language as a prop: `useLocalized()` knows which one is
  on screen;
- a registration in `core.ts` tying the two together with a type, a version, an icon, the i18n key
  of its name, and `example` properties the gallery mounts.

Three files rather than one because a module that exports components and constants together loses
fast refresh, which is a thing you notice every day.

A **data block** shows something the hub knows rather than something an editor typed. The server
answers for it (`IDataBlockProvider`), and the page decides *when* the question is asked: `live`
means the browser asks as it draws, `frozen` means publication asked once and stored the answer, so
the page keeps saying what it said that day until somebody publishes it again.

Two rules with something that fails behind them. Every block of the registry has a section in
`/staff/admin/ui-kit`, and its `example` has to satisfy its own schema — the test next to the
gallery is both halves. And every key a block asks for at run time (`blocks.<type>.label`,
`blocks.<type>.fields.<path>`, `blocks.<type>.options.<path>.<value>`) has to exist in every
language: `pnpm i18n:check` cannot see keys built at run time, so `blocks/registry.test.ts` reads
the language files and checks them, which is the test rule 1 tells you to write.

The conventions for what a block should look like — spacing, when to use a callout rather than a
heading — are M1. What is fixed now is the shape.

## Times

Always in UTC, with the time zone of the division next to it — a hub is read by people flying in one
and organising in the other. `DataList` does that for a `col.date`; anywhere else, use
`Intl.DateTimeFormat` with `timeZone: 'UTC'` and with `bootstrap.division.timezone`. Never the time
zone of the browser on its own.

## Accessibility, briefly

Every input has a `<label>` bound to it; the generator does this for you. An icon that carries no
meaning is `aria-hidden`; an icon-only button gets an `aria-label`. An error message is
`role="alert"`, so it is announced when it appears rather than only seen.
