import { z } from 'zod';

import { DEPARTMENTS } from '../../shared/api/department';
import { localized, type ChoiceOption } from '../../shared/forms';

/**
 * The form of an award, mirroring `AwardWriteDto`. Types and what is required, and nothing else: a
 * name in every language is the server's rule and is answered by it (design M0 §7.5).
 */
export const awardSchema = z.object({
  // Fixed before the form opens, like the department of a link: chosen on the list, kept by an
  // existing award.
  ownerDepartment: z.enum(DEPARTMENTS).meta({ hidden: true }),
  name: localized(),
  description: localized().meta({ localized: true, multiline: true }),
  // What somebody has to have done: the sentence whoever assigns it checks against.
  criteria: localized().meta({ localized: true, multiline: true }),
  // Uploaded by hand into the library: IVAO's documentation of award images answers 403.
  imageMediaId: z.number().int().optional().meta({ media: true }),
  isActive: z.boolean(),
  rowVersion: z.string().meta({ hidden: true }),
});

export type AwardFormValues = z.output<typeof awardSchema>;

/**
 * The form of an assignment, mirroring `AwardAssignmentWriteDto`. A function, because the awards on
 * offer are rows and not a set the code knows; the identifier travels as text, the shape a select
 * whose labels are not its values takes everywhere in the hub.
 */
export function awardAssignmentSchema(awards: readonly ChoiceOption[] = []) {
  return z.object({
    awardId: z.string().meta({ choices: awards }),
    vid: z.number().int(),
    reason: z.string().meta({ multiline: true }),
    // The line of the queue being answered, carried from the queue; read by the server on creation only.
    signalId: z.number().int().optional().meta({ hidden: true }),
    rowVersion: z.string().meta({ hidden: true }),
  });
}

export type AwardAssignmentFormValues = z.output<ReturnType<typeof awardAssignmentSchema>>;
