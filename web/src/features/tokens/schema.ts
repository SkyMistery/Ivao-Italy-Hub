import { z } from 'zod';

/**
 * The form of a new personal token (M2, T19a), mirroring `PersonalTokenWriteDto`. What it is for is a choice among the
 * audiences the bootstrap says this member may use — known only at runtime, so `choices` and not a `z.enum`. Every rule
 * (the name, the audience, 1 to 90 days, ten live tokens) is the server's, and the server answers it field by field.
 */
export function tokenSchema(audiences: readonly string[]) {
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
