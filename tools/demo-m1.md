# Demonstrating M1

> Updated to **12 September 2026, evening** — so it covers G13 and the twelve requests the first run of this
> sheet produced, then G15 (the editor that answers), G14 (the operational document), the colour, the
> security headers and the interactive block.
> A working translation lives in `docs/internal/demo-m1.md`; this one is the official version, and it
> is in English because whoever forks the hub has to be able to read it (CLAUDE.md section 1). If one
> of the two changes, the other changes in the same commit.

M0 built a backbone and proved it on one boring entity. M1 is what that backbone was for: a public
site nobody had to program, an editor a coordinator can use, and five modules' worth of screens that
are configuration far more than they are code.

So this walk-through is not "look at the features". It is one question asked nine times: **is this
row, or is this code?** Every part ends with a box, and the boxes are the eight points of the
definition of done in design M1 §0.1.

It takes about forty minutes. Nothing is scripted, deliberately: a script that passes tells you the
script passes.

> Every part names the automated test that asserts the same property. If a step here fails and its
> test passes, that difference is the most interesting thing in the room.

---

## What you need

| Tool | Version |
| --- | --- |
| .NET SDK | 10.0.x |
| Node.js | 22 LTS or newer |
| pnpm | 10.x |
| Docker | any recent version |
| An IVAO OAuth client | login URL and redirect URI registered for `http://localhost:5173` |

```bash
git clone https://github.com/SkyMistery/Ivao-Italy-Hub.git
cd Ivao-Italy-Hub
cp config/ivao-oauth.example.json config/ivao-oauth.json   # then fill it in
docker compose up -d                                        # MariaDB 11.4.10 and Mailpit
dotnet run --project src/IvaoHub.Web                        # API on :5000
cd web && pnpm install && pnpm dev                          # SPA on :5173
```

Sign in at <http://localhost:5173> before part 2. Everything up to there is what a visitor sees.

---

## Part 1 — The public site is rows

Open `/`, `/start`, `/pilots`, `/atc`, `/about` as a visitor. Five pages, and **no component
anywhere draws any of them**: they are rows in `cms_contents`, seeded from templates in
`seed/content-pages/*.json`, with a body of blocks.

Now the menu, which is the part that usually is code:

1. go to `/staff/wd/menu` and delete the **Pilots** entry;
2. reload the public site. It is gone from the header, with nothing recompiled;
3. put it back.

⚠️ The menu is a table **owned by the web department**, not a screen every department has. Try
`/staff/ed/menu`: it is not there.

**New in G13, and it is the strictest rule this product has taken on a field.** In the table,
**Order** and **Visible to** are written in the cell — no form, and a refusal puts the old value
back. And opening one entry, the **Address** is a **closed** list:

- the pages of the site, grouped by the department that wrote them, **drafts included** (a draft says
  so): write the entry now, switch it on when the page goes out;
- the screens of the application, which are routes and not rows;
- the links of the library, **the ones in use**.

Type something that is not one of them and it is gone the moment you leave the field, and the server
refuses it too — the field is a convenience, the rule is the server's. **The point is not the menu**:
it is that every address leaving this site lives in one table, so moving the forum is one row of
`/staff/wd/links` and the menu follows.

- [ ] **Point 1** — the public site exists and the code does not draw it, and a menu entry cannot
      point anywhere the site does not own.
      Asserted by `e2e/full/menu.spec.ts`, `e2e/back-office.spec.ts` and
      `SiteMenuAndDashboardTests.AMenuEntryOnlyLeadsWhereTheSiteOwnsSomething`.

---

## Part 2 — News and documents are one entity with two `kind`

Open `/staff/wd/news` and `/staff/wd/documents`. Two lists, two forms, two public sections — and
**one** table, one editor, one renderer, one publication path, one projection.

The evidence is in the shape rather than in the screens: `features/content/kinds.ts` is what makes a
news item a news item, and it is a configuration object. Write one news item, publish it, and read
it at `/news` and `/news/{slug}`; write a document with a file and read `/documents` and
`/documents/wd`.

If either of those needed a new non-nullable column or a second editor, design §9.3 did not hold and
the closing report has to say so. It did not.

**New in G13, and you meet it here**: the address (`slug`) is **proposed from the title** until you
write one yourself, and saving now **answers** — a toast in the corner. A row that already has an
address never moves it.

**New in G14: the operational document, which is the hardest thing this claim has been asked to
carry.** On `/staff/aod/documents` make one and give it a **type** — SOP or LoA — an airport and a
FIR **chosen from a list** (the validator refuses an ICAO the division's airspace does not hold), the
positions it is about, the day it comes into force and the day it is to be reviewed by. Publish it
and read it: a strip under the title says what it is about, a notice at the top appears when it is no
longer in force and names what replaced it, and **Print** puts it on paper with the tabs and
accordions unfolded. ⚠️ Then look at what it is **not**: no new table, no new entity, no second
editor, no third `kind`. Six nullable columns, two blocks and a footer — and
`NoSecondContentEntity` is still green, which is the test that would have said otherwise.

- [ ] **Point 2** — two kinds, not two tables.

---

## Part 3 — The block set, and the gallery that builds itself

Open `/staff/admin/ui-kit`. Every block the registry declares is there, drawn from the registration
itself: nobody adds a section to that page when a block is added. Count them — **29**, of which 7 are
Data blocks that ask the server for their content and two are G14's, the frequency table and the
coordination table of an operational document.

On the same page, among the components, is what G13 added: **`Notice`**, the four-state alert, in
both of its shapes — the panel that stays and the confirmation that appears in a corner and then
goes. It is the **fifth** component of the closed list.

Then read `docs/UI-GUIDELINES.md`. The block conventions are decided and written there: the spacing
and the ground belong to the **section** and never to a block, **eight** grounds, four widths, no
block contains blocks, and the prose of a block is what is translated.

⚠️ That last one used to be a rule somebody had to remember — "no string inside `props` that is not
prose" — and it was broken by a block of the first set, which the visual round caught
(`decisions/2026-09-07-giro-visivo-m1.md`). Since 9 September it is a property of the mechanism: the
extractor keeps a string only if the walk reached it through a `Localized` map. The corollary is the
price, and it is written there too — a searchable name that nobody translates is not indexed.

- [ ] **Point 3** — the block set of §1, every block in the gallery, conventions written down.

---

## Part 4 — One calendar, with a screen

`/calendar` as a visitor: filters in the address, so a filtered view is a link you can send. Then
`/staff/wd/calendar`, where staff write entries for their own department, and a `calendar` block
dropped into any page shows the same entries.

The point is that there is **one** calendar: an entry written by a module and an entry written by
hand are the same row, because the modules project into it rather than keeping their own.

**New in G13, all of it visible here:**

- **four views** instead of three: a week and a month as a grid, a week and a month **as a list**
  — the list leaves out the empty days, which is the whole difference;
- the time is **UTC** and, **in brackets**, the division's own;
- every entry carries a **coloured chip** for its kind;
- and the **kinds are a division vocabulary**: `/staff/admin/calendar-kinds`, decided by whoever
  holds `Calendar.ManageKinds` and the same for every department. ⚠️ Writing an entry, the kind is
  **picked from a list**: it is not free text any more, and a word that is not in the vocabulary is
  refused by the server.

Retire a kind (take "in use" off) and look: the entries already written with it stay as they are
— there is no foreign key, on purpose — but nobody can file a new one under it.

- [ ] **Point 4** — the single calendar has a UI.

---

## Part 5 — Media, contacts, the staff directory, the live status

- **Media** (`/staff/wd/media`): upload one image. Use it in a `hero`, in a `gallery`, and as the
  cover of a news item. One file, three uses, and the delete dialog tells you where it is used
  *before* you press anything.
  ⚠️ **New in G13, and you meet it immediately**: a file arrives visible to the **staff**.
  Publishing a public page that shows it is now **refused**, naming the picture — before, nothing
  was said and the visitor got a broken image. Make it public in the library and publish again.
- **Contacts**: `/contact` — a **member** page, because a message carries the VID of whoever wrote
  it. Send one to a department, then read it at `/staff/wd/contacts` and read the mail Mailpit
  caught at <http://localhost:8025>. ⚠️ It came from the core's notification service; no module
  speaks SMTP.
- **The staff directory**: on `/about`. It lists whoever has signed in at least once — which is not
  a query but a **foreign key**: a position of somebody who never opened the hub cannot be written.
- **The live status**: the band above every public page. If the network cannot be asked it draws
  **nothing**, because four zeros would be the site answering a question it never asked.
  ⚠️ **New in G13**: the band has a hierarchy now — the number is the loudest thing on it, the
  words the quietest, an icon per figure, and the dot that says "of this minute" breathes.

- [ ] **Point 5** — media, contacts, directory and live status work.

---

## Part 6 — Search

`/search?q=` as a visitor: the query is the address, the words you searched for come back marked,
and what you find is only what you could open — the same query filter, not a second rule.

Then sign in and press **⌘K / Ctrl-K** anywhere in the back office: the same rows plus the screens
of the back office. Both lists come from `staffDestinations`, so a screen cannot be reachable from
one and not the other.

**New in G13**: there is a **visible search box** at the top of the back office column, with the
shortcut printed on it. It opens the same palette — it is not a second search.

Search for a word of two letters. It **says** the words were too short rather than answering with
nothing, which was one of the three questions M0 left open.

- [ ] **Point 6** — search has a screen, and the three questions have written answers.

---

## Part 7 — The editor, and what the template still says

This is the part M1 exists for, and the part that changed most after the first run of this sheet: G15
rebuilt how it answers, G14 gave it a publication window, and the colour of 12 September reaches it
through the same renderer the public site uses. On any page of `/staff/wd/content`:

1. **Moving things.** A section moves three ways: dragged by its handle in the outline, with the
   arrows beside it, and **from the page itself** — pick one and it carries a plate with the commands
   its template allows. A block does the same and can be dropped on **any slot of the page, another
   section's included**; a component dragged out of the palette lands **between** two blocks instead
   of at the end. The arrows, and the "Section" select in a block's properties, are the keyboard road,
   and they are not decoration: they are the whole of what works without a mouse.
   ⚠️ **Undo and Redo**, fifty steps, `Ctrl/⌘+Z`, `Ctrl/⌘+Shift+Z` and `Ctrl+Y` — and a sentence typed
   into a block is **one** step to undo rather than twenty, because consecutive changes to the same
   thing collapse into one. The shortcuts do nothing **inside** a field, deliberately: there ⌘Z still
   means "undo what I just typed", which is the browser's job and not ours.
2. **The properties apply while you write.** There is no "Apply" button left: type, and the page
   redraws about a fifth of a second after you stop — measured at ~180 ms. A value the schema refuses
   changes nothing and says so in its own field, so a title emptied halfway through a rewrite does
   not empty the block. The one button still under a form is **"Fix the key"** on a template's
   section, because a key is fixed once and typing would otherwise make "in" the key of "intro".
3. **Nothing is lost.** The draft saves itself **ten seconds** after you stop, and again when you
   leave the page; the line beside the buttons says "Saved at 14:32" / "Saving…" / "Unsaved changes".
   "Publish" flushes the pending save first, so it stays one gesture. A row that does not exist yet is
   never created by an autosave — only "Save draft" creates one — and a 409 stops the autosave and
   says why, because somebody else saved that row and the way on is to reload rather than to
   overwrite every ten seconds. ⚠️ What it costs the shared database is the thing to look at: an
   autosave leaves an `autosaved` audit row with the list of fields that moved and **no body** — about
   200 bytes instead of two copies of the page.
4. **The preview tells the truth.** Three widths, a language, and Draft | Published. It is a
   `max-width` on the very same renderer the public site uses — not an emulator — and at "Phone" a
   two-column section really becomes **one** column, because every width that decides a layout under
   `blocks/` is a container query on the page rather than a media query on the window. Before G15 it
   drew two columns of 167 px inside a frame 390 px wide, and the test that measured the frame passed
   while the preview lied.
5. **A locked section** shows its fields and not its structure, with a line naming **which template**
   fixes it and who may change that. The palette above it says the same rather than offering an
   impossible "Adds to: Welcome" over buttons that are disabled for a good reason.
6. **The differences from the template.** Open a template — `/staff/wd/templates` — add a section to
   it, then reopen a page made from it: the editor says a section was added, and offers to add it —
   **one difference at a time**, never all at once. ⚠️ And the page a visitor reads has not changed,
   and does not change even after you accept the difference into the draft. Only publishing moves
   what the public sees.
7. **Writing a template.** On a template row, a section carries four more fields — `key`, whether
   pages may delete it, whether they may restructure it, and which blocks it allows. Tick one block
   type, save, make a page from that template: its palette offers that block and no other. A template
   is **made from a button** on that screen — choose which kind it is for, and the editor opens on a
   row that is already a template. Opening an existing one says **how many rows were made from it**,
   which is the sentence that stops a careless edit. The screen is behind `Content.ManageTemplates`:
   every staff member *reads* templates, so that "new from a template" works across departments, and
   only whoever may change them sees the screen.
8. **Publishing asks** (new in G14). "Publish" opens a window with **what changed** — the changelog
   line every version could carry since M0 and had never had a box for — and, on a document, the
   **AIRAC cycle** it belongs to. It is `ConfirmDialog`, extended with fields rather than written
   beside as a fifth dialog of its own.

**The comforts, added between 10 and 12 September while Carmine used it:** double-click a block or a
section to pick it and land in the first field of its panel; `Canc` deletes, `⌘D` duplicates, `Esc`
lets go; whatever you pick is scrolled into view; a section duplicates itself, new identifiers and
all; sections nest **four** levels deep; a block with nothing written in it draws a dashed placeholder
with its name, which a visitor never sees; the palette has a search box and the side panels have thin
scrollbars; the file picker uploads into the department's library and links to it; the preview
follows the language you chose; and two identical images uploaded twice are **one** file, answered
with the row that was already there.

**And the colour, 12 September.** A section stands on one of **eight** grounds: `accent` is the
brand's pale blue rather than the fourth grey it used to be, and `aurora` is a fourth dark one beside
`brand`, `deep` and `dark`. `hero`, `cardGrid`, `iconGrid` and `timeline` each take one of four brand
accents, drawn on an icon, on the rule above a card, on the bar above a hero — and never on a word,
because a graphic needs 3 : 1 of its ground and a word needs 4.5 : 1. Headings are no longer the pale
grey Atmosphere paints them.

**And the interactive block, 12 September** — the one block whose content is somebody's code. Sign
in again first (its permission, `Content.EmbedCode`, is new and permissions are computed at sign
in), then find **Interactive drawing** under Content → Media.

1. Under the generated form there is a **Code** box, a byte count, **choose a file from your
   computer** (read in the browser, never uploaded), and two downloads: **the guidelines** you hand
   to whoever writes the animation, and **the local preview**, which runs a fragment from your own
   disk in the same sandboxed frame. Both are generated from the shell the hub wraps the code in.
2. Paste the worked example out of the guidelines and save: the frame shows the **saved draft**, which
   only editors of the page can see. Put the section on a dark ground — the frame stays transparent
   and its ink turns light — then switch the theme, narrow the window to a phone, and print: the
   frame folds away and the description stays.
3. ⚠️ Now paste a line that calls `fetch(...)` and save. The browser refuses it, and **a line under
   the animation names what was refused** — shown to the staff and to nobody else. Paste a whole web
   page instead of a fragment: refused, with the reason, whether pasted or chosen as a file.

- [ ] **Point 7a** — a template never rewrites a page by itself, and the editor says so.
      Asserted by `e2e/full/template.spec.ts`.

---

## Part 8 — The round, and the preview against the public page

The one M0 could not tick, twice over.

1. Create a page from a template, add a heading, a text and a callout, and **press Preview**. Keep
   that on screen.
2. Set it to Public, save, publish.
3. Open `/{slug}` in a **private window**, signed in as nobody.

Compare the two. They are the same page because they are the same renderer — that is the whole
reason the editor does not have a preview of its own. Then:

4. Back in the editor, change the callout and save **without publishing**.
5. Reload the private window. It has not moved.

That last step is the point of the whole milestone: the public reads the published version, and a
draft is private until somebody decides otherwise.

- [ ] **Point 7b** — the round was executed in a browser against the real API, and the editor's
      preview is the public page. Asserted by `e2e/full/round.spec.ts`.

---

## Part 9 — And all of it, asserted

```bash
dotnet build IvaoHub.sln                    # also writes artifacts/openapi/IvaoHub.Web.json
dotnet test --solution IvaoHub.sln --configuration Release

cd web
pnpm lint && pnpm format:check && pnpm typecheck && pnpm test && pnpm i18n:check && pnpm build
pnpm gen:api && git diff --exit-code        # the generated client must not move

pnpm e2e:install                            # once
pnpm e2e                                    # Chromium against the production build
pnpm e2e:full                               # the published application, real API, real database
```

Expect **488 .NET tests** (309 unit, 179 integration against a real MariaDB 11.4.10), **387
Vitest**, **70 Playwright smokes** and **18 of the full round** (counted on 12 September 2026). None
is skipped.

⚠️ **The smoke suite runs under the real content security policy.** `config/security.json` is read by
the backend *and* by Vite's preview server, so every one of those tests would fail on a directive the
application cannot live with; `e2e/security.spec.ts` also watches the console and fails on a single
refusal, because a blocked stylesheet fails no other assertion.

⚠️ `dotnet test --solution` has been seen on Windows to report "Zero tests ran" with exit code 5
**in both configurations** — Release does not avoid it — while the very same binaries pass
everything when run directly:
`tests/IvaoHub.UnitTests/bin/<config>/net10.0/IvaoHub.UnitTests.exe` and the integration one beside
it, which take `-class <FullName>` to run one class. If the run says zero, run the binaries before
believing anything is broken.

⚠️ **Stop the API before building.** With `dotnet run` up, MSBuild fails with `MSB3027`/`MSB3021`
— locked DLLs — and emits **no `CS` error at all**, so `grep "error CS"` reports a clean build that
never happened. Read `Error(s)` in the summary.

`pnpm i18n:check` is green **with the `mail` namespace**, which did not exist in M0: the notification
service writes its templates in `locales/{lang}/mail.json` like everything else.

- [ ] **Point 8** — the backbone tests still pass, the fictional division XX passes with the full
      public site, and `pnpm i18n:check` is green.

---

## Definition of done, in one place

Design M1 §0.1, ticked against the parts above.

| # | | Shown in |
| --- | --- | --- |
| 1 | The public site exists and the code does not draw it; the menu is a table and removing an entry removes it from the site | Part 1 |
| 2 | News and documents are two `kind` of one entity, not two tables | Part 2 |
| 3 | The block set of §1, every block in `/staff/admin/ui-kit`, conventions in `docs/UI-GUIDELINES.md` | Part 3 |
| 4 | One calendar with a UI: public, internal, and as a block | Part 4 |
| 5 | Media, contacts with the core notification service, staff directory, live status | Part 5 |
| 6 | Search has a screen, and the three questions M0 left open have answers | Part 6 |
| 7 | The round in a browser against the real API, and the editor's preview *is* the public page | Parts 7 and 8 |
| 8 | M0's backbone tests all pass, division XX passes with the full public site, `i18n:check` green with `mail` | Part 9 |

If every box is ticked, M1 is done. What that means is worth saying plainly: from here, a new module
is expected to be **screens made of configuration** — a schema, a list of columns, a permission name
and a registration — and the closing report
(`decisions/2026-09-07-m1-review.md`) is where that claim is checked against the numbers rather than
asserted.
