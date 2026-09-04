import type { ModuleManifest } from '../shared/modules';

import { atcManifest } from './atc';

/**
 * Explicit list of the frontend module manifests, the mirror image of `IvaoHub.Web/Modules.cs`
 * (design M0 §6.5). Adding a module means adding one line here and one there; no scanning, no
 * dynamic import.
 *
 * Only `app/registry.ts` reads this file — that is the rule ESLint enforces, and the reason the
 * list sits at the boundary rather than inside a module folder.
 */
export const moduleManifests: readonly ModuleManifest[] = [atcManifest];
