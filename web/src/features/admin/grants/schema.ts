import { z } from 'zod';

import type { Bootstrap } from '../../../shared/api/bootstrap';
import { DEPARTMENTS } from '../../../shared/api/department';

/**
 * The form of a grant. It mirrors `GrantWriteDto` and carries nothing else: the three rules that
 * actually matter — the permission has to be one the catalogue knows, it may never be a global one,
 * and the VID has to be staff of this division — are the server's, and are answered by it with an
 * i18n key per field (design M0 §7.5).
 *
 * The schema is built from the bootstrap rather than written out, because the set of permissions
 * depends on which modules the installation was built with. That is also why `value` is a
 * `.meta({ choices })` string and not a `z.enum`: a `z.enum` is a compile time set, and this one is
 * not knowable until the server says what it has.
 */
export function grantSchema(bootstrap: Bootstrap) {
  // Only the departmental ones. A global permission is refused by the server, and offering one in
  // a select would be offering a choice whose only outcome is a refusal.
  const grantable = bootstrap.registries.permissions
    .filter((permission) => !permission.isGlobal)
    .map((permission) => permission.name);

  return z.object({
    vid: z.number().int(),
    // One kind today. It travels because the contract has it, and it is hidden because there is
    // nothing to choose: a select with one option is a question with one answer.
    kind: z.enum(['Permission']).meta({ hidden: true }),
    value: z.string().meta({ choices: grantable }),
    // Empty means every department. The server stores that as null, which is what
    // `EffectivePermissionsCalculator` reads as "held everywhere".
    department: z.enum(DEPARTMENTS).optional(),
    effect: z.enum(['Grant', 'Deny']),
    expiresAt: z.string().optional(),
    reason: z.string().optional().meta({ multiline: true }),
    // The version the form was loaded with. Sending back a stale one is how the server finds out
    // somebody else saved first, and answers 409.
    rowVersion: z.string().meta({ hidden: true }),
  });
}

export type GrantFormValues = z.output<ReturnType<typeof grantSchema>>;
