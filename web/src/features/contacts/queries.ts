import { queryOptions } from '@tanstack/react-query';

import type { Department } from '../../shared/api/bootstrap';
import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import { listQuerySerializer, toQuery, type ListSearch } from '../../shared/list';

/**
 * Every call the contact queue makes, as query options. A component never fetches: it asks for
 * these and React Query decides whether that means a round trip (design M0 §7.4).
 */

export type ContactListDto = components['schemas']['ContactListDto'];
export type ContactDetailDto = components['schemas']['ContactDetailDto'];
export type ContactStatus = components['schemas']['ContactStatus'];
export type ContactPage = components['schemas']['PagedResultOfContactListDto'];
export type ContactThreadDto = components['schemas']['ContactThreadDto'];

/** The four the server declares, in the order a message moves through them. */
export const CONTACT_STATUSES = [
  'New',
  'Read',
  'Answered',
  'Closed',
] as const satisfies readonly ContactStatus[];

export const contactsKey = ['contacts'] as const;

export function contactsListKey(department: Department, search: ListSearch) {
  return [...contactsKey, 'list', department, search] as const;
}

export function contactKey(id: number) {
  return [...contactsKey, 'detail', id] as const;
}

export function contactThreadKey(id: number) {
  return [...contactsKey, 'thread', id] as const;
}

export function myContactsListKey(search: ListSearch) {
  return [...contactsKey, 'mine', search] as const;
}

/**
 * One page of the queue of a department. The department is a filter and not a path segment,
 * because the resource is `/api/contacts` — one CRUD engine, one route — and the back office
 * narrows it (`CrudOptions.Filterable`).
 */
export function contactsListQuery(department: Department, search: ListSearch) {
  return queryOptions({
    queryKey: contactsListKey(department, search),
    queryFn: async (): Promise<ContactPage> =>
      unwrap(
        await api.GET('/api/contacts', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ ownerDepartment: department }),
        }),
      ),
  });
}

export function contactQuery(id: number) {
  return queryOptions({
    queryKey: contactKey(id),
    queryFn: async (): Promise<ContactDetailDto> =>
      unwrap(await api.GET('/api/contacts/{id}', { params: { path: { id: String(id) } } })),
  });
}

/**
 * The conversation of one message, as the reader may read it: the same address for the back office and for
 * `/me/contacts` (M2, T14a). The server hides who answered from the member who wrote.
 */
export function contactThreadQuery(id: number) {
  return queryOptions({
    queryKey: contactThreadKey(id),
    queryFn: async (): Promise<ContactThreadDto> =>
      unwrap(await api.GET('/api/contacts/{id}/thread', { params: { path: { id } } })),
  });
}

/** One page of the member's own threads: the ones they wrote and the ones they were added to. */
export function myContactsListQuery(search: ListSearch) {
  return queryOptions({
    queryKey: myContactsListKey(search),
    queryFn: async (): Promise<ContactPage> =>
      unwrap(
        await api.GET('/api/me/contacts', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({}),
        }),
      ),
  });
}
