import { useMutation, useQueryClient } from '@tanstack/react-query';

import type { Department } from '../../shared/api/bootstrap';
import { api, unwrap, unwrapEmpty } from '../../shared/api/client';

import {
  categoryDetailKey,
  categoryKey,
  type CategoryDetailDto,
  type CategoryWriteDto,
} from './queries';
import type { CategoryFormValues } from './schema';

/**
 * Writing a category. An ordinary payload, an ordinary CRUD resource: what the vocabulary costs
 * this hub is this file and its four siblings, which is what design M1 §3.4 asks it to cost.
 */

export function toWriteDto(values: CategoryFormValues): CategoryWriteDto {
  return {
    kind: values.kind,
    ownerDepartment: values.ownerDepartment,
    key: values.key.trim(),
    // Sent as it stands, so the server can name the language that is missing rather than being
    // handed a field that quietly became null.
    label: values.label,
    sort: values.sort,
    isActive: values.isActive,
    rowVersion: values.rowVersion,
  };
}

/** The form as a new shelf starts it: in the department of the route, active, last in order. */
export function emptyCategory(
  department: Department,
  locales: readonly string[],
): CategoryFormValues {
  return {
    ownerDepartment: department,
    kind: 'News',
    key: '',
    label: Object.fromEntries(locales.map((locale) => [locale, ''])),
    sort: 0,
    isActive: true,
    rowVersion: '',
  };
}

/** The form as an existing shelf fills it, with every language of the division present as a tab. */
export function toFormValues(
  category: CategoryDetailDto,
  locales: readonly string[],
): CategoryFormValues {
  return {
    ownerDepartment: category.ownerDepartment,
    // A page has no shelves, so the form offers the two kinds that do; a row that somehow says
    // otherwise is shown as news rather than putting the select in a state it cannot draw.
    kind: category.kind === 'Document' ? 'Document' : 'News',
    key: category.key,
    label: Object.fromEntries(locales.map((locale) => [locale, category.label?.[locale] ?? ''])),
    sort: category.sort,
    isActive: category.isActive,
    rowVersion: category.rowVersion,
  };
}

export function useCreateCategory() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: CategoryFormValues): Promise<CategoryDetailDto> =>
      unwrap(await api.POST('/api/categories', { body: toWriteDto(values) })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: categoryKey });
    },
  });
}

export function useUpdateCategory(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: CategoryFormValues): Promise<CategoryDetailDto> =>
      unwrap(
        await api.PUT('/api/categories/{id}', {
          params: { path: { id: String(id) } },
          body: toWriteDto(values),
        }),
      ),
    onSuccess: async (category) => {
      queryClient.setQueryData(categoryDetailKey(id), category);
      await queryClient.invalidateQueries({ queryKey: categoryKey });
    },
  });
}

/**
 * Deleting a shelf. ⚠️ The rows filed under it keep their key: there is no foreign key, on purpose,
 * so nothing cascades and nothing is rewritten — the list simply shows the key as it is (design
 * M1 §3.4). Retiring a category with `isActive` is the gentler half of the same gesture.
 */
export function useDeleteCategory() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/categories/{id}', { params: { path: { id: String(id) } } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: categoryKey });
    },
  });
}
