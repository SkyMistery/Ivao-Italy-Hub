import { coreBlocks } from '../blocks/registry';
import { coreWidgets } from '../features/me/widgets';
import { moduleManifests } from '../modules';
import type { BlockRegistration, RouteDefinition, WidgetRegistration } from '../shared/modules';

/**
 * The loader of the module manifests. This file, and only this file, reads
 * `web/src/modules/index.ts`: everything else in the core sees the composed result and never a
 * module (design M0 §6.5, enforced by `import-x/no-restricted-paths`).
 *
 * Composing zero manifests is the same code as composing three, which is why this existed before
 * there was a module: adding one is a line in `web/src/modules/index.ts` and nothing here.
 */

export interface Registry {
  readonly blocks: readonly BlockRegistration[];
  readonly widgets: readonly WidgetRegistration[];
  readonly routes: readonly RouteDefinition[];
  readonly i18nNamespaces: readonly string[];
}

export function composeRegistry(): Registry {
  const blocks: BlockRegistration[] = [...coreBlocks];
  const widgets: WidgetRegistration[] = [...coreWidgets];
  const routes: RouteDefinition[] = [];
  const namespaces = new Set<string>(['common', 'errors']);

  for (const manifest of moduleManifests) {
    for (const block of manifest.blocks) {
      if (blocks.some((existing) => existing.type === block.type)) {
        // Two blocks answering to one name is a bug that only shows up as the wrong thing being
        // drawn on a page, which is exactly the kind of bug that takes a day to find.
        throw new Error(`Block type "${block.type}" is registered twice; module "${manifest.key}" is one.`);
      }
      blocks.push(block);
    }

    for (const widget of manifest.widgets) {
      if (widgets.some((existing) => existing.key === widget.key)) {
        throw new Error(`Widget "${widget.key}" is registered twice; module "${manifest.key}" is one.`);
      }
      widgets.push(widget);
    }

    routes.push(...manifest.routes);
    for (const namespace of manifest.i18nNamespaces) {
      namespaces.add(namespace);
    }
  }

  return { blocks, widgets, routes, i18nNamespaces: [...namespaces] };
}

/** Composed once: the list is static, so recomposing it per render would only cost renders. */
export const registry: Registry = composeRegistry();
