import { z } from 'zod';

import { DEPARTMENTS } from '../shared/api/department';
import { localized } from '../shared/forms';
import { CALENDAR_VIEWS } from '../shared/ui';

/**
 * What each block of the core holds, as zod. This is the only description of a block's properties
 * anywhere: the backend keeps `props` opaque (CLAUDE.md §2) and the same schema is what
 * `SchemaForm` turns into the property form in the editor.
 *
 * Separate from the components so that each file exports one kind of thing, which is what keeps
 * fast refresh working on the components.
 */

/** The four tones a callout can take. */
export const CALLOUT_TONES = ['info', 'success', 'warning', 'danger'] as const;

/** How far down the outline a heading sits. Four is as deep as a page of this hub ever goes. */
export const HEADING_LEVELS = [1, 2, 3, 4];

export const headingSchema = z.object({
  // A number with choices rather than a `z.enum`: every string inside `props` is extracted as the
  // text of the page for the search index, and "2" is not text (design M0 §5.3). The generator
  // still draws a select, so nobody can type a level that does not exist.
  //
  // ⚠️ It starts at **2**, and the default is the whole of the fix. Without one the generator takes
  // the first choice, so every heading anybody added was an `h1`: `/start` had four of them by the
  // time somebody measured it (`decisions/2026-09-07-giro-visivo-m1.md`, finding 1). A page has one
  // `h1` — its own title — and everything a writer adds under it is a level below.
  level: z.number().int().default(2).meta({ choices: HEADING_LEVELS }),
  text: localized(),
});

export const textSchema = z.object({
  markdown: localized().meta({ multiline: true }),
});

export const calloutSchema = z.object({
  tone: z.enum(CALLOUT_TONES),
  title: localized(),
  text: localized().meta({ multiline: true }),
});

export const ctaSchema = z.object({
  label: localized(),
  href: z.string(),
});

export const linkListSchema = z.object({
  category: z.string(),
  // A choice and not free text: a department typed by hand is a department that can be misspelled,
  // and the server answers a name it does not know with nothing at all rather than with everything.
  department: z.enum(DEPARTMENTS).optional(),
  // The default lives on the field, so a new block starts at ten and nothing else has to know it.
  limit: z.number().int().default(10),
});

// --- the sixteen of G3 -------------------------------------------------------------------------
//
// Two rules shape every schema below, and both come from design M1 §1.5.
//
// A property that is not prose is a `z.enum` or a number with `choices`, never free text: every
// string inside `props` is concatenated into the text of the page for the search index, so an
// alignment or a column count must not be something a member finds by searching for it.
//
// A property that names a file is `.meta({ media: true })` and nothing else: a number to type is a
// page pointing at a file somebody deleted years ago.

/** Where a block sets its content, when it is the kind of block that can be centred. */
export const ALIGNMENTS = ['left', 'center'] as const;

/** The three grounds a block that owns its background may stand on (docs/UI-GUIDELINES.md). */
export const TONES = ['plain', 'muted', 'accent'] as const;

/**
 * The four accents a block may be drawn with (12 September 2026,
 * `decisions/2026-09-12-il-sito-ha-un-colore.md`). Four families of the brand's own palette, a closed
 * set like the grounds and for the same reason: there is no free colour anywhere in this hub.
 *
 * ⚠️ An accent colours **graphics** — an icon, a rule above a card, a bar above a hero — and never a
 * word. WCAG asks 3 : 1 of a graphic and 4.5 : 1 of text, and the brand's orange does not reach the
 * second on every ground it can stand on. `blocks.tsx` holds the pairs and says the numbers.
 *
 * `brand` first because it is the default, and the default is what those blocks already looked like:
 * no page written before today changes when it is read after it.
 */
export const ACCENTS = ['brand', 'ocean', 'aurora', 'artifice'] as const;

/** How many columns a grid has. Numbers, so the count never reads as text of the page. */
export const GRID_COLUMNS = [2, 3, 4];

/** How much of its column a picture takes. */
export const IMAGE_WIDTHS = ['third', 'half', 'full'] as const;

/** The shape a video is drawn in. */
export const ASPECTS = ['16x9', '4x3', '1x1'] as const;

/** A row of steps, or a column of moments. Same properties, two drawings. */
export const TIMELINE_VARIANTS = ['steps', 'timeline'] as const;

/** How a cell is set. Kept apart from `ALIGNMENTS` because a table cell also right-aligns. */
export const CELL_ALIGNMENTS = ['left', 'center', 'right'] as const;

/** Which button of a group is the one being asked for. */
export const BUTTON_VARIANTS = ['primary', 'secondary', 'ghost'] as const;

/** How much air a spacer adds where a section's own padding is not the answer. */
export const SPACER_SIZES = ['sm', 'md', 'lg', 'xl'] as const;

/** What a divider is drawn with. */
export const DIVIDER_VARIANTS = ['line', 'dots'] as const;

/** How much room a divider takes above and below itself. */
export const DIVIDER_SPACINGS = ['sm', 'md', 'lg'] as const;

/**
 * A button an editor writes: somewhere to go, and what to call it. Written once because two blocks
 * carry one — the pair of a `hero`, the entries of a `buttonGroup` — and a second description of
 * the same pair is a second place for it to drift.
 */
const linkFields = { label: localized(), href: z.string() };

export const heroSchema = z.object({
  eyebrow: localized().optional(),
  title: localized(),
  text: localized().optional().meta({ multiline: true }),
  mediaId: z.number().int().optional().meta({ media: true }),
  align: z.enum(ALIGNMENTS).default('left'),
  // A hero is one of the three blocks whose identity *is* its ground (docs/UI-GUIDELINES.md), so
  // it carries a tone of its own where an ordinary block leaves that to its section.
  tone: z.enum(TONES).default('muted'),
  accent: z.enum(ACCENTS).default('brand'),
  primary: z.object(linkFields).optional(),
  secondary: z.object(linkFields).optional(),
});

export const imageSchema = z.object({
  mediaId: z.number().int().meta({ media: true }),
  // Empty is a decorative picture, and that is what it renders as. It is not inherited from the
  // library: the public renderer is handed a published body and nothing else, and a server that
  // filled it in would have to read inside `props`, which is the one thing it must not do
  // (plan §16.5; decision of 6 September 2026).
  alt: localized().optional(),
  caption: localized().optional(),
  width: z.enum(IMAGE_WIDTHS).default('full'),
  rounded: z.boolean().default(true),
});

export const videoSchema = z.object({
  // Either an address on a host the hub knows how to frame, or a file of the library. Both are
  // optional because either one is enough; the block says so when it has neither.
  url: z.string().optional(),
  mediaId: z.number().int().optional().meta({ media: true }),
  caption: localized().optional(),
  aspect: z.enum(ASPECTS).default('16x9'),
});

export const embedSchema = z.object({
  url: z.string(),
  // Not decoration: it is the name of the frame, and without it somebody reading with a keyboard
  // finds a box with nothing to say what is inside (design M1 §1.2).
  title: localized(),
  height: z.number().int().default(480),
});

export const timelineSchema = z.object({
  variant: z.enum(TIMELINE_VARIANTS).default('steps'),
  accent: z.enum(ACCENTS).default('brand'),
  items: z.array(
    z.object({
      title: localized(),
      text: localized().optional().meta({ multiline: true }),
      // A real day, held as an ISO instant, drawn by the field G2 built. Steps have none, and an
      // entry without one simply shows no date.
      date: z.string().optional().meta({ date: true }),
      icon: z.string().optional().meta({ icon: true }),
    }),
  ),
});

/**
 * ⚠️ The rows are a list of objects holding a list of objects, and not the `rows[][]` of design
 * §1.2. The generator draws a list of *objects*; a list of bare values is a kind of field it has
 * not got, and inventing one for this single schema would be a sixth extension to the form
 * generator for a shape nothing else asks for. The wrapper costs one key in the JSON and nothing
 * in the editor, which is the cheaper of the two.
 */
export const tableSchema = z.object({
  caption: localized().optional(),
  columns: z.array(z.object({ label: localized(), align: z.enum(CELL_ALIGNMENTS).default('left') })),
  rows: z.array(z.object({ cells: z.array(z.object({ text: localized() })) })),
});

// ---- the operational document (G14) --------------------------------------------------------

/** What a station is, as the network suffixes a callsign. */
export const STATION_KINDS = ['DEL', 'GND', 'TWR', 'APP', 'DEP', 'CTR', 'FSS', 'ATIS'] as const;

/**
 * The frequencies of a SOP: one row per station, callsign and frequency as they are written on
 * the network, whether the position takes CPDLC, the rating asked of whoever opens it, and a note.
 * Callsigns and frequencies are not prose and are not translated — and, by the rule of 9 September
 * 2026, not indexed for search either; the note is the one thing that is.
 */
export const frequencyTableSchema = z.object({
  caption: localized().optional(),
  stations: z.array(
    z.object({
      callsign: z.string(),
      frequency: z.string(),
      kind: z.enum(STATION_KINDS).default('TWR'),
      cpdlc: z.boolean().default(false),
      // Free text rather than a list of ratings: a division writes "AS3" where another writes
      // "ADC", and a rating that is not required is simply left empty.
      minimumRating: z.string().optional(),
      note: localized().optional(),
    }),
  ),
});

export const COORDINATION_DIRECTIONS = ['inbound', 'outbound', 'both'] as const;

/**
 * The agreements of a LoA: who hands what to whom, where and at which level. One row per
 * agreement; the note is where the prose goes, and it is the one translated thing in the row.
 */
export const coordinationSchema = z.object({
  caption: localized().optional(),
  agreements: z.array(
    z.object({
      from: z.string(),
      to: z.string(),
      point: z.string().optional(),
      level: z.string().optional(),
      direction: z.enum(COORDINATION_DIRECTIONS).default('both'),
      note: localized().optional().meta({ multiline: true }),
    }),
  ),
});

export const cardGridSchema = z.object({
  columns: z.number().int().default(3).meta({ choices: GRID_COLUMNS }),
  accent: z.enum(ACCENTS).default('brand'),
  cards: z.array(
    z.object({
      title: localized(),
      text: localized().optional().meta({ multiline: true }),
      mediaId: z.number().int().optional().meta({ media: true }),
      href: z.string().optional(),
      icon: z.string().optional().meta({ icon: true }),
    }),
  ),
});

export const iconGridSchema = z.object({
  columns: z.number().int().default(3).meta({ choices: GRID_COLUMNS }),
  accent: z.enum(ACCENTS).default('brand'),
  items: z.array(
    z.object({
      icon: z.string().meta({ icon: true }),
      title: localized(),
      text: localized().optional().meta({ multiline: true }),
    }),
  ),
});

export const gallerySchema = z.object({
  // One object per picture, for the same reason the table's rows are: a list of bare numbers is
  // not something the generator draws, and a media is never a number anyway.
  images: z.array(z.object({ mediaId: z.number().int().meta({ media: true }) })),
  columns: z.number().int().default(3).meta({ choices: GRID_COLUMNS }),
  // The picture becomes a link to the file itself, opened in a tab of its own. Not a dialog of our
  // own making: that would be a custom component, and the closed list is closed (design M1 §12).
  lightbox: z.boolean().default(true),
});

export const logoGridSchema = z.object({
  columns: z.number().int().default(4).meta({ choices: GRID_COLUMNS }),
  items: z.array(
    z.object({
      mediaId: z.number().int().meta({ media: true }),
      // The name of a partner is a name, not a sentence: it reads the same in every language, and
      // it is what a reader who cannot see the logo is told.
      name: z.string(),
      href: z.string().optional(),
    }),
  ),
});

/**
 * ⚠️ `tabs` and `accordion` carry markdown per entry and never blocks (design M1 §1.5). A block
 * containing blocks would be a second tree, with a second validator, a second editor and a second
 * way of getting the depth wrong. What is inside is the same sanitized `MarkdownContent` as `text`.
 */
export const tabsSchema = z.object({
  tabs: z.array(z.object({ label: localized(), body: localized().meta({ multiline: true }) })),
});

export const accordionSchema = z.object({
  allowMultiple: z.boolean().default(false),
  items: z.array(z.object({ question: localized(), answer: localized().meta({ multiline: true }) })),
});

export const testimonialSchema = z.object({
  quote: localized().meta({ multiline: true }),
  // A person's name, written once: the same string in every language.
  author: z.string(),
  role: localized().optional(),
  mediaId: z.number().int().optional().meta({ media: true }),
});

export const buttonGroupSchema = z.object({
  align: z.enum(CELL_ALIGNMENTS).default('left'),
  buttons: z.array(z.object({ ...linkFields, variant: z.enum(BUTTON_VARIANTS).default('primary') })),
});

/**
 * Air, where a section's own padding is not the answer — between two blocks that belong together
 * and two that do not. It exists for the declared exception and not to make up for inconsistent
 * margins: a block never carries a margin of its own (docs/UI-GUIDELINES.md).
 */
export const spacerSchema = z.object({
  size: z.enum(SPACER_SIZES).default('md'),
});

export const dividerSchema = z.object({
  variant: z.enum(DIVIDER_VARIANTS).default('line'),
  spacing: z.enum(DIVIDER_SPACINGS).default('md'),
});

// --- the six data blocks of G4 -----------------------------------------------------------------
//
// A data block draws what the hub knows rather than what an editor typed, so its properties are
// not its content: they are the question. What comes back is `data`, from the provider registered
// for the same type on the server (design M1 §1.2, group Data).
//
// Two shapes below are lists of objects holding a single value — `metrics[] { metric }`,
// `figures[] { figure }`, `kinds[] { kind }` — and that is deliberate. The generator draws lists of
// *objects*; a list of bare values is a kind of field it has not got, and inventing one for three
// schemas would be a sixth extension for a shape nothing else asks for. It is the same trade G3
// made for `table.rows` and `gallery.images`, and it costs one key in the JSON.

/**
 * The figures of the division a `stats` block may show. A closed set, and this is it: a module
 * that wants a number of its own registers a block of its own (design M1 §1.2, correction 2).
 *
 * ⚠️ Compound words on purpose. Every string inside `props` is concatenated into the text of the
 * page for the search index (design M1 §1.5), and a metric spelled `news` would be a page that
 * answers a search for news.
 */
export const STATS_METRICS = [
  'knownMembers',
  'staffMembers',
  'publishedNews',
  'publishedDocuments',
  'upcomingEntries',
] as const;

/** What `networkStats` can count: connections here, and connections anywhere. */
export const NETWORK_FIGURES = ['divisionAtc', 'divisionPilots', 'networkAtc', 'networkPilots'] as const;

/** How far ahead a calendar block looks. */
export const CALENDAR_RANGES = ['upcoming', 'week', 'month'] as const;

/** How a list of rows is set out. */
export const LIST_LAYOUTS = ['list', 'cards'] as const;

export const statsSchema = z.object({
  metrics: z.array(z.object({ metric: z.enum(STATS_METRICS) })),
  columns: z.number().int().default(3).meta({ choices: GRID_COLUMNS }),
});

export const networkStatsSchema = z.object({
  figures: z.array(z.object({ figure: z.enum(NETWORK_FIGURES) })),
  // Who is on frequency right now, under the figures. Off by default: a page that wants the two
  // numbers and not the list is the common one.
  showPositions: z.boolean().default(false),
});

export const calendarSchema = z.object({
  // Free strings, because the staff writes them: `meeting`, `deadline`, whatever a department
  // uses. Naming none asks for every kind.
  kinds: z.array(z.object({ kind: z.string() })),
  department: z.enum(DEPARTMENTS).optional(),
  range: z.enum(CALENDAR_RANGES).default('upcoming'),
  // Born in G6 with `CalendarView`, exactly as design M1 §1.2 said it would: in G4 the block was
  // the agenda and only the agenda, and a select with one option is a control that does nothing.
  // Additive, with a default, so bodies written before it keep reading as the agenda.
  view: z.enum(CALENDAR_VIEWS).default('agenda'),
  limit: z.number().int().default(5),
});

export const newsListSchema = z.object({
  category: z.string(),
  department: z.enum(DEPARTMENTS).optional(),
  limit: z.number().int().default(3),
  layout: z.enum(LIST_LAYOUTS).default('cards'),
  pinnedFirst: z.boolean().default(true),
});

export const documentListSchema = z.object({
  category: z.string(),
  department: z.enum(DEPARTMENTS).optional(),
  limit: z.number().int().default(10),
  groupByCategory: z.boolean().default(true),
});

export const staffListSchema = z.object({
  department: z.enum(DEPARTMENTS).optional(),
  includeFirStaff: z.boolean().default(true),
  layout: z.enum(LIST_LAYOUTS).default('cards'),
});
