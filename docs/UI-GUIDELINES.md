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

`web/src/shared/icons/index.ts` holds `ICONS`, the allow list an editor picks from — see
"Icons a block or an entity can choose" below. Reach for any `lucide` icon you like in a screen you
are writing; the allow list is only about what an *editor* may choose.

If an icon is genuinely missing from `lucide`, draw it in `web/src/shared/icons/<Name>.tsx` — a
24×24 SVG, `stroke-width` 2, `currentColor` — and add it to `ICONS` if editors should be able to
choose it too.

Never inline an `<svg>` in a screen. ESLint refuses one anywhere outside `shared/icons/` and
`blocks/`, where a block may draw its own decoration.

## 3. The custom components are a closed list

It is exactly:

`Hero`, `SectionHeader`, `StatTile`, `PageShell`, `EmptyState`, `LocaleSwitcher`, `LocaleFields`,
`MarkdownContent`, `DataList`, `SchemaForm`, `ProblemAlert`, `DepartmentBadge`, `VisibilityBadge`,
`StatusBadge`, `ConfirmDialog`, `MediaPicker`.

`MediaPicker` chooses a file out of the library of a department, and it is on the list because two
very different screens mount it: the library itself, and every block property that names a file. It
picks and nothing else — uploading belongs to the library screen, and a picker that also uploaded
would be a second way for a file to enter the hub.

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

`CalendarView`, `ContactForm` and `LiveStatusStrip` are the rest of M1 and are added by the phases
that need them; `RatingBadge`, `AirportCard` and `EventTimeline` belong to modules that do not exist
yet and are not to be started early.

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
server, which answers with it anyway.

A field that holds other fields — a translated object, a list, a nested object — is named in the
language files **flat**: `"seo"` for the group and `"seo.title"` beside it, rather than nesting
`title` inside `seo`. i18next resolves a dotted key either way, and a nested `seo` would be an
object where the group's own name has to be a word.

What the schema may say about how a field is drawn:

- `localized()` marks a translated field, which becomes one tab per language;
- `.meta({ multiline: true })` asks for a textarea;
- `.meta({ hidden: true })` keeps a field in the payload and off the screen — `rowVersion`;
- `.meta({ choices: [1, 2, 3] })` on a **number** draws a select. A number and not a `z.enum`
  because every string inside a block's properties is extracted as the text of the page for the
  search index, and a heading level is not text;
- `.meta({ choices: ['Links.Edit', 'Content.Edit'] })` on a **string** draws a select whose options
  are the values themselves, with no i18n key each. It is for a set only known at run time — the
  permission catalogue, whose members depend on which modules are installed — and whose members are
  identifiers rather than prose: `Links.Edit` reads `Links.Edit` in every language, exactly as a VID
  or a department code does;
- `.default(10)` is read as well, so what a new row or a new block starts with lives next to the
  field rather than in a second place that can drift;
- an **optional** `z.enum` also gets a "nothing chosen" entry, labelled
  `<labels>.options.<path>.none`. A select has no gesture for going back, so without it the first
  choice a coordinator makes would be permanent;
- `.meta({ media: true })` on a **number** is a file of the media library, chosen in `MediaPicker`.
  Never a number to type: a free identifier is how a page ends up pointing at a file somebody
  deleted years ago. The screen hands `SchemaForm` the library to choose from (`mediaLibrary`),
  because which department's files to show is a fact of the screen and not of the schema;
- `.meta({ icon: true })` on a **string** is an icon out of the allow list in `web/src/shared/icons`,
  drawn as a grid of pictures. It is not a select: Atmosphere's takes a plain string per option, so
  a select could only ever list the names — and a name without its picture is the choice nobody can
  make;
- `.meta({ date: true })` is a calendar day and `.meta({ datetime: true })` an instant. **The value
  is always ISO in UTC**, whatever the browser's own time zone is. An instant shows the division's
  local time underneath, which is the rule every list follows; a day does not, because "the same day
  elsewhere" is not a fact a day has, and echoing one is only ever a chance to read the wrong day.
  Both need `division` on `SchemaForm`;
- `localizedObject({ ... })` is a small object per language: language tabs, and inside each of them
  the generator again. `seo` is the first, and it is how a translated JSON column becomes fields a
  coordinator can fill in rather than JSON they have to write;
- a **list** of objects can be reordered with the up and down buttons beside add and remove. They
  are what a keyboard reaches, and they stay when dragging arrives.

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

### The conventions every block follows

These were decided with the whole set on the table rather than one block at a time, which is the only
way they could have been decided at all.

**Spacing belongs to the section, never to the block.** A block draws itself and does not touch the
margin around it. The section has `padding` (`none`, `sm`, `md`, `lg`) and the blocks inside a column
are separated by one constant gap. The reason is not tidiness: if two blocks each brought a margin of
their own, the distance between them would depend on *which two they are*, and nobody would know
where to change it. `spacer` exists for the declared exception — air between two blocks that belong
together and two that do not — and not to make up for margins that disagree.

**The background belongs to the section too, and there are four**: `none`, `muted`, `accent`, and
`image`, which carries a `mediaId` of the library. A block has no ground of its own, with three
exceptions whose identity *is* their ground — `hero`, `callout`, `testimonial` — and even those use
the semantic tokens of the theme and never a colour written by hand. Two `muted` sections one after
the other simply merge, and that is fine: alternating is the editor's choice, not a rule.

A picture behind a section is for a quiet section. The text over it keeps the page's own foreground
colour — there is no veil, because a veil is a colour that is not a token — so a wall of prose over a
photograph is not something this hub can make read well, and should not be attempted.

**The width belongs to the section**, and there are four: `narrow`, `default` (the reading column),
`wide`, `full`. `full` is for `hero`, `gallery` and `image`; a section of text as wide as the screen
is a line nobody finishes.

**A locked section shows its fields, not its structure.** No "add block", no "move", no "delete":
what an editor sees is the list of blocks the template put there, each with its property form, and a
line at the top saying which template fixes it and who may change that (`Content.ManageTemplates`).
A disabled button with no explanation produces support tickets; a sentence saying "this section is
fixed by the *Policy* template" does not.

**An unknown block is shown to the staff only.** If the server declares a type this browser has no
component for, or the other way round, a coordinator gets a dashed box naming the `type`, and a
visitor gets nothing at all. A page does not break because a browser is one release behind.

**Every block declares its own `lucide` icon**, and the type insists on it: it is what the editor
shows in the "add a block" list and in the tree.

**Nothing that is not prose is a free string.** Every string inside a block's properties is
concatenated into the text of the page for the search index, so an alignment, a column count or an
icon name is a `z.enum` with values nobody would search for, or a number with `choices`. A column
count is a number; the shape of a video is `16x9` and not `16:9`, because a colon is what i18next
reads as a namespace separator.

**A block never contains blocks.** `tabs` and `accordion` carry markdown per entry — the same
sanitized `MarkdownContent` as `text` — and the only nesting in the model is the one sections have
(depth three, enforced by the server). A block that contained blocks would be a second tree, with a
second validator, a second editor and a second way of getting the depth wrong. The 10 % of cases
markdown does not cover is a section with a column layout.

**A frame is only ever pointed at a host on the allow list.** `video` and `embed` read
`web/src/blocks/allowlist.ts`, which turns the address of a *page* — the one in the browser bar — into
the address of a player, built from the identifier it recognised. Nothing typed is ever echoed into
the `src`. Growing the list is adding an entry; pointing an `<iframe>` at whatever an editor typed is
not something this hub does.

### How a block names a file

A file in the media library is referred to by its identifier, and the property that holds it is
called **`mediaId`** — one file, one key, wherever it sits. A block that shows several holds a list
of small objects each with its own `mediaId` (`images: [{ mediaId }]`), rather than a list of bare
numbers: the form generator draws lists of objects, and the key stays the one the server looks for.
The section's own background picture is a `mediaId` too, for the same reason.

The name is a convention rather than a type because the server cannot read a block schema: it stores
`body_json` as an opaque document. What it does know how to do is ask a JSON column whether it
mentions an identifier, **anywhere at any depth**, under that key — and that is what stands between
deleting a file and breaking a page that has already been published. A block that invented a second
name would have its file deleted out from under it. (`mediaIds`, a bare array, is still understood by
the same query and is what an older body may carry.)

Never draw such a property as a number field. `MediaPicker` is what fills it in, and the reason is
plain: a free numeric field produces pages pointing at files that were deleted years ago.

The alternative text is a property of the **block**, and empty means the picture is decoration: it
renders as `alt=""`, which is what makes a screen reader skip it. It is *not* inherited from the
library — the public renderer is handed a published body and nothing else, and a server that filled
it in would have to read inside `props`, which is the one thing it must not do. The library keeps an
`alt` of its own for the library's own screens. Why it is this way rather than inherited:
`docs/internal/decisions/2026-09-06-alt-delle-immagini.md`.

## Icons a block or an entity can choose

`web/src/shared/icons/index.ts` is the allow list an `.meta({ icon: true })` field offers, and the
place an icon `lucide` genuinely lacks would be drawn by hand. It lives in `shared/` rather than in
`blocks/` because the form generator reads it and `blocks/` already imports the generator: the other
way round would close a circle between the two.

Growing the list is adding a line. It is not a decision, because what it draws from — `lucide` — was
decided once and is not up for discussion.

## Times

Always in UTC, with the time zone of the division next to it — a hub is read by people flying in one
and organising in the other. `DataList` does that for a `col.date`; anywhere else, use
`Intl.DateTimeFormat` with `timeZone: 'UTC'` and with `bootstrap.division.timezone`. Never the time
zone of the browser on its own.

## Accessibility, briefly

Every input has a `<label>` bound to it; the generator does this for you. An icon that carries no
meaning is `aria-hidden`; an icon-only button gets an `aria-label`. An error message is
`role="alert"`, so it is announced when it appears rather than only seen.
