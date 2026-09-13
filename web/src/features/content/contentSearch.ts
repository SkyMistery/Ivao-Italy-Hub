import { z } from 'zod';

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

export const contentSearchSchema = departmentListSearchSchema.extend({
  kind: z.enum(CONTENT_SCREEN_KINDS).default('Page'),
});

export type ContentSearch = z.output<typeof contentSearchSchema>;

/** The kinds a template, or a row, can be. Not `Dashboard`: a department has one home, seeded. */
export const ROW_KINDS = ['Page', 'News', 'Document'] as const;

export type RowKind = (typeof ROW_KINDS)[number];

/**
 * What the editor of `/staff/content/$id` is told about a row that does not exist yet: what it
 * will be, and where. An existing row says both about itself.
 */
export const contentEditorSearchSchema = z.object({
  kind: z.enum(ROW_KINDS).default('Page'),
  template: z.boolean().default(false),
  department: z.enum(DEPARTMENTS).optional(),
});

export type ContentEditorSearch = z.output<typeof contentEditorSearchSchema>;
