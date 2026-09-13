import { queryOptions } from '@tanstack/react-query';

import type { Department } from '../../shared/api/bootstrap';
import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import { listQuerySerializer, toQuery, type ListSearch } from '../../shared/list';

/**
 * Every call this feature makes, as query options. A component never fetches: it asks for these
 * and React Query decides whether that means a round trip (design M0 §7.4).
 */

export type ContentListDto = components['schemas']['ContentListDto'];
export type ContentDetailDto = components['schemas']['ContentDetailDto'];
export type ContentWriteDto = components['schemas']['ContentWriteDto'];
export type PublicContentDto = components['schemas']['PublicContentDto'];
export type ContentKind = components['schemas']['ContentKind'];
/** SOP or LoA: what an operational document is, when it is one (G14). Never null on a form. */
export type DocumentType = NonNullable<components['schemas']['DocumentType']>;
export type AirspaceListingDto = components['schemas']['AirspaceListingDto'];
export type ContentPage = components['schemas']['PagedResultOfContentListDto'];
export type ContentPublishProblemsDto = components['schemas']['ContentPublishProblemsDto'];

export const contentKey = ['content'] as const;

export function contentListKey(department: Department, search: ListSearch, kind: ContentKind | null) {
  return [...contentKey, 'list', department, kind, search] as const;
}

export function contentDetailKey(id: number) {
  return [...contentKey, 'detail', id] as const;
}

export function templateListKey(department: Department, search: ListSearch) {
  return [...contentKey, 'template-list', department, search] as const;
}

export function menuDestinationsKey(q: string) {
  return [...contentKey, 'menu-destinations', q] as const;
}

export function madeFromTemplateKey(templateId: number) {
  return [...contentKey, 'made-from', templateId] as const;
}

export function successorsKey(department: Department) {
  return [...contentKey, 'successors', department] as const;
}

export const airspaceKey = ['ref', 'airspace'] as const;

export function publishProblemsKey(id: number) {
  return [...contentKey, 'publish-problems', id] as const;
}

export function templatesKey(kind: ContentKind | null) {
  return [...contentKey, 'templates', kind] as const;
}

export function publicContentKey(kind: ContentKind, slug: string) {
  return [...contentKey, 'public', kind, slug] as const;
}

/**
 * One page of the content of a department. Templates are not in it: the server keeps them out
 * unless a caller asks, which is what the template picker does through `templatesQuery`.
 */
export function contentListQuery(
  department: Department,
  search: ListSearch,
  kind: ContentKind | null = null,
) {
  return queryOptions({
    queryKey: contentListKey(department, search, kind),
    queryFn: async (): Promise<ContentPage> =>
      unwrap(
        await api.GET('/api/content', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({
            ownerDepartment: department,
            ...(kind === null ? {} : { kind }),
          }),
        }),
      ),
  });
}

/**
 * The templates **of one department**, as its own screen lists them: every kind together, because a
 * department has a handful and splitting them into three lists would be three screens for nine rows.
 *
 * It is the same list endpoint as everything else with the default filter turned round — the server
 * keeps templates out unless a caller asks — so paging, sorting and searching are the ordinary ones
 * and there is nothing new behind this.
 */
export function templateListQuery(department: Department, search: ListSearch) {
  return queryOptions({
    queryKey: templateListKey(department, search),
    queryFn: async (): Promise<ContentPage> =>
      unwrap(
        await api.GET('/api/content', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ ownerDepartment: department, isTemplate: 'true' }),
        }),
      ),
  });
}

/**
 * How many rows were made from one template, asked the cheapest way there is: the list, filtered by
 * `templateId`, for a page of one. What is read is `total`, and the single row comes along because
 * a page of zero is not a thing a list endpoint offers.
 *
 * ⚠️ It is asked on the template's own screen and **not** as a column of the list, and that is a
 * decision rather than an omission: a column would be one request per row, and `DataList` draws one
 * query. Where the number actually matters is in front of somebody about to change a template —
 * "eleven pages were made from this" is the sentence that stops a careless edit.
 */
export function madeFromTemplateQuery(templateId: number) {
  return queryOptions({
    queryKey: madeFromTemplateKey(templateId),
    queryFn: async (): Promise<ContentPage> =>
      unwrap(
        await api.GET('/api/content', {
          params: { query: { page: 1, pageSize: 1 } },
          querySerializer: listQuerySerializer({ templateId: String(templateId) }),
        }),
      ),
  });
}

/** The templates a page may be made from. Owned by the web team, readable by every staff member. */
export function templatesQuery(kind: ContentKind | null = null) {
  return queryOptions({
    queryKey: templatesKey(kind),
    queryFn: async (): Promise<ContentPage> =>
      unwrap(
        await api.GET('/api/content', {
          params: { query: { page: 1, pageSize: 100 } },
          querySerializer: listQuerySerializer({
            isTemplate: 'true',
            ...(kind === null ? {} : { kind }),
          }),
        }),
      ),
  });
}

/**
 * Every page of the site, whichever department wrote it, so that a screen can offer the addresses
 * that exist instead of asking somebody to remember them. A hundred rows: a division site with more
 * pages than that in its menu has a different problem.
 *
 * **Drafts are in it on purpose.** Writing the menu entry before publishing the page is how a menu
 * is actually built, and the entry can wait — switched off — until the page is public. The server
 * accepts the same set, and refuses everything outside it (`MenuItemWriteDtoValidator`).
 *
 * ⚠️ Read with the department narrowing of the engine, like every other list — which here narrows
 * to nothing: the menu belongs to the department that owns the site, so whoever may edit it is a
 * web coordinator or a director, and those reach every department (`ReachesEveryDepartment`). The
 * groups this list is drawn in are therefore real, and a page of another department is offered.
 *
 * ⚠️ **And it takes what is being typed**, since 9 September 2026. A page of this list is a hundred
 * rows, which is the ceiling of the engine — so while the field only suggested, a site with more
 * pages than that was an inconvenience, and since the field decides it was a page nobody could point
 * at. The server searches the title and the slug, which is what the entry shows.
 */
export function menuDestinationPagesQuery(q = '') {
  return queryOptions({
    queryKey: menuDestinationsKey(q),
    queryFn: async (): Promise<ContentPage> =>
      unwrap(
        await api.GET('/api/content', {
          params: { query: { page: 1, pageSize: 100, ...(q === '' ? {} : { q }) } },
          querySerializer: listQuerySerializer({
            kind: 'Page',
            isTemplate: 'false',
          }),
        }),
      ),
  });
}

/**
 * The published documents of a department, for the select that says which one replaced this one
 * (G14). A hundred, like the templates: a shelf longer than that is a shelf with a category, and
 * the successor of a SOP is on the same shelf as the SOP. The server accepts any document there is
 * — the narrowing to one department is this screen's, not a rule.
 */
export function successorsQuery(department: Department) {
  return queryOptions({
    queryKey: successorsKey(department),
    queryFn: async (): Promise<ContentPage> =>
      unwrap(
        await api.GET('/api/content', {
          params: { query: { page: 1, pageSize: 100 } },
          querySerializer: listQuerySerializer({
            ownerDepartment: department,
            kind: 'Document',
            status: 'Published',
          }),
        }),
      ),
  });
}

/**
 * The airports and the centres of the division with their names, for the two fields of a document
 * that choose from a list rather than type (G14). Read from the snapshot the login reads and cached
 * for the session: it moves when the daily synchronisation does, not while a form is open.
 */
export function airspaceQuery() {
  return queryOptions({
    queryKey: airspaceKey,
    queryFn: async (): Promise<AirspaceListingDto> => unwrap(await api.GET('/api/ref/airspace')),
    staleTime: Infinity,
  });
}

export function contentQuery(id: number) {
  return queryOptions({
    queryKey: contentDetailKey(id),
    queryFn: async (): Promise<ContentDetailDto> =>
      unwrap(await api.GET('/api/content/{id}', { params: { path: { id: String(id) } } })),
  });
}

/**
 * What stands between this row and the public, asked before anybody presses publish.
 *
 * The answer comes from the server because the rules are the server's: the same checks publication
 * runs, run without publishing (`ContentPublishService.ProblemsAsync`). A client working them out
 * for itself would be the rules of publication written a second time, and the second copy is the
 * one that goes stale.
 *
 * It is asked about the **stored** row, which is the honest thing: publishing acts on what was
 * saved, not on what is on screen, and the editor already refuses to publish with unsaved changes.
 */
export function publishProblemsQuery(id: number) {
  return queryOptions({
    queryKey: publishProblemsKey(id),
    queryFn: async (): Promise<ContentPublishProblemsDto> =>
      unwrap(await api.GET('/api/content/{id}/publish-problems', { params: { path: { id } } })),
  });
}

/** What a visitor reads: the published version, or nothing at all. */
export function publicContentQuery(kind: ContentKind, slug: string) {
  return queryOptions({
    queryKey: publicContentKey(kind, slug),
    queryFn: async (): Promise<PublicContentDto> =>
      unwrap(
        await api.GET('/api/content/public/{kind}/{slug}', {
          params: { path: { kind, slug } },
        }),
      ),
  });
}
