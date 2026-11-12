import type { ComponentType } from 'react';
import type { z } from 'zod';

/**
 * What a module contributes to the front end, and the only thing the core ever knows about one.
 *
 * A module is added to the monorepo and recompiled — it is not a plugin loaded at runtime — but the
 * boundary is drawn as if it were, at no cost: all of a module's React code lives in
 * `web/src/modules/<key>/`, `index.ts` there exports exactly one `ModuleManifest`, and
 * `web/src/modules/index.ts` is the explicit list of them, the mirror image of
 * `IvaoHub.Web/Modules.cs` (design M0 §6.5). ESLint keeps it drawn.
 *
 * `BlockRegistration` is the shape the blocks of the core already use, which is the point: a
 * module's blocks and the core's are the same thing, and the shape was decided before there was a
 * module to bend it. The build has none until events opens M2.
 */

/** What a block is made of, spelled the way the server declares it in `/api/me`. */
export type BlockKind = 'Content' | 'Data';

/**
 * Where a block sits in the palette of the editor. The five are the families design M1 §1.2 already
 * named — G3 is titled after four of them and G4 brought the fifth — so this declares a grouping
 * that existed on paper, it does not invent one.
 *
 * ⚠️ A block's group is **code**, declared on its own registration next to its icon and its schema,
 * and a module's blocks declare theirs the same way. There is no screen where somebody arranges the
 * palette, and no table behind it: a palette an editor can rearrange is a second place where the
 * catalogue lives, and the two would drift.
 */
export const BLOCK_GROUPS = ['content', 'layout', 'interactive', 'structure', 'data'] as const;

export type BlockGroup = (typeof BLOCK_GROUPS)[number];

/**
 * The optional second level, for the groups big enough to need one. Closed, like the groups, for
 * one reason: every entry needs a label in every language, and `pnpm i18n:check` can only prove
 * that for a list it can enumerate.
 */
export const BLOCK_SUBGROUPS = [
  'text',
  'media',
  'tables',
  'grids',
  'containers',
  'operational',
  'personal',
] as const;

export type BlockSubgroup = (typeof BLOCK_SUBGROUPS)[number];

/** What every block component is handed. */
export interface BlockComponentProps {
  /**
   * The identifier the block carries in the body. Every component is given it, and one uses it: an
   * interactive block asks for the address of **its own** frame, which contains this
   * (`blocks/embedding.ts`). It is the envelope's own field, so handing it over teaches a component
   * nothing it did not already sit inside.
   */
  readonly id?: string;
  /**
   * Whether whoever is reading this page is a member of staff. Two blocks' worth of things are only
   * ever said to them — a capture rather than a live answer, and an interactive block that the
   * browser had to refuse something to. A visitor is told none of it.
   */
  readonly staff?: boolean;
  /** The properties an editor wrote. Already checked against the block's own schema. */
  readonly props: Record<string, unknown>;
  /**
   * A data block only: what the provider answered. It is the `frozen` capture when the page
   * carries one, and the live answer otherwise; `undefined` while that answer is on its way, and
   * `null` when it could not be had.
   */
  readonly data?: unknown;
}

/** A block an editor can put on a page: its schema, how it is drawn, its example for the ui-kit. */
export interface BlockRegistration {
  /** The type as it appears in `body_json`, for example `text` or `events.upcoming`. */
  readonly type: string;
  /** Matches the descriptor the server publishes; a mismatch is a block drawn from stale code. */
  readonly version: number;
  readonly kind: BlockKind;
  /**
   * A data block that is meaningless captured -- who is online, right now. The editor does not
   * offer the choice and publication never freezes it.
   */
  readonly alwaysLive?: boolean;
  /**
   * The properties of the block, which `SchemaForm` turns into its property form. Both sides of
   * the schema are named: the generator reads the input side to build the fields and the output
   * side to hand them back, and a schema declared with only one of them fits neither.
   */
  readonly schema: z.ZodType<Record<string, unknown>, Record<string, unknown>>;
  readonly component: ComponentType<BlockComponentProps>;
  /** Valid props, mounted in `/staff/admin/ui-kit` and checked by a test. */
  readonly example: Record<string, unknown>;
  /** What the gallery hands a data block instead of calling the server. */
  readonly exampleData?: unknown;
  /** i18n key for the name the editor puts on it, for instance `blocks.text.label`. */
  readonly editorLabelKey: string;
  /**
   * Where the labels of its properties are written, when they are not the core's.
   *
   * The property form reads `<prefix>.fields.<path>` and `<prefix>.options.<path>.<value>`, and the
   * prefix is `blocks.<type>` unless this says otherwise — which is exactly what a module needs:
   * the core's language files do not know what a tour is, and a module writes its own
   * (`flightops:blocks.tourCards`). T9 saw it coming with a block that has no properties at all;
   * T10 is the first that has them.
   */
  readonly propertyLabels?: string;
  /** Which drawer of the palette it appears in. Required: a block with nowhere to be added is a
   * block nobody can use, and `registry.test.ts` refuses one. */
  readonly group: BlockGroup;
  /** The drawer inside the drawer, where the group has them. */
  readonly subgroup?: BlockSubgroup;
  /** From `lucide-react`, like every other icon of the hub (docs/UI-GUIDELINES.md). */
  readonly icon: ComponentType<{ className?: string; 'aria-hidden'?: boolean }>;
  /**
   * A permission whoever adds this block must hold, when adding it is more than adding a heading.
   * The palette hides an entry nobody may use rather than offering it and refusing afterwards.
   *
   * ⚠️ It gates the **palette**, not the renderer: a block already in a body is drawn for whoever
   * may read the page, because the person who put it there was the one who needed the right. One
   * block declares it today — `interactive`, whose source is code (`Content.EmbedCode`).
   */
  readonly permission?: string;
  /**
   * The block has left the palette: it cannot be added any more, and a page that already carries it
   * keeps drawing it, because the type stays registered here **and** on the server (`CLAUDE.md` §2,
   * "a block type is registered in two halves") — taking either half away would refuse the save of
   * every page that holds one (`decisions/2026-10-22-il-piano-dopo-la-prova-dello-staff.md`, III and
   * XXI: the coordination table, whose agreements live on the ATC site, and the button in the text,
   * the same piece as the interactive group's buttons).
   *
   * ⚠️ Like `permission`, it gates the **palette** only: the renderer, the properties panel of a block
   * already in a body and the gallery of the back office still know it.
   */
  readonly retired?: boolean;
  /**
   * Whether the block carries a `source` on its envelope, and therefore a field in the properties
   * panel that the form generator does not draw.
   *
   * ⚠️ A declared exception and not a second editor. The generator draws `props`, and the source of
   * an interactive block is deliberately **not** a property: the server has to read it to serve the
   * frame, and the server never reads inside `props` (plan §16.5). So the panel puts one text area
   * under the generated form, through the same `onEnvelope` that already writes `renderMode` and
   * `column` — which are the envelope too.
   */
  readonly carriesSource?: boolean;
  /**
   * Whether the block may stand on the left, in the middle or on the right of its column — the `align` of its envelope
   * (design of the editor's phase 3, §5.6). Only where it means something: a block that takes the whole column already,
   * a table or a grid of cards, has nothing to move, and its panel offers no choice.
   *
   * The block does not draw it: the renderer's wrapper sets the alignment of its text and pushes a piece narrower than
   * the column — a picture, say — to that side. A row of pieces laid out by `flex`, which neither of the two moves (the
   * buttons of a group), carries `data-align-row`, and the wrapper justifies it. ⚠️ So an alignable block declares no
   * `align` among its own properties: one would be a second alignment of the same thing (`blocks/align.test.tsx`).
   */
  readonly alignable?: boolean;
  /**
   * Whether the block has a width of its own inside its column, and in which of its properties (design of the editor's
   * phase 3, §5.3): today only the picture, `widthPercent`. Like the alignment, the block does not draw it: the
   * renderer's wrapper takes that share of the column — never under 10 rem, and all of it in a column narrower than
   * 24 rem —, draws a handle on its edge while the block is picked and composed, and the editor writes the property.
   */
  readonly resizable?: Resizable;
  /**
   * What the line at the top of the block's properties panel needs besides its words, which sit
   * beside its name — `editorLabelKey` with `.about` for `.label`; a block without that key has no
   * line: the values the words name, and a screen of the hub the line leads to, labelled by the
   * same key with `Link` after it (`blocks.linkList.aboutLink`).
   *
   * It is for a block whose properties do not say where its content comes from — a list of links
   * written on another screen, a frame of another site — which is what staff who did not build the
   * site could not tell (`decisions/2026-10-22-il-piano-dopo-la-prova-dello-staff.md`, IV and VII).
   */
  readonly about?: {
    readonly values?: Readonly<Record<string, string>>;
    readonly href?: string;
  };
}

/**
 * A width a block takes in its column (`BlockRegistration.resizable`): the property that holds it, in percent of the
 * column, and the limits the handle and the panel keep to. The schema accepts any whole number between `min` and `max`,
 * and `step` is the gesture's: an older body's value need not be a multiple of it.
 */
export interface Resizable {
  readonly prop: string;
  readonly min: number;
  readonly max: number;
  readonly step: number;
  /**
   * What a body written before the property existed said instead (the picture's `width`, `third | half | full`, before
   * PR 3.10): the property it wrote, and what each of its values is worth. Such a body is never rewritten for it (design
   * §2, principle 4): the renderer reads it, and the next write of the editor to the block moves it (`blocks/size.ts`).
   */
  readonly old?: {
    readonly prop: string;
    readonly values: Readonly<Record<string, number>>;
  };
}

/**
 * The key of the sentence that says what a block is for: `editorLabelKey` with `.about` for `.label`
 * (`blocks.text.about`, `events:blocks.eventList.about`).
 *
 * Every block has one, in every language of the division, and `app/blockAbout.test.ts` asks it of each
 * (PR 3.0a, `decisions/2026-10-30-una-pagina-scritta-con-claude.md`, answer 1): it is the line at the top
 * of the properties panel, and how the instructions for Claude say which block is for what.
 */
export function aboutKeyOf(block: Pick<BlockRegistration, 'editorLabelKey'>): string {
  return block.editorLabelKey.replace(/\.label$/, '.about');
}

/**
 * A route a module adds. TanStack generates the tree from files, so a module's own route files are
 * what normally appear; this is the escape hatch for a route a manifest would rather register.
 */
export interface RouteDefinition {
  readonly path: string;
  readonly component: ComponentType;
  /**
   * Where the route hangs: the public site, with its header and footer (the default), or the back office,
   * behind the staff guard and inside the staff layout — `/staff/tours/...` (M2, T5). A module's screens of
   * the back office are its own like its public pages, so they arrive the same way.
   * <br />`member` is the third, for a page that only makes sense signed in — the pilot's report form (M2, T11b):
   * under `_member`, like `/contact`, so the sign in guard runs first and an anonymous visitor goes to the login and
   * comes back to the same address.
   */
  readonly area?: 'public' | 'member' | 'staff';
  /** A staff route only: the permission it is behind, held on any department. Without it, `/forbidden`. */
  readonly permission?: string;
  /**
   * A staff route only: `permission` must be held **without a row scope** — a register of the whole division, whose list
   * the server opens with `HasUnscoped`, so that a grant on one event does not lead to a page that answers 403. It is
   * what `NavItemDescriptor(…, Unscoped: true)` says of the menu entry (note 2026-10-17-il-permesso-senza-ambito).
   */
  readonly unscoped?: boolean;
  /** The typed search parameters of the route, as recipe 2 of design M0 §7.3 declares them for a list. */
  readonly validateSearch?: z.ZodType;
}

/**
 * A component a module brings for its own screens (M2, T20c, note 2026-09-25-le-rifiniture-di-m2): it lives in the module,
 * and the gallery of the back office shows it next to the core's, which it cannot import. Adding one is a decision like
 * adding one to the core's closed list: `uiKit.test.ts` writes their names out.
 */
export interface ComponentRegistration {
  readonly name: string;
  /** The component drawn with example data and nothing from the server: the gallery is not a page of this installation. */
  readonly sample: ComponentType;
}

export interface ModuleManifest {
  readonly key: string;
  /**
   * Its blocks, the Data ones included. There are no dashboard tiles apart from them since
   * 13 September 2026: a module's tile is a Data block that answers for whoever is looking, placed on
   * a dashboard with the editor (note 2026-09-13-le-dashboard-a-tutto-schermo).
   */
  readonly blocks: readonly BlockRegistration[];
  readonly routes: readonly RouteDefinition[];
  /** Namespaces of `locales/{lng}/<ns>.json` the module brings with it. */
  readonly i18nNamespaces: readonly string[];
  /** The components of its own, for the gallery; a module with none leaves it out. */
  readonly components?: readonly ComponentRegistration[];
}
