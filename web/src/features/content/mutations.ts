import { useMutation, useQueryClient } from '@tanstack/react-query';

import { emptyBody, type Body } from '../../blocks';
import type { Department, LocalizedString } from '../../shared/api/bootstrap';
import { api, unwrap, unwrapEmpty } from '../../shared/api/client';
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

export function toWriteDto(values: ContentFormValues, body: Body, seo: unknown): ContentWriteDto {
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
    // Sent back exactly as it was loaded: no screen edits it yet, and dropping it on every save
    // would be a way of losing it (see `schema.ts`).
    seo: seo as ContentWriteDto['seo'],
    body,
    schemaVersion: body.schemaVersion,
    rowVersion: values.rowVersion,
  };
}

/** The form as a new page starts it: empty, in the department of the route. */
export function emptyContent(
  department: Department,
  locales: readonly string[],
  kind: ContentKind = 'Page',
): ContentFormValues {
  return {
    kind,
    slug: '',
    ownerDepartment: department,
    // A page is drafted where only the staff can see it; making it public is a choice, and one
    // that only takes effect when somebody publishes.
    visibility: 'Staff',
    isTemplate: false,
    title: emptyLocalized(locales),
    summary: emptyLocalized(locales),
    rowVersion: '',
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
    title: spread(content.title),
    summary: spread(content.summary),
    rowVersion: content.rowVersion,
  };
}

interface ContentWrite {
  values: ContentFormValues;
  body: Body;
  seo: unknown;
}

export function useCreateContent() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (write: ContentWrite): Promise<ContentDetailDto> =>
      unwrap(await api.POST('/api/content', { body: toWriteDto(write.values, write.body, write.seo) })),
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
          body: toWriteDto(write.values, write.body, write.seo),
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
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: contentKey });
    },
  });
}

/**
 * Publishing. A refusal reaches the caller as an `ApiError` like any other, so the dialog shows
 * the missing languages per path through the very same `useProblemDetails` a form uses.
 */
export function usePublishContent(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (changelog: string | null): Promise<ContentDetailDto> =>
      unwrap(
        await api.POST('/api/content/{id}/publish', {
          // A number, not a string: the route constrains it to a long, so the contract says
          // integer -- unlike `/api/content/{id}`, which the CRUD engine addresses as text.
          params: { path: { id } },
          body: { changelog },
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
    }): Promise<ContentDetailDto> =>
      unwrap(
        await api.POST('/api/content/from-template/{templateId}', {
          params: { path: { templateId: request.templateId } },
          body: { ownerDepartment: request.ownerDepartment, slug: request.slug },
        }),
      ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: contentKey });
    },
  });
}

/** The body of a row that has none yet. */
export { emptyBody };
