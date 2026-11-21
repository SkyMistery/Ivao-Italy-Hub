import { z } from 'zod';

import type { Department } from '../../shared/api/bootstrap';
import { DEPARTMENTS } from '../../shared/api/department';
import { departmentListSearchSchema } from '../../shared/list';

/**
 * What `/staff/content` is asked for in its address: the paging, sorting and searching every list
 * has, plus **which kind** and — optionally — **which department** (note
 * 2026-09-13-contenuti-centralizzati, 3.1). One screen for the pages, the news, the documents and
 * the templates of every department a member reaches; the entries of a department in the sidebar
 * are this same screen with the department already chosen.
 *
 * `Template` is not a `kind` of the row: it is the other side of `is_template`, all kinds together,
 * which is how the templates screen has always listed them. It is a kind of the *screen*.
 */
export const CONTENT_SCREEN_KINDS = ['Page', 'News', 'Document', 'Template'] as const;

export type ContentScreenKind = (typeof CONTENT_SCREEN_KINDS)[number];

/** The three states of a row; `Ready` is a page waiting for approval (G19). */
export const CONTENT_STATUSES = ['Draft', 'Ready', 'Published'] as const;

/**
 * How the pages are shown (design `13-design-struttura-del-sito.md` §3.2): as the tree of the site, the default, or as the
 * list that pages, sorts and filters. Only the pages have a tree: news and documents live under `/news` and `/documents`.
 */
export const CONTENT_VIEWS = ['tree', 'list'] as const;

export type ContentView = (typeof CONTENT_VIEWS)[number];

export const contentSearchSchema = departmentListSearchSchema.extend({
  // Read as the default when the address carries something else, as the parameters of every list are (issue #332, item 5).
  kind: z.enum(CONTENT_SCREEN_KINDS).default('Page').catch('Page'),
  // Optional and not defaulted: a default would be written into every address of the list, news and documents included.
  view: z.enum(CONTENT_VIEWS).optional().catch(undefined),
  // Optional and not defaulted, like the department: every state unless one is asked for.
  status: z.enum(CONTENT_STATUSES).optional().catch(undefined),
});

export type ContentSearch = z.output<typeof contentSearchSchema>;

/**
 * The view of the pages the address asks for, or the one it gets by default: the tree, unless a department is chosen —
 * the entry of a department in the sidebar opens its own list (design 13 §3.2).
 */
export function defaultView(search: Pick<ContentSearch, 'view' | 'department'>): ContentView {
  return search.view ?? (search.department === undefined ? 'tree' : 'list');
}

/** The kinds a template, or a row, can be. Not `Dashboard`: a department has one home, seeded. */
export const ROW_KINDS = ['Page', 'News', 'Document'] as const;

export type RowKind = (typeof ROW_KINDS)[number];

/**
 * What the editor of `/staff/content/$id` is told about a row that does not exist yet: what it
 * will be, and where. An existing row says both about itself.
 */
export const contentEditorSearchSchema = z.object({
  // All three optional and none defaulted: a default is written back into the address, and the
  // address of a row that exists would then read `/staff/content/20?kind=Page&template=false` —
  // three facts the row states about itself, repeated in a URL that could contradict them. The
  // reader of the search supplies the defaults (found by the round in CI, 13 September 2026).
  kind: z.enum(ROW_KINDS).optional(),
  template: z.boolean().optional(),
  department: z.enum(DEPARTMENTS).optional(),
  // The page a new page starts under, from «New page here» on the tree of the site (design 13 §3.2). A suggestion the
  // form starts from, not a rule: the server checks the place like any other, and a number that is no page is refused.
  parent: z.coerce.number().int().positive().optional().catch(undefined),
  // Where the form was opened from: `tree` when from the tree of the site, so that leaving it goes back there and not to a
  // department's list (review of #550, item 1). Absent for every other way in, which goes back to the list.
  view: z.enum(CONTENT_VIEWS).optional().catch(undefined),
});

export type ContentEditorSearch = z.output<typeof contentEditorSearchSchema>;

/** What the address of `/staff/content` is given to go back to, from the form of a row. */
export type ContentReturnSearch =
  { kind: 'Page'; view: 'tree' } | { kind: ContentScreenKind; department: Department };

/**
 * Where the form of a row goes back to — by its breadcrumb and when it is finished: the tree of the site when it was opened
 * from the tree, which is the whole site and so names no department; otherwise the list of its kind in its department.
 */
export function contentReturnSearch(
  kind: ContentScreenKind,
  department: Department,
  openedFrom: ContentView | undefined,
): ContentReturnSearch {
  return kind === 'Page' && openedFrom === 'tree' ? { kind: 'Page', view: 'tree' } : { kind, department };
}

/** The same place as an address, for the breadcrumb. */
export function contentReturnHref(search: ContentReturnSearch): string {
  return `/staff/content?${new URLSearchParams(search).toString()}`;
}

/** The language the draft is looked at in, in a tab of its own: the one the editor was showing (design of phase 3, §4.7). */
export const draftPreviewSearchSchema = z.object({
  lang: z.string().optional(),
});
