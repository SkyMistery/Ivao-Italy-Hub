import type { WidgetRegistration } from '../../../shared/modules';

import { WelcomeWidget } from './WelcomeWidget';

/**
 * The dashboard tiles of the core, in the same shape a module registers one: `app/registry.ts`
 * composes these with whatever the manifests bring, and `/me` draws what the server declared in
 * `registries.widgets` (design M0 §6.3).
 *
 * One tile in M0, deliberately. What had to be built is the composition, not a library of tiles.
 */
export const coreWidgets: readonly WidgetRegistration[] = [{ key: 'welcome', component: WelcomeWidget }];
