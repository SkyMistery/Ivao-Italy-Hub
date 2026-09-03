import { z } from 'zod';

import type { operations } from '../api/schema';

/**
 * What every list of the hub accepts. Not a convention: the server declares `CrudListRequest` in
 * the contract, so these are the same five parameters, spelled the same way (design M0 §3.9 and
 * §7.3). The check below is what keeps them the same.
 *
 * `filter[name]=value` is deliberately absent, here as there: its names are the properties of the
 * entity, so it cannot be one type. The allow list is `CrudOptions.Filterable` on the server, and a
 * name outside it is answered with 400 rather than ignored.
 */
export const listSearchSchema = z.object({
  page: z.number().int().min(1).default(1),
  pageSize: z.number().int().min(1).max(100).default(25),
  sort: z.string().optional(),
  dir: z.enum(['asc', 'desc']).default('asc'),
  q: z.string().optional(),
});

export type ListSearch = z.output<typeof listSearchSchema>;

/** The query the generated client sends. It is what the route's search parameters become. */
type ContractQuery = NonNullable<operations['LinksList']['parameters']['query']>;

// Every parameter the route validates has to exist in the contract, with the same type. A
// parameter the server does not know would be dropped silently; one it renamed would stop working
// silently. Neither compiles.
// `-?` matters: without it an optional parameter maps to an optional property, and indexing the
// result would hand back `undefined` rather than the name of a key that does not match.
type Mismatch = {
  [K in keyof ListSearch]-?: K extends keyof ContractQuery
    ? NonNullable<ListSearch[K]> extends NonNullable<ContractQuery[K]>
      ? never
      : K
    : K;
}[keyof ListSearch];
const _searchMatchesTheContract: [Mismatch] extends [never] ? true : never = true;
void _searchMatchesTheContract;

/** The search parameters as the client sends them; `undefined` entries are simply not sent. */
export function toQuery(search: ListSearch): ContractQuery {
  return {
    page: search.page,
    pageSize: search.pageSize,
    ...(search.sort === undefined ? {} : { sort: search.sort }),
    dir: search.dir,
    ...(search.q === undefined || search.q === '' ? {} : { q: search.q }),
  };
}

/**
 * How `filter[name]=value` is spelled, in one place, because the server spells it in one place too.
 * It is not part of `CrudListRequest` — its names are the properties of the entity — so the
 * generated client cannot type it, and a screen writing the brackets by hand is a screen that can
 * get them wrong (see `CrudOptions.Filterable`; a name outside the allow list is answered with 400).
 */
export function listQuerySerializer(filters: Readonly<Record<string, string>>) {
  return (query: Record<string, unknown>): string => {
    const params = new URLSearchParams();

    for (const [key, value] of Object.entries(query)) {
      // Only the five parameters of `CrudListRequest` reach here, and they are all primitives; a
      // value of any other shape is a caller mistake and is dropped rather than sent as
      // "[object Object]".
      if (typeof value === 'string' && value !== '') {
        params.set(key, value);
      } else if (typeof value === 'number' || typeof value === 'boolean') {
        params.set(key, String(value));
      }
    }

    for (const [name, value] of Object.entries(filters)) {
      params.set(`filter[${name}]`, value);
    }

    return params.toString();
  };
}
