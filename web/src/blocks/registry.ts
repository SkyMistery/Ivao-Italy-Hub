import type { BlockRegistration } from '../shared/modules';

import { coreBlockRegistrations } from './core';

/**
 * The blocks of the core. A module's blocks are not listed here: they arrive through its manifest
 * and are composed with these by `app/registry.ts`, which is what keeps a module out of the core
 * (design M0 §6.5).
 *
 * Everything registered here has to appear in `/staff/admin/ui-kit`, and its `example` props have
 * to satisfy its own schema; `features/admin/uiKit.test.ts` is both halves of that. A block nobody
 * can look at is a block that rots.
 */
export const coreBlocks: readonly BlockRegistration[] = coreBlockRegistrations;
