import { z } from 'zod';

import { DEPARTMENTS } from '../../shared/api/department';

/**
 * What a visitor may narrow a public list by. It is a search parameter and not a path segment
 * because it is a *view* of the same list, and because a filter that is part of the address is a
 * filter somebody can send to somebody else — which is the whole reason it is in the URL at all.
 *
 * `catch` on each: a hand-edited address with nonsense in it shows the whole list rather than an
 * error page. There is nothing here worth refusing over.
 */
export const publicListSearchSchema = z.object({
  category: z.string().optional().catch(undefined),
  department: z.enum(DEPARTMENTS).optional().catch(undefined),
});

export type PublicListSearch = z.output<typeof publicListSearchSchema>;
