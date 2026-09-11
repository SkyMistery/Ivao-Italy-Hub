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
 * module to bend it. `atc` is the first module, and in M0 the only one.
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
export const BLOCK_SUBGROUPS = ['text', 'media', 'tables', 'grids', 'containers'] as const;

export type BlockSubgroup = (typeof BLOCK_SUBGROUPS)[number];

/** What every block component is handed. */
export interface BlockComponentProps {
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
  /** The type as it appears in `body_json`, for example `text` or `atc.roster`. */
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
  /** Which drawer of the palette it appears in. Required: a block with nowhere to be added is a
   * block nobody can use, and `registry.test.ts` refuses one. */
  readonly group: BlockGroup;
  /** The drawer inside the drawer, where the group has them. */
  readonly subgroup?: BlockSubgroup;
  /** From `lucide-react`, like every other icon of the hub (docs/UI-GUIDELINES.md). */
  readonly icon: ComponentType<{ className?: string; 'aria-hidden'?: boolean }>;
}

/** A tile on a dashboard. Registered in M0, drawn from M1. */
export interface WidgetRegistration {
  readonly key: string;
  readonly component: ComponentType;
}

/**
 * A route a module adds. TanStack generates the tree from files, so a module's own route files are
 * what normally appear; this is the escape hatch for a route a manifest would rather register.
 */
export interface RouteDefinition {
  readonly path: string;
  readonly component: ComponentType;
}

export interface ModuleManifest {
  readonly key: string;
  readonly blocks: readonly BlockRegistration[];
  readonly widgets: readonly WidgetRegistration[];
  readonly routes: readonly RouteDefinition[];
  /** Namespaces of `locales/{lng}/<ns>.json` the module brings with it. */
  readonly i18nNamespaces: readonly string[];
}
