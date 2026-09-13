import { useMutation, useQueryClient } from '@tanstack/react-query';

import { emptyBody, type Body } from '../../blocks';
import type { Department, LocalizedString } from '../../shared/api/bootstrap';
import { AUTOSAVE_HEADER, api, unwrap, unwrapEmpty } from '../../shared/api/client';
import { NEW_ROW_VERSION } from '../../shared/api/rowVersion';
import { emptyLocalized } from '../../shared/i18n/localized';

import {
  contentDetailKey,
  contentKey,
  type ContentDetailDto,
  type ContentKind,
  type ContentWriteDto,
} from './queries';
import type { ContentFormValues } from './schema';

/**
 * Writing a content row. The metadata come from the form and the body from the section tree; this
 * is the one place they are put back together into the payload the API takes, which is why no
 * screen builds a `ContentWriteDto` of its own (design M0 §7.5).
 */

/** A translated field with nothing written in it is absent, not an object full of empty strings. */
function trimLocalized(value: Record<string, string>): LocalizedString | null {
  const written = Object.entries(value).filter(([, text]) => text.trim().length > 0);
  return written.length === 0 ? null : Object.fromEntries(written);
}

/**
 * A language of a translated object with nothing written in it is absent, exactly as an empty
 * translated string is: an object full of empty strings would be a page claiming a description it
 * has not got.
 */
function trimLocalizedObject(value: ContentFormValues['seo']): ContentWriteDto['seo'] {
  const written = Object.entries(value ?? {}).filter(([, entry]) =>
    Object.values(entry ?? {}).some(
      (field) => field !== undefined && field !== null && String(field).trim() !== '',
    ),
  );

  return written.length === 0 ? null : Object.fromEntries(written);
}

export function toWriteDto(values: ContentFormValues, body: Body): ContentWriteDto {
  return {
    kind: values.kind,
    slug: values.slug.trim(),
    ownerDepartment: values.ownerDepartment,
    visibility: values.visibility,
    isTemplate: values.isTemplate,
    // The title is sent as it stands, so that publication can name the language that is missing
    // rather than being handed a field that quietly became null.
    title: values.title,
    summary: trimLocalized(values.summary),
    seo: trimLocalizedObject(values.seo),
    body,
    schemaVersion: body.schemaVersion,
    // The five that belong to one kind each. A kind whose form does not draw them sends what the
    // column already holds for a row that has none: no shelf, no picture, unpinned, first, no file.
    // They are columns of `cms_contents` and not a table, which is the whole point of design M1 §3.
    // Text on the form, a number on the row; empty is the top of the site.
    parentId: values.parentId === undefined || values.parentId === '' ? null : Number(values.parentId),
    category: values.category === undefined || values.category === '' ? null : values.category,
    coverMediaId: values.coverMediaId ?? null,
    pinned: values.pinned ?? false,
    sort: values.sort ?? 0,
    fileMediaId: values.fileMediaId ?? null,
    // The life of a document (G14). The same rule: a kind whose form does not draw them sends
    // nothing, and the server refuses them on anything but a document anyway.
    effectiveOn: blankToNull(values.effectiveOn),
    reviewOn: blankToNull(values.reviewOn),
    retiredAt: blankToNull(values.retiredAt),
    // Text on the form, a number on the row: the same conversion the parent of a menu entry makes.
    supersededById:
      values.supersededById === undefined || values.supersededById === ''
        ? null
        : Number(values.supersededById),
    showFooter: values.showFooter ?? true,
    rowVersion: values.rowVersion,
  };
}

/** A text field left empty is a column left null, not an empty string the server has to refuse. */
function blankToNull(value: string | undefined): string | null {
  return value === undefined || value.trim() === '' ? null : value.trim();
}

/**
 * The form as a new row starts it: empty, in the department of the route.
 *
 * `isTemplate` is an argument and not a field, for the same reason it is hidden on the form: a page
 * may not promote itself into a template. What decides it is **which screen you are on** — the
 * templates screen of a department makes templates, every other screen makes rows — and that screen
 * is behind `Content.ManageTemplates`.
 */
export function emptyContent(
  department: Department,
  locales: readonly string[],
  kind: ContentKind = 'Page',
  isTemplate = false,
): ContentFormValues {
  return {
    kind,
    slug: '',
    ownerDepartment: department,
    // A page is drafted where only the staff can see it; making it public is a choice, and one
    // that only takes effect when somebody publishes.
    visibility: 'Staff',
    isTemplate,
    parentId: '',
    title: emptyLocalized(locales),
    summary: emptyLocalized(locales),
    seo: emptySeo(locales),
    category: '',
    pinned: false,
    sort: 0,
    // On by default: a document that says nothing about its footer has one (G14).
    showFooter: true,
    rowVersion: NEW_ROW_VERSION,
  };
}

/** The form as an existing row fills it, with every language of the division present as a tab. */
export function toFormValues(content: ContentDetailDto, locales: readonly string[]): ContentFormValues {
  const spread = (value: LocalizedString | null): Record<string, string> =>
    Object.fromEntries(locales.map((locale) => [locale, value?.[locale] ?? '']));

  return {
    kind: content.kind,
    slug: content.slug,
    ownerDepartment: content.ownerDepartment,
    visibility: content.visibility,
    isTemplate: content.isTemplate,
    parentId: content.parentId === null ? '' : String(content.parentId),
    title: spread(content.title),
    summary: spread(content.summary),
    seo: spreadSeo(content.seo, locales),
    // An absent shelf is the empty string and not `undefined`: a select that starts at `undefined`
    // is an uncontrolled field that React complains about the moment somebody chooses one.
    category: content.category ?? '',
    ...(content.coverMediaId === null ? {} : { coverMediaId: content.coverMediaId }),
    pinned: content.pinned,
    sort: content.sort,
    ...(content.fileMediaId === null ? {} : { fileMediaId: content.fileMediaId }),
    // The life of a document (G14), carried whether or not this kind's form draws them: a save
    // from the editor must never wipe what the row already says about itself.
    ...(content.effectiveOn === null ? {} : { effectiveOn: content.effectiveOn }),
    ...(content.reviewOn === null ? {} : { reviewOn: content.reviewOn }),
    ...(content.retiredAt === null ? {} : { retiredAt: content.retiredAt }),
    ...(content.supersededById === null ? {} : { supersededById: String(content.supersededById) }),
    showFooter: content.showFooter,
    rowVersion: content.rowVersion,
  };
}

/** One empty entry per language, so every tab of the translated object has something to fill in. */
function emptySeo(locales: readonly string[]): ContentFormValues['seo'] {
  return Object.fromEntries(locales.map((locale) => [locale, { title: '', description: '' }]));
}

/**
 * What the server stored, spread over the languages of the division. The column is a
 * `Localized<JsonNode>` — opaque to the backend by design — so the shape is read here, where the
 * schema that draws it also lives.
 */
function spreadSeo(
  value: Record<string, unknown> | null | undefined,
  locales: readonly string[],
): ContentFormValues['seo'] {
  return Object.fromEntries(
    locales.map((locale) => {
      const entry = (value?.[locale] ?? {}) as Record<string, unknown>;

      return [
        locale,
        {
          title: typeof entry.title === 'string' ? entry.title : '',
          description: typeof entry.description === 'string' ? entry.description : '',
          ...(typeof entry.ogImageMediaId === 'number' ? { ogImageMediaId: entry.ogImageMediaId } : {}),
        },
      ];
    }),
  );
}

interface ContentWrite {
  values: ContentFormValues;
  body: Body;
  /** A save the editor made by itself, after a pause: audited without the body (`AUTOSAVE_HEADER`). */
  autosave?: boolean;
}

export function useCreateContent() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (write: ContentWrite): Promise<ContentDetailDto> =>
      unwrap(await api.POST('/api/content', { body: toWriteDto(write.values, write.body) })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: contentKey });
    },
  });
}

export function useUpdateContent(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (write: ContentWrite): Promise<ContentDetailDto> =>
      unwrap(
        await api.PUT('/api/content/{id}', {
          params: { path: { id: String(id) } },
          body: toWriteDto(write.values, write.body),
          ...(write.autosave === true ? { headers: { [AUTOSAVE_HEADER]: '1' } } : {}),
        }),
      ),
    onSuccess: async (content) => {
      queryClient.setQueryData(contentDetailKey(id), content);
      await queryClient.invalidateQueries({ queryKey: contentKey });
    },
  });
}

export function useDeleteContent() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/content/{id}', { params: { path: { id: String(id) } } })),
    // ⚠️ Deliberately not awaited, and deliberately not `async`. What has just been deleted is the
    // row a screen is **looking at**, so invalidating waits for that screen's own query to refetch
    // — a row that no longer exists. The refetch 404s and retries, `onSuccess` never settles, and
    // the callbacks a caller passed to `mutate` never run: the screen deletes the row and then sits
    // there saying nothing. Found in G12, and caused by making the screens read the query rather
    // than the loader (`decisions/2026-09-07-il-loader-non-e-la-riga.md`), which is what gave that
    // query an observer in the first place.
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: contentKey });
    },
  });
}

/**
 * What publication is told beside which row: a line for the staff about what changed, optional,
 * asked in the dialog the Publish button opens.
 */
export interface PublishRequest {
  changelog: string;
}

/**
 * Publishing. A refusal reaches the caller as an `ApiError` like any other, so the dialog shows
 * the missing languages per path through the very same `useProblemDetails` a form uses.
 */
export function usePublishContent(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (request: PublishRequest): Promise<ContentDetailDto> =>
      unwrap(
        await api.POST('/api/content/{id}/publish', {
          // A number, not a string: the route constrains it to a long, so the contract says
          // integer -- unlike `/api/content/{id}`, which the CRUD engine addresses as text.
          params: { path: { id } },
          body: { changelog: blankToNull(request.changelog) },
        }),
      ),
    onSuccess: async (content) => {
      queryClient.setQueryData(contentDetailKey(id), content);
      await queryClient.invalidateQueries({ queryKey: contentKey });
    },
  });
}

/** A page made from a template: the server does the deep copy, identifiers and all. */
export function useCreateFromTemplate() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (request: {
      templateId: number;
      ownerDepartment: Department;
      slug: string;
      /** The page it sits under; null at the top of the site. */
      parentId: number | null;
    }): Promise<ContentDetailDto> =>
      unwrap(
        await api.POST('/api/content/from-template/{templateId}', {
          params: { path: { templateId: request.templateId } },
          body: { ownerDepartment: request.ownerDepartment, slug: request.slug, parentId: request.parentId },
        }),
      ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: contentKey });
    },
  });
}

/** The body of a row that has none yet. */
export { emptyBody };
