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

/** A block an editor can put on a page: its schema, how it is drawn, its example for the ui-kit. */
export interface BlockRegistration {
  /** The type as it appears in `body_json`, for example `text` or `atc.roster`. */
  readonly type: string;
  /** The properties of the block, which `SchemaForm` turns into its property form. */
  readonly schema: z.ZodType<Record<string, unknown>>;
  readonly component: ComponentType<{ props: Record<string, unknown> }>;
  /** Valid props, mounted in `/staff/admin/ui-kit` and checked by a test. */
  readonly example: Record<string, unknown>;
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
