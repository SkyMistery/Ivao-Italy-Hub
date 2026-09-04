import type { ModuleManifest } from '../../shared/modules';

import { AtcPage } from './AtcPage';

/**
 * Everything the ATC operations module offers the front end, in one object. `app/registry.ts`
 * composes this with the core's own registrations and with the other modules'; nothing else in the
 * application imports anything from this folder (design M0 §6.5, enforced by ESLint).
 *
 * The mirror image of `IvaoHub.Modules.Atc/AtcModule.cs`, and it has to stay one: an integration
 * test reads `/api/me` and this file and fails when the two sides declare different blocks.
 */
export const atcManifest: ModuleManifest = {
  key: 'atc',
  // None yet. A block of a module is named after it — `atc.roster` — so that the one registry can
  // hold the core's and every module's without a collision being possible by accident.
  blocks: [],
  widgets: [],
  routes: [{ path: '/atc', component: AtcPage }],
  i18nNamespaces: ['atc'],
};
