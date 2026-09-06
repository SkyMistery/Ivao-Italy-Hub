import { z } from 'zod';

import type { ChoiceOption } from '../forms';

/**
 * What the contact form holds and the schema it is drawn from. Next to the component rather than
 * inside it, for the reason every block of the registry is three files: a module that exports a
 * component and a constant together loses fast refresh, and that is paid every day (HANDOFF §3).
 */

/** It mirrors `ContactSubmitDto` and carries nothing else. */
export interface ContactFormValues {
  department: string;
  subject: string;
  body: string;
}

/**
 * The schema, as a function of the departments that can be written to: their names are rows of the
 * language files rather than of this form, so they arrive as choices with a label already resolved
 * — the same way the category of a news item does (design M1 §3.4).
 */
export function contactSchema(departments: readonly ChoiceOption[]) {
  return z.object({
    department: z.string().meta({ choices: departments }),
    subject: z.string(),
    body: z.string().meta({ multiline: true }),
  });
}
