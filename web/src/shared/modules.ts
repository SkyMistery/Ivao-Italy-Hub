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
 * Nothing here exists yet: the first module is `atc`, in F8. The types and the loader are in F6 so
 * that the shape a module has to fit is decided before there is one to bend it.
 */

/** What a block is made of, spelled the way the server declares it in `/api/me`. */
export type BlockKind = 'Content' | 'Data';

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
