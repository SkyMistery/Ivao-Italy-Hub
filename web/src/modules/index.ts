import type { ModuleManifest } from '../shared/modules';

import { flightOpsManifest } from './flightops';
import { trainingManifest } from './training';

/**
 * Explicit list of the frontend module manifests, the mirror image of `IvaoHub.Web/Modules.cs`
 * (design M0 §6.5). Adding a module means adding one line here and one there; no scanning, no
 * dynamic import.
 *
 * Empty from 13 September 2026, when the ATC module left together with vIPI (note
 * `2026-09-13-staccarsi-da-vipi`), until the tours opened M2 (T5, 16 September 2026); the training
 * followed with M3 (A4, 25 September 2026).
 *
 * Only `app/registry.ts` reads this file — that is the rule ESLint enforces, and the reason the
 * list sits at the boundary rather than inside a module folder.
 */
export const moduleManifests: readonly ModuleManifest[] = [flightOpsManifest, trainingManifest];
