import type { BlockRegistration } from '../shared/modules';

/**
 * The blocks of the core. Empty in F6 on purpose: the registry has to exist before the editor does,
 * because `/staff/admin/ui-kit` mounts every entry of it and a test checks that it does — so the
 * day F7 adds `text`, `richtext` and the rest, they cannot be added without also being shown.
 *
 * A module's blocks are not listed here. They arrive through its manifest and are composed with
 * these by `app/registry.ts`, which is what keeps a module out of the core (design M0 §6.5).
 */
export const coreBlocks: readonly BlockRegistration[] = [];
