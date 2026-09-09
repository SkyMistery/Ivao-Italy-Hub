import { col, type ColumnSpec } from '../../shared/list';

import type { ContentKind, ContentListDto } from './queries';

/**
 * What tells a page, a news item and a document apart in the back office — and it is deliberately
 * this short. They are one entity, one editor, one renderer, one publication and one projection;
 * what differs is a fixed `kind` on the list and which columns it shows (design M1 §3.2).
 *
 * ⚠️ The **field** labels stay in the `content` namespace for all three, and that is not laziness:
 * "Title", "Address" and "Visible to" mean the same thing whichever kind is being edited, and three
 * copies of them would be three places to keep in step. What is per kind is the *screen*: its
 * heading, its description and the wording of its "new" button, which is the `titles` namespace
 * below.
 *
 * If this file ever grows a second editor, a renderer or a column the entity has not got, plan
 * §9.3 has not held and the closing report has to say so.
 */
export interface ContentKindConfig {
  kind: ContentKind;
  /** Where the screen's own words live. The field labels are always `content`. */
  titles: string;
  columns: readonly ColumnSpec<ContentListDto>[];
}

/** Common to all three, in this order: what a row is called, where it lives, and how it is doing. */
const identity: readonly ColumnSpec<ContentListDto>[] = [
  col.localized('title'),
  col.text('slug', { sortable: true }),
];

const state: readonly ColumnSpec<ContentListDto>[] = [
  col.badge('status', 'content', { sortable: true }),
  col.badge('visibility', 'content'),
  col.date('publishedAt', { sortable: true }),
  col.date('updatedAt', { sortable: true }),
];

export const CONTENT_KINDS: Record<ContentKind, ContentKindConfig> = {
  Page: {
    kind: 'Page',
    titles: 'content',
    columns: [...identity, ...state],
  },
  News: {
    kind: 'News',
    titles: 'news',
    // Category, cover and pin: the three facts that decide where a news item lands in a list.
    columns: [
      ...identity,
      col.text('category', { sortable: true }),
      col.media('coverMediaId'),
      col.boolean('pinned'),
      ...state,
    ],
  },
  // The home of a department, which is a content row like the other three and is edited in the same
  // editor. It has no list screen and needs none: a department has exactly one, and it is reached
  // from the department's own page rather than from a list of one row (design M1 §14).
  Dashboard: {
    kind: 'Dashboard',
    titles: 'dashboard',
    columns: [...identity, ...state],
  },
  Document: {
    kind: 'Document',
    titles: 'documents',
    // Category, order and file: the three facts that decide where a document lands on a shelf and
    // whether it is read or downloaded (design M1 §3.2).
    //
    // The file is a **link** and not a thumbnail, unlike the cover of a news item: a document's file
    // is as often a PDF as a picture, and a list row does not carry the type of what it points at.
    columns: [
      ...identity,
      col.text('category', { sortable: true }),
      col.number('sort'),
      col.file('fileMediaId'),
      ...state,
    ],
  },
};

/**
 * The templates of a department, all kinds together. It is not a `ContentKindConfig`, because a
 * template is not a fifth kind: it is the same four seen from the other side of `is_template`, and
 * the column that says which one it is exists here and on no other list — everywhere else the kind
 * is fixed by the screen and printing it would be printing the title of the page in every row.
 */
export const TEMPLATE_COLUMNS: readonly ColumnSpec<ContentListDto>[] = [
  col.localized('title'),
  col.text('slug', { sortable: true }),
  col.badge('kind', 'content', { sortable: true }),
  col.date('updatedAt', { sortable: true }),
];
