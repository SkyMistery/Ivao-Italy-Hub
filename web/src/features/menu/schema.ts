import { z } from 'zod';

import { localized, type ChoiceOption } from '../../shared/forms';

/**
 * The form of a menu entry, as a zod schema mirroring `MenuItemWriteDto`. Types and what is
 * required, and nothing else: the shape of a path and a label in every language are the server's
 * rules and it answers with them (design M0 §7.5).
 *
 * There is no department here, and that is the point of the whole resource: every row belongs to
 * the web team, stated once on the entity, so there is no field with which to move one and nothing
 * has to refuse the move (design M1 §8.1).
 *
 * It is a function because the entries a row may hang under are rows, not a set the code knows.
 */
export function menuItemSchema(parents: readonly ChoiceOption[] = []) {
  return z.object({
    scope: z.enum(['Public', 'Footer']),
    // The identifier of the parent, carried as text because a select whose labels are not its
    // values is a text field with choices — the same shape the category of a news item takes.
    // Empty means "top level", which is what most entries are.
    parentId: z.string().optional().meta({ choices: parents }),
    label: localized(),
    path: z.string(),
    sort: z.number().int(),
    visibility: z.enum(['Public', 'Members', 'Staff', 'Department']),
    isActive: z.boolean(),
    rowVersion: z.string().meta({ hidden: true }),
  });
}

export type MenuItemFormValues = z.output<ReturnType<typeof menuItemSchema>>;
