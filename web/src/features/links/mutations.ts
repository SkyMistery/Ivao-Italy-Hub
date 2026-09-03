import { useMutation, useQueryClient } from '@tanstack/react-query';

import type { Department, LocalizedString } from '../../shared/api/bootstrap';
import { api, unwrap, unwrapEmpty } from '../../shared/api/client';

import { linkKey, linksKey, type LinkDetailDto, type LinkWriteDto } from './queries';
import type { LinkFormValues } from './schema';

/**
 * Writing a link. Three mutations, and one place that turns what the form holds into what the API
 * expects — a text box has an empty string where the contract has a `null`, and that translation
 * belongs here rather than in a screen (design M0 §7.5).
 */

/** A translated field with nothing written in it is absent, not an object full of empty strings. */
function trimLocalized(value: Record<string, string>): LocalizedString | null {
  const written = Object.entries(value).filter(([, text]) => text.trim().length > 0);
  return written.length === 0 ? null : Object.fromEntries(written);
}

export function toWriteDto(values: LinkFormValues): LinkWriteDto {
  return {
    ownerDepartment: values.ownerDepartment,
    visibility: values.visibility,
    // The title is required in every language: it is sent as it stands, so the server can say
    // which language is missing rather than being handed a field that quietly became null.
    title: values.title,
    url: values.url.trim(),
    description: trimLocalized(values.description),
    category: values.category.trim() === '' ? null : values.category.trim(),
    sort: values.sort,
    isActive: values.isActive,
    rowVersion: values.rowVersion,
  };
}

/** The form as a new link starts it: empty, in the department of the route. */
export function emptyLink(department: Department, locales: readonly string[]): LinkFormValues {
  const blank = Object.fromEntries(locales.map((locale) => [locale, '']));

  return {
    ownerDepartment: department,
    visibility: 'Public',
    title: blank,
    url: '',
    description: { ...blank },
    category: '',
    sort: 0,
    isActive: true,
    // No version yet: the server reads an empty one as "the row as it is now", which for a create
    // is the only thing it can mean.
    rowVersion: '',
  };
}

/** The form as an existing link fills it, with every language of the division present as a tab. */
export function toFormValues(link: LinkDetailDto, locales: readonly string[]): LinkFormValues {
  const spread = (value: LocalizedString | null): Record<string, string> =>
    Object.fromEntries(locales.map((locale) => [locale, value?.[locale] ?? '']));

  return {
    ownerDepartment: link.ownerDepartment,
    visibility: link.visibility,
    title: spread(link.title),
    url: link.url,
    description: spread(link.description),
    category: link.category ?? '',
    sort: link.sort,
    isActive: link.isActive,
    rowVersion: link.rowVersion,
  };
}

export function useCreateLink() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: LinkFormValues): Promise<LinkDetailDto> =>
      unwrap(await api.POST('/api/links', { body: toWriteDto(values) })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: linksKey });
    },
  });
}

export function useUpdateLink(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: LinkFormValues): Promise<LinkDetailDto> =>
      unwrap(
        await api.PUT('/api/links/{id}', {
          params: { path: { id: String(id) } },
          body: toWriteDto(values),
        }),
      ),
    onSuccess: async (link) => {
      queryClient.setQueryData(linkKey(id), link);
      await queryClient.invalidateQueries({ queryKey: linksKey });
    },
  });
}

export function useDeleteLink() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/links/{id}', { params: { path: { id: String(id) } } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: linksKey });
    },
  });
}
