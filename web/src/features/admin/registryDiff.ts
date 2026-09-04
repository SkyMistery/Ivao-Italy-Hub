import type { Bootstrap } from '../../shared/api/bootstrap';
import type { BlockRegistration, WidgetRegistration } from '../../shared/modules';

/**
 * The third side of "registry ⇄ ui-kit": what the **server** declares in `/api/me`, against what
 * this build of the front end registered (design M0 §6.5).
 *
 * The two halves of a module are two projects that are compiled separately — `IvaoHub.Modules.Atc`
 * and `web/src/modules/atc/` — so they can disagree, and the way they disagree is quiet: a block
 * only the server knows draws "unknown block" on a page somebody already published, and a block
 * only the browser knows is offered in the editor and then refused on save, with a message about an
 * envelope. Saying it here, in one place a coordinator can be pointed at, is cheaper than either.
 *
 * A pure function on two lists, so the test does not need a browser, a server or a router.
 */
export interface RegistryDifference {
  /** Declared by the server, with no component in this build. */
  readonly blocksMissingInBrowser: string[];
  /** Registered here, unknown to the server: a page using one would be refused on save. */
  readonly blocksMissingOnServer: string[];
  /** Known to both, at different versions: the same name meaning two things. */
  readonly blockVersionMismatches: string[];
  readonly widgetsMissingInBrowser: string[];
  readonly widgetsMissingOnServer: string[];
}

export function compareRegistries(
  bootstrap: Bootstrap,
  blocks: readonly BlockRegistration[],
  widgets: readonly WidgetRegistration[],
): RegistryDifference {
  const server = bootstrap.registries;

  const serverBlocks = new Map(server.blocks.map((block) => [block.type, block.version]));
  const clientBlocks = new Map(blocks.map((block) => [block.type, block.version]));

  const serverWidgets = new Set(server.widgets.map((widget) => widget.key));
  const clientWidgets = new Set(widgets.map((widget) => widget.key));

  return {
    blocksMissingInBrowser: [...serverBlocks.keys()].filter((type) => !clientBlocks.has(type)),
    blocksMissingOnServer: [...clientBlocks.keys()].filter((type) => !serverBlocks.has(type)),
    blockVersionMismatches: [...serverBlocks.entries()]
      .filter(([type, version]) => clientBlocks.has(type) && clientBlocks.get(type) !== version)
      .map(([type, version]) => `${type} (server ${version}, browser ${clientBlocks.get(type)})`),
    widgetsMissingInBrowser: [...serverWidgets].filter((key) => !clientWidgets.has(key)),
    widgetsMissingOnServer: [...clientWidgets].filter((key) => !serverWidgets.has(key)),
  };
}

/** True when the two sides say exactly the same thing, which is the only acceptable answer. */
export function registriesAgree(difference: RegistryDifference): boolean {
  // Named one by one rather than walked: a field added to the difference and forgotten here would
  // be a disagreement the gallery quietly calls agreement, and the compiler cannot see that.
  return (
    difference.blocksMissingInBrowser.length === 0 &&
    difference.blocksMissingOnServer.length === 0 &&
    difference.blockVersionMismatches.length === 0 &&
    difference.widgetsMissingInBrowser.length === 0 &&
    difference.widgetsMissingOnServer.length === 0
  );
}
