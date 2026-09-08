import { z } from 'zod';

import { localized, type ChoiceOption, type Suggestion } from '../../shared/forms';

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
export function menuItemSchema(parents: readonly ChoiceOption[] = [], addresses: readonly Suggestion[] = []) {
  return z.object({
    scope: z.enum(['Public', 'Footer']),
    // The identifier of the parent, carried as text because a select whose labels are not its
    // values is a text field with choices — the same shape the category of a news item takes.
    // Empty means "top level", which is what most entries are.
    parentId: z.string().optional().meta({ choices: parents }),
    label: localized(),
    // ⚠️ Two annotations, and they answer two different halves of the same question. `suggestions`
    // offers the addresses that **exist** — the published pages, grouped by the department that
    // wrote them, and the screens the application itself has — because a menu entry that points at
    // nothing is a 404 nobody notices until a visitor finds it. `slugFrom` proposes one from the
    // label for the entry whose page does not exist yet, which is the other half of how a menu is
    // written. Neither is a rule: the value stays free text, or the menu could not link the forum.
    // ⚠️ **Closed**, since 8 September 2026: a menu entry leads to a page of this site, to a screen
    // of the application, or to a link of the library, and to nothing else. The point is not the
    // menu — it is that every address leaving the site lives in one table, so that changing where
    // the forum lives is one row rather than a hunt. `MenuItemWriteDtoValidator` refuses the rest,
    // which is what makes it a rule rather than a habit of this screen.
    //
    // `slugFrom` stays for the entry whose page is written next: a draft counts, so the proposal
    // still meets a row that exists by the time anybody saves.
    path: z.string().meta({
      slugFrom: 'label',
      slugPrefix: '/',
      suggestions: addresses,
      suggestionsOnly: true,
    }),
    sort: z.number().int(),
    visibility: z.enum(['Public', 'Members', 'Staff', 'Department']),
    isActive: z.boolean(),
    rowVersion: z.string().meta({ hidden: true }),
  });
}

export type MenuItemFormValues = z.output<ReturnType<typeof menuItemSchema>>;
