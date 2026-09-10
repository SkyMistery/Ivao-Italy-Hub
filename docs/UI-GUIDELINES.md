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
`StatusBadge`, `ConfirmDialog`, `Notice`, `MediaPicker`, `CalendarView`, `ContactForm`,
`LiveStatusStrip`, `StaffSidebar`.

`StaffSidebar` is the navigation of the back office, and it is the one entry on this list that
replaces something Atmosphere ships rather than adding something it lacks. The reason is narrow and
worth knowing before anybody proposes going back: Atmosphere's `Sidebar` draws its own collapse
button, last in the panel, the full width of it, carrying the string `"Close sidebar"` written into
the library — so the button cannot be moved, cannot be made an icon, and **cannot be translated**,
which made rule 1 of this file impossible to keep in the back office of a division that does not
speak English.

What it does *not* replace is as important: the open and closed state is still Atmosphere's
`SidebarProvider` and `SidebarContext`, and every leaf is still its `SidebarItem`. Only the frame
and the group heading are ours, because those are the two pieces the library exports no way to
reach. And it **is** the `<aside>` — it is not a panel to wrap in a shell of your own. Wrapping the
one it replaced once drew the whole back office inside a 288 pixel column with two collapse
buttons, and `e2e/back-office.spec.ts` measures the geometry so that it cannot happen again.

`MediaPicker` chooses a file out of the library of a department, and it is on the list because two
very different screens mount it: the library itself, and every block property that names a file. It
picks and nothing else — uploading belongs to the library screen, and a picker that also uploaded
would be a second way for a file to enter the hub.

`CalendarView` draws the one calendar of the division as an agenda, a week or a month, and it is on
the list for the same reason: two screens mount it, the public calendar and the `calendar` block
inside a page. Atmosphere has a `Calendar`, and it is a date *picker* — it answers "which day do you
mean", not "what is happening". The component draws and decides nothing else: which entries, and for
which stretch of time, is the caller's business, which is what lets it be a month grid on one screen
and five lines inside a section on another.

⚠️ Its days are **UTC days**. The hub stores UTC and the network runs on it, and a grid whose day
boundaries moved with the reader's browser would put one entry in two different squares for two
people looking at the same page. Every time on it is shown in UTC **and** in the division's own
zone, never one instead of the other, and the zone is handed in from `/api/me` rather than being a
constant anywhere.

The list lives in `web/src/shared/ui/catalog.ts`. Everything else is Atmosphere.

⚠️ **Atmosphere's `Select` throws `aria-label` away.** A select given only that attribute has no
accessible name at all: label it with a real `<label htmlFor>` — visually hidden where there is no
room for one — and give the select the matching `id`, which it does forward. Measured in the DOM
after a test could not find a control by its name.

⚠️ **`hidden sm:block` does nothing here — write `max-sm:hidden`.** Atmosphere's stylesheet is
imported after Tailwind's own utilities and declares `.hidden` again, so the plain class wins over
the one inside the `sm` media query and the element never comes back on a wide screen. Both rules
have the same specificity, so the later sheet decides. Anything that should appear only above a
breakpoint therefore hides itself inside the media query instead. Found in the built bundle, twice
in one hour, after two elements quietly refused to exist.

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

`ContactForm` is on the list since G7: writing to a department is mounted from more than one place —
the contact page, and any section of a department's own page that grows one — and what it adds to a
generated form is the two things a schema has no opinion about, which departments can be written to
with their names in the language on screen, and what a sent message looks like. It contains no
field: the form itself is `SchemaForm` reading `shared/ui/contact.ts`.

`Notice` says one thing to the person using the hub, in one of four tones: something went wrong,
something is worth their attention, something worked, something is worth knowing. It is on the list
because it is mounted from anywhere, in both of its shapes — a panel that stays on the page, and the
same sentence said in the corner of the screen and then gone, which is what `useNotice()` pushes into
Atmosphere's toast queue. A confirmation is not something to close: "saved" has been read by the time
the eye is back on the page.

⚠️ It is **not** `ProblemAlert` and does not replace it. That one draws what the server refused,
field by field, out of a `ProblemDetails`; this one is a sentence somebody wrote. The two may be
worth merging one day; doing it touches every screen of the back office, so it is a decision of its
own rather than a tidy-up.

Two of its four tones are Atmosphere's own alert variants, used as they are; the other two are
written in `shared/ui/notices.ts` in the same shape, because the theme ships the `semantic-yellow`
and `semantic-blue` scales and only the component lacks a variant for them. That file is the one
table both shapes read, so a tone cannot be green in one of them and grey in the other.

`LiveStatusStrip` is the band under the header of the public site: who is connected to the network,
refreshed by **polling** and never by a socket — a strip that changes by ones once a minute does not
justify a connection per reader, and the proxy in front of this application is not the place for one.
It has no endpoint of its own either: it asks the `networkStats` data block, which is anonymous and
always live and is already the answer to that question.

⚠️ Two things about it are rules and not taste. It goes in the **`banner` slot of `Shell`**, between
the header and the content, because a strip inside the reading column is not a strip — and there is a
measurement in `web/e2e/live-status.spec.ts` that fails if somebody moves it back. And when the
network could not be asked it draws **nothing**: `updatedAt` of null means "no answer", which is not
the same as nobody being connected, and four zeroes would be the site answering a question it never
got an answer to.

`RatingBadge`, `AirportCard` and `EventTimeline` belong to modules that do not exist yet and are not
to be started early.

## 4. Colours are tokens, and dark mode is not optional

Use the semantic classes of the Atmosphere theme: `bg-body`, `bg-card`, `text-foreground`,
`text-muted-foreground`, `border-border`, `text-destructive`, `bg-primary`, and the rest of the same
family. Never a hex value, never a raw Tailwind palette colour such as `bg-slate-800`.

Both themes are the same design, so a component is finished when it reads correctly in both. There
is no light-only screen and no dark-only screen.

`DarkModeToggle` sits in the header of every layout; `ThemeProvider` in `main.tsx` is what decides.

**Two things are overridden in Atmosphere, and they are the only two.** Both live at the bottom of
`src/styles/index.css`, both are measured, and both have a test that fails if the line goes away.

The first is a **colour**. Atmosphere flips every foreground for the dark theme except
`--muted-foreground`, which stays fuselage-500 in both — a grey that reads well on white and comes
out at 3.14 : 1 on the dark ground, where WCAG AA asks 4.5 : 1 for text at 12 and 14px. It becomes
fuselage-400 for the dark theme only, which is the distance the light theme already keeps from its
own ground.

The second is a **height**, and it is a plain defect rather than a matter of taste: their `Select`
gives its popup the height of its **trigger**, so the list is one row tall whatever it holds —
measured at 46px of viewport for rows of 30. Every option is in the document and reachable from a
keyboard; what a reader sees is a control offering one thing out of four, with no sign there is a
second. The rule gives the popup the height of its own list, capped by what Radix says is available
on screen, so a long list still scrolls. `e2e/select.spec.ts` asserts the geometry, which is the only
thing that would have caught it.

Both have to sit **after** the Atmosphere imports, because that stylesheet is loaded after
Tailwind's utilities and where a rule goes decides whether it wins — neither needs `!important`, and
that was verified in a browser rather than assumed. `e2e/contrast.spec.ts` measures every visible
piece of secondary text on nine screens in the dark theme and fails if the colour goes back; the
colours are read out of a canvas, because some arrive as `oklab()` and a regular expression over one
of those returns something close to black.

If you fork this and change the palette, that is the test that tells you whether your greys are
readable. **Adding a third override is a decision, not a tweak**: the point of keeping the list short
is that "Atmosphere as it is" stays true enough to be worth saying.

## Screens are configuration, not markup

A back office screen does not contain a table or a form.

A list is a set of column descriptions in `features/<x>/list.ts` (`col.localized('title')`,
`col.date('updatedAt', { sortable: true })`) handed to `DataList`. `sortable` says what the server
declared in `CrudOptions.Sortable`; a column that claims more gets a 400. `col.media` draws a file
of the library as a thumbnail rather than as the number it is stored as; its alternative text is
empty on purpose, because the row's own title is in the cell beside it. `col.file` is the same
identifier drawn as a **link**, for an attachment whose type the row does not carry: a thumbnail
handed a PDF draws a broken image, which reads as a failed upload. An empty cell there means the row
has no file, which is a state and not a gap.

A column can be written in place: `col.number('sort', { editable: true })` draws a field in the cell
and `col.badge('visibility', 'content', { editable: ['Public', 'Members', 'Staff'] })` a select — a badge cannot
know its own set, so it is given one. Only those two and a boolean: a translated text or a file needs
the form, and a cell that opens half of one is the second way of writing a row that this whole
mechanism exists to avoid. The control appears only if the screen also handed `DataList` an `onEdit`,
or a list with no way of saving would draw a field that does nothing. A number saves when the field
is left, a select when the choice is made, and a refusal puts the old value back and says why in a
`Notice`.

Two lists that differ only in what they are about are **one screen twice**, not two screens. The
news, the documents and the pages of a department are the same list with a fixed `kind` and a
different set of columns, so what tells them apart is a configuration object and the route file that
owns the address (`features/content/kinds.ts`). If telling two lists apart ever needs a second
screen, that is worth saying out loud in the pull request.

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
- `.meta({ choices: [{ value: 'guides', label: 'Guides' }] })` is the same select when the label is
  **not** the value: the category of a news item is stored as a stable key and shown as the word a
  coordinator wrote in another table. The caller resolves the label into the language on screen
  before handing it over — the generator never translates a value it was given — and an **optional**
  one gets the same "nothing chosen" entry an optional `z.enum` does;
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
  are what a keyboard reaches, and they stay when dragging arrives;
- `.meta({ slugFrom: 'title' })` proposes a field from another one while the other is being typed,
  and stops for ever the moment somebody writes in it. `slugPrefix: '/'` puts the leading slash of a
  menu path in front. An address outlives the page it was made for, so a row that already has one
  never has it moved;
- `.meta({ suggestions: [{ value: '/about', label: 'About us', group: 'Web' }] })` is a box that
  offers what exists while somebody types, grouped by whatever the caller says the group is. It is
  **not** a select: what is typed is the value, and the list is a way of not typing it. Add
  `suggestionsOnly: true` and it becomes the opposite — what is typed is a way of *searching* the
  list, and anything the list did not offer is gone when the field is left. What is typed reaches the
  screen through `onSuggestSearch`, so the list can be a question to the server rather than a page of
  rows already downloaded.

⚠️ `suggestionsOnly` is the only field kind that **decides** rather than offers, so it comes with two
obligations. The first: **the server has to refuse the same set.** The closed field is a convenience,
and a convenience is not a rule (a `PUT` from anywhere else would walk straight past it). The one use
of it is the address of a menu entry, and the pair to read is
`MenuItemWriteDtoValidator.LeadsSomewhereThisSiteOwnsAsync` next to `menuItemSchema`.

⚠️ The second: **the list must be able to grow past one request.** A list endpoint answers at most a
hundred rows, and while a field only suggests that is an inconvenience — whoever does not find their
row types it. A closed field turns it into a row nobody can point at. So hand `SchemaForm` an
`onSuggestSearch` — it is called with the field's path and what is being typed, three hundred
milliseconds after the typing stops — and let the screen ask the server again with `q` (use
`keepPreviousData`, or the list blinks empty and an empty list here reads as "nothing matches").
Filtering in memory is right for a short, fixed list and wrong for anything a database grows.

⚠️ And where that set contains **routes of this client**, the two halves agree **by hand**: a route
is not something the OpenAPI contract can carry. `MenuItemWriteDtoValidator.Screens` and
`SITE_SCREENS` in `web/src/routes/_staff/staff.$dept.menu.$id.tsx` are one list written twice. A fork
that adds or removes a screen edits both, and an integration test posting a screen keeps them honest
— the same arrangement the backgrounds of a section already use.

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
  of its name, the **drawer of the palette it belongs in**, and `example` properties the gallery
  mounts.

**Where a block appears in the editor is declared on the block, in code.** The bar of components on
the left of the editor is the registry drawn: `group` — one of `content`, `layout`, `interactive`,
`structure`, `data` — and optionally `subgroup`, and the drawers come out in the order those two
lists are written in, not the order the blocks happen to be registered. A module's blocks arrive in
the same bar through its manifest and declare the same field.

There is no table of palette entries and no screen where anybody arranges them, and that is the
point: a palette somebody could rearrange would be a second place where the catalogue lives, and the
two would disagree the first time a block was added. Adding a group or a subgroup means adding it to
those lists and giving it a name in every language (`blocks.groups.<group>`,
`blocks.subgroups.<subgroup>`); `blocks/registry.test.ts` refuses a block whose drawer has no name.
A drawer nothing is in is not drawn, so a fork that registers no data block simply has no Data
drawer.

Three files rather than one because a module that exports components and constants together loses
fast refresh, which is a thing you notice every day.

A **data block** shows something the hub knows rather than something an editor typed. The server
answers for it (`IDataBlockProvider`), and the page decides *when* the question is asked: `live`
means the browser asks as it draws, `frozen` means publication asked once and stored the answer, so
the page keeps saying what it said that day until somebody publishes it again.

It costs two more things than a content block, and both are small. An **`exampleData`** in its
registration, because the gallery is a page about the components: a data block that called the
server there would show whatever this installation happens to hold today, or nothing at all on a
fresh one. And a **provider registered for its type** on the server, which reads through the same
visibility filter as everything else — a provider that filtered by hand is how a staff row ends up
on a public page.

Some data is meaningless once captured — who is on frequency right now. Such a block declares
**`alwaysLive`** on both halves of its registration, and that one flag is the whole rule: the editor
does not offer the choice and publication does not freeze it. Do not write the exception anywhere
else. `networkStats` is the only one in the core, and it is what the flag was added for.

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

**A locked section shows its fields, not its structure.** No "add block", no "move", no "drag", no
"delete":
what an editor sees is the list of blocks the template put there, each with its property form, and a
line at the top saying which template fixes it and who may change that (`Content.ManageTemplates`).
A disabled button with no explanation produces support tickets; a sentence saying "this section is
fixed by the *Policy* template" does not.

**The renderer has an editing mode, and it does not exist for a visitor.** Since 9 September 2026 a
page is composed **on the page**: in the editor's preview you click a block and its fields open
beside it. The same component draws both, so the interactivity is a context (`blocks/picking.ts`)
that is `null` everywhere and that the public path never provides — not a flag that is switched off,
a thing that is not there. A block you write needs to do nothing about it; what you must not do is
make the renderer read a global, an environment variable or a route to decide, because then a
visitor's page and the editor's stop being the same page.

⚠️ A block is wrapped, not replaced: the click is caught in the **capture** phase and stopped there,
so a link or a button inside a block selects the block instead of firing. That is why a call to
action in the preview does not carry the editor away with unsaved changes — and why a section is
picked by its own space rather than by its children.

**An unknown block is shown to the staff only.** If the server declares a type this browser has no
component for, or the other way round, a coordinator gets a dashed box naming the `type`, and a
visitor gets nothing at all. A page does not break because a browser is one release behind.

**Every block declares its own `lucide` icon**, and the type insists on it: it is what the editor
shows in the "add a block" list and in the tree.

**The prose of a block is what is translated, and only that reaches the search index.** The
extractor walks the properties and keeps a string only if the walk passed through a `Localized` map
on its way to it; an alignment, a column count, an icon name, a URL — bare strings, all of them — are
left out. It needs to know nothing about any block's schema, which is the constraint the backend is
held to, and it is why "nothing that is not prose is a free string" is now a property of the
mechanism rather than a rule somebody has to remember. It used to be only the rule, and the rule was
broken by a block in the first set: a snippet read "… four simple steps. `left muted` First of
all…".

⚠️ The corollary, and it bites: **a searchable string that is not translated is not indexed.** A
partner's name in `logoWall` and the author of a `testimonial` are written once because they read the
same in every language, and they are the two that pay for it. If a block of yours carries prose that
does not vary by language and has to be findable, make it `localized()` anyway.

An alignment or a tone is still a `z.enum` and a column count still a number — not because the index
would otherwise eat them, but because a select is the right control for a closed set. The shape of a
video is `16x9` and not `16:9`, because a colon is what i18next reads as a namespace separator.

**And prose loses its Markdown on the way in.** A snippet is read by a person, so the asterisks come
off, a link keeps its text and loses its address, and a heading or a bullet loses its marker.
Underscores stay: `snake_case` is a word.

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

The five marks in `brands.tsx` are the exception the paragraph above promised, and the only one so
far: `lucide` carried brand icons until version 1 and then dropped every one of them, so Discord, X,
Facebook, Instagram and YouTube are drawn here, on lucide's grid, in `currentColor`. They are
simplified marks and not the brands' own artwork — a division that wants the logotypes puts them in
the media library.

⚠️ **Draw an icon through `iconGlyph(name, classes)`, never by calling `iconByName` in a render.** A
component read out of a map while something renders is one React treats as new on every pass: it
remounts what it draws, and `react-hooks/static-components` refuses it outright. `iconGlyph` hands
back an element, built once per set of classes and kept.

## The frame of every page: one bar, and a footer in columns

**The bar at the top is one row.** The menu, the search, the language, the theme, the account and
the way into the back office all ride in `Navbar`'s own children slot, which Atmosphere draws at the
far end of the line that carries the logo and the division's name. Two rows was a second band of
chrome above every page for no gain. Anything put there needs forcing white — it sits on a dark
blue — and the primary button variant is that same blue, so a call to action there is `secondary`
or it is invisible.

Three zones, and the middle one grows: the brand on the left, the **menu centred**, the tools on
the right. That is why the bar is composed from `NavbarContainer` and `IVAOLogo` rather than from
`Navbar` — `Navbar` puts its children in a box of their own at the far end of the line, and a box
that cannot grow cannot hold anything in the middle.

And everything in the right hand zone is **one word wide or less**. The language switcher shows the
code (`EN`, `IT`) and carries the full name as its accessible label: spelled out it took more room
than the search, the theme and the account together, on every page of the site, to say something the
reader already knows.

**The footer is the footer menu, drawn in columns.** A top level entry of `Scope = Footer` is a
column and its children are its links; nothing in the component decides what is in them. Three
shapes, and all three are states of the menu table:

- an entry with **no address** is a column heading — "Quick links", "Resources" — and it is the one
  entry in the whole hub allowed to lead nowhere. Only at the top of the footer: a child with no
  address is a line nobody can click, and a heading in the bar at the top is an entry that does
  nothing when pressed;
- a column whose links **all carry an icon** is the row of the division's accounts, and it is drawn
  beside the division's own words rather than as one more column of text. This is the one inference
  in the footer, and it is here rather than in a second field on every menu entry because only one
  column in a whole site ever asks the question;
- an entry with **no children** keeps the shape footers had before columns: a plain link in a row of
  its own. A division that upgrades does not lose what it already wrote.

The sentence under the division's name and the legal links are words, not rows: they live in
`locales/`, so a fork changes them where it changes every other sentence.

The columns are **centred**, not pinned to the left edge: a division may write one or four, and a
row that centres what it has looks deliberate at either count where a four column grid holding two
leaves empty tracks.

**The line under the rule carries two sentences and no links**: who the site belongs to with the
release it is running, and what it is part of. It is where the eye stops, so everything else put
there competes with the only two facts that belong there — the legal links of headquarters are a
column above. The year in it comes from the browser: a year written into a language file is wrong
every January, in every language at once.

## The editor of a page, and what it may not do

Five rules M1 settled by using the editor rather than by designing it. They are here because they
are the ones a contributor is most likely to break by improving something.

**Three columns, and the middle one is the only one that changes.** Components on the left, the page
in the middle, the properties of whatever is selected on the right. The middle column opens on the
page itself — the same renderer the public gets, clicked to select — and one press swaps it for the
outline, which is the road for anybody without a mouse. The two side columns do not move when it
does: an editor whose panels jump when you change how you are looking at the page is one you have to
re-find your place in every time.

**There is one place a block is added from.** The palette is the bar on the left, and adding a block
puts it in the section that is selected — the section itself, or the one holding the selected block.
A block the template forbids there is **disabled and still shown**, with the reason on it, rather
than filtered out: the target changes as you click around the page, and a list that changed shape
each time would be one nobody could learn.

**A section is reordered by dragging *and* by two arrows, and the arrows are not decoration.**
Dragging is a pointer and nothing else — no keyboard, no screen reader, no touch worth the name — so
the arrows are the whole of that panel for anybody who cannot use a mouse. A row is dragged by a
**handle**, never by the whole row: the row is made of buttons, and making it all draggable turns
every click on "remove" into a race.

**A template never rewrites a page.** The editor may say a section was added, removed, or no longer
fits — and it applies **one** difference at a time, on a click, never all of them. Some differences
have no action at all: a section whose blocks the template no longer allows is *said*, because
aligning it would mean deleting what somebody wrote. A disabled button is the same trap with a
friendlier face.

**A section a page adds for itself carries no `key`.** A key is the handle back to a section of the
template; a page-only section that claims one is reported as "no longer in the template", and the
action offered for that is *remove*. A seed that gets this wrong is an editor offering to delete a
page's own content — which is why `src/features/content/seeds.test.ts` exists.

**The multi-device preview is a `max-width`, not an emulator.** Three widths of the same page,
rendered by the very same component the public site uses. It must not grow a device frame, a user
agent or touch emulation: the value of one renderer is that "what will this look like" cannot
disagree with "what this looks like".

## Times

**Aviation, not the locale's habit.** Twenty four hours everywhere, `Z` for zulu and `LT` for the
reader's own zone — `14:00Z (16:00 LT)`. A briefing at 14:00 is written 14:00, and "2:00 PM" is a
form nobody on the network uses. It is one line in `useMoment`, which is the only place in the client
that formats an instant, and that is why it is one line.

**The date goes only where nothing else has said the day.** A square of a calendar grid and the
heading of a day list have already said it; repeating it is noise on the line a reader actually
reads. The agenda keeps it, because it is a flat list running forward and has neither. An entry that
is a whole day keeps its date wherever it is drawn — and takes **no** `Z`, because a day is not an
instant.

Always in UTC, with the time zone of the division next to it — a hub is read by people flying in one
and organising in the other. `DataList` does that for a `col.date`; anywhere else, use
`Intl.DateTimeFormat` with `timeZone: 'UTC'` and with `bootstrap.division.timezone`. Never the time
zone of the browser on its own.

## Accessibility, briefly

Every input has a `<label>` bound to it; the generator does this for you. An icon that carries no
meaning is `aria-hidden`; an icon-only button gets an `aria-label`. An error message is
`role="alert"`, so it is announced when it appears rather than only seen.
