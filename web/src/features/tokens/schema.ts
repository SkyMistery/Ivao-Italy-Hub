import { z } from 'zod';

import type { ChoiceOption } from '../../shared/forms/schema';

/**
 * The form of a new personal token (M2, T19a), mirroring `PersonalTokenWriteDto`. What it is for is a choice among the
 * audiences the bootstrap says this member may use — known only at runtime, so `choices` and not a `z.enum`, each with the
 * word its module gives it (T19b). Every rule (the name, the audience, 1 to 90 days, ten live tokens) is the server's, and
 * the server answers it field by field.
 */
export function tokenSchema(audiences: readonly ChoiceOption[]) {
  return z.object({
    name: z.string(),
    audience: z.string().meta({ choices: [...audiences] }),
    days: z.number().int(),
  });
}

export type TokenFormValues = z.output<ReturnType<typeof tokenSchema>>;

/** The form as it opens: the first audience, and the longest life the hub allows. */
export function emptyToken(audiences: readonly string[]): TokenFormValues {
  return { name: '', audience: audiences[0] ?? '', days: 90 };
}

/**
 * The i18n key of an audience's word: an audience is named after its module (`flightops.agent`), and the module words it in
 * its own file under `tokenAudiences.{name}` — the core never names a module.
 */
export function audienceWordKey(audience: string): string {
  const dot = audience.indexOf('.');
  return dot < 0 ? audience : `${audience.slice(0, dot)}:tokenAudiences.${audience.slice(dot + 1)}`;
}
