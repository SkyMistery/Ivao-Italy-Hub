import { z } from 'zod';

/**
 * The envelope of `body_json`, as zod. It is the one contract the server also knows — id, type,
 * version, props, renderMode, frozen — and nothing in it says what a block *means*: `props` stays
 * opaque here too, and is checked by the schema the block itself registers (design M0 §5.2).
 *
 * The backend validates the same shape with `BlockDocumentWalker`. The lists below are the place
 * where they have to agree by hand: they are values inside an opaque document, so the OpenAPI
 * contract cannot carry them. `BlockDocumentWalker.Layouts`, `RenderModes` and `Backgrounds` are
 * the other half, and the integration test that posts a value the server does not know is what
 * keeps the pair honest.
 */

/** How a section arranges its blocks. */
export const LAYOUTS = ['stacked', '1/2+1/2', '1/3+2/3', '2/3+1/3', '3x1/3'] as const;
export type Layout = (typeof LAYOUTS)[number];

/** What a data block does when the page is read. */
export const RENDER_MODES = ['live', 'frozen'] as const;
export type RenderMode = (typeof RENDER_MODES)[number];

/**
 * What sits behind a section. Four, since G3: `image` is the one that carries something with it —
 * `mediaId`, a file of the library — and it is what the design asked for from the start
 * (design M1 §1.4). A background belongs to the section and never to a block, so that the ground
 * under two blocks does not depend on which two they are.
 */
export const BACKGROUNDS = ['none', 'muted', 'accent', 'image'] as const;
export type Background = (typeof BACKGROUNDS)[number];
export const PADDINGS = ['none', 'sm', 'md', 'lg'] as const;

/**
 * How wide a section is. Four, and not the three of design §1.4: `narrow` was already written by
 * M0 and is carried by bodies that are already published, so removing it would not be an additive
 * change (implementation plan §D, G3 task 4). The design has been corrected to say four.
 */
export const WIDTHS = ['narrow', 'default', 'wide', 'full'] as const;

/** How many columns a layout has; a block may only claim one of them. */
export function columnsOf(layout: Layout | null | undefined): number {
  if (layout === undefined || layout === null || layout === 'stacked') {
    return 1;
  }
  return layout === '3x1/3' ? 3 : 2;
}

export const blockEnvelopeSchema = z.object({
  id: z.string(),
  type: z.string(),
  version: z.number().int().default(1),
  /** Opaque here: the block's own schema is what reads it. */
  props: z.record(z.string(), z.unknown()).default({}),
  renderMode: z.enum(RENDER_MODES).nullish().default(null),
  /** What the provider answered when the page was published. Never written by the editor. */
  frozen: z.unknown().nullish().default(null),
  column: z.number().int().nullish().default(null),
});

export type BlockEnvelope = z.output<typeof blockEnvelopeSchema>;

/**
 * `| undefined` is spelled out on every optional member because the project compiles with
 * `exactOptionalPropertyTypes`: without it, "absent" and "present and undefined" are different
 * types, and zod's `.nullish()` produces the second.
 */
export interface SectionEnvelope {
  id: string;
  key?: string | null | undefined;
  title?: Record<string, string> | null | undefined;
  layout: Layout;
  background: (typeof BACKGROUNDS)[number];
  /** The picture behind the section, when its background is `image`. Ignored otherwise. */
  mediaId?: number | null | undefined;
  padding: (typeof PADDINGS)[number];
  width: (typeof WIDTHS)[number];
  collapsed?: boolean | null | undefined;
  /** Template only: the page made from it may not remove this section. */
  required?: boolean | null | undefined;
  /** Template only: the page made from it may only edit the properties of its blocks. */
  locked?: boolean | null | undefined;
  /** Template only: which block types may be added here. Null means any. */
  allowedBlocks?: string[] | null | undefined;
  renderMode?: RenderMode | null | undefined;
  blocks: BlockEnvelope[];
  sections: SectionEnvelope[];
}

export const sectionSchema: z.ZodType<SectionEnvelope> = z.lazy(() =>
  z.object({
    id: z.string(),
    key: z.string().nullish(),
    title: z.record(z.string(), z.string()).nullish(),
    layout: z.enum(LAYOUTS).default('stacked'),
    background: z.enum(BACKGROUNDS).default('none'),
    mediaId: z.number().int().nullish(),
    padding: z.enum(PADDINGS).default('md'),
    width: z.enum(WIDTHS).default('default'),
    collapsed: z.boolean().nullish(),
    required: z.boolean().nullish(),
    locked: z.boolean().nullish(),
    allowedBlocks: z.array(z.string()).nullish(),
    renderMode: z.enum(RENDER_MODES).nullish(),
    blocks: z.array(blockEnvelopeSchema).default([]),
    sections: z.array(z.lazy(() => sectionSchema)).default([]),
  }),
);

/** The only envelope version M0 reads, the same constant the server holds. */
export const SCHEMA_VERSION = 1;

export const bodySchema = z.object({
  schemaVersion: z.literal(SCHEMA_VERSION),
  sections: z.array(sectionSchema).default([]),
});

export type Body = z.output<typeof bodySchema>;

/** An empty page: what a content row starts with when nobody chose a template. */
export function emptyBody(): Body {
  return { schemaVersion: SCHEMA_VERSION, sections: [] };
}

/**
 * A body that came from the API, which is `unknown` in the contract because the server treats it
 * as opaque. Anything that does not parse is drawn as an empty page rather than crashing the
 * screen: a visitor is never shown a stack trace, and an editor is told by the server instead.
 */
export function readBody(value: unknown): Body {
  const parsed = bodySchema.safeParse(value);
  return parsed.success ? parsed.data : emptyBody();
}

/**
 * The blocks that draw the title of the page they are at the top of: a hero, and a heading somebody
 * set to level one.
 *
 * ⚠️ It exists because every public page had **two** `h1` — the title of the row, drawn `sr-only` by
 * the route, and the block at the top of the body
 * (`decisions/2026-09-07-giro-visivo-m1.md`, finding 4). One of them had to go, and the one to keep
 * is the visible one: the tab and the search result read the row's title from the metadata, which
 * does not need an element in the document.
 *
 * The list lives here, beside the other closed sets of the envelope, and not in the route: which
 * block draws a title is a fact about blocks.
 */
export function startsWithPageTitle(body: Body): boolean {
  const first = body.sections[0]?.blocks[0];
  if (first === undefined) {
    return false;
  }

  return (
    first.type === 'hero' || (first.type === 'heading' && (first.props as { level?: unknown }).level === 1)
  );
}

/** Every block of a body, outer sections first, the way the server enumerates them. */
export function allBlocks(body: Body): BlockEnvelope[] {
  return allSections(body).flatMap((section) => section.blocks);
}

/** Every section of a body, nested ones included, in reading order. */
export function allSections(body: Body): SectionEnvelope[] {
  const flatten = (sections: SectionEnvelope[]): SectionEnvelope[] =>
    sections.flatMap((section) => [section, ...flatten(section.sections)]);

  return flatten(body.sections);
}

/**
 * A new identifier for a section or a block. Short and prefixed, like the ones the seeds carry and
 * the ones the server writes when it copies a template, so that a body is readable by a person
 * looking at the JSON.
 */
export function newId(prefix: 's' | 'b'): string {
  return `${prefix}_${crypto.randomUUID().replaceAll('-', '').slice(0, 8)}`;
}
