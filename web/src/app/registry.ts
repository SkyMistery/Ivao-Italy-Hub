import { coreBlocks } from '../blocks/registry';
import { moduleManifests } from '../modules';
import type { BlockRegistration, ComponentRegistration, RouteDefinition } from '../shared/modules';

/**
 * The loader of the module manifests. This file, and only this file, reads
 * `web/src/modules/index.ts`: everything else in the core sees the composed result and never a
 * module (design M0 §6.5, enforced by `import-x/no-restricted-paths`).
 *
 * Composing zero manifests is the same code as composing three, which is why this existed before
 * there was a module: adding one is a line in `web/src/modules/index.ts` and nothing here.
 */

/** A module's component, with the module it came from: the gallery names both. */
export interface ModuleComponent extends ComponentRegistration {
  readonly module: string;
}

export interface Registry {
  readonly blocks: readonly BlockRegistration[];
  readonly routes: readonly RouteDefinition[];
  readonly i18nNamespaces: readonly string[];
  readonly components: readonly ModuleComponent[];
}

export function composeRegistry(): Registry {
  const blocks: BlockRegistration[] = [...coreBlocks];
  const routes: RouteDefinition[] = [];
  const namespaces = new Set<string>(['common', 'errors']);
  const components: ModuleComponent[] = [];

  for (const manifest of moduleManifests) {
    for (const block of manifest.blocks) {
      if (blocks.some((existing) => existing.type === block.type)) {
        // Two blocks answering to one name is a bug that only shows up as the wrong thing being
        // drawn on a page, which is exactly the kind of bug that takes a day to find.
        throw new Error(`Block type "${block.type}" is registered twice; module "${manifest.key}" is one.`);
      }
      blocks.push(block);
    }

    routes.push(...manifest.routes);
    for (const namespace of manifest.i18nNamespaces) {
      namespaces.add(namespace);
    }

    components.push(
      ...(manifest.components ?? []).map((component) => ({ ...component, module: manifest.key })),
    );
  }

  return { blocks, routes, i18nNamespaces: [...namespaces], components };
}

/** Composed once: the list is static, so recomposing it per render would only cost renders. */
export const registry: Registry = composeRegistry();
