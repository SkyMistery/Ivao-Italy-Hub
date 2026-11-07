import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { I18nextProvider } from 'react-i18next';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

import { api } from '../../shared/api/client';
import { createTestI18n } from '../../test/harness';
import { bootstrapQuery } from '../me/queries';

import { SITE_SCREENS, useAddressesToLinkTo } from './siteAddresses';

/**
 * Which reads the address field of a block makes: only for a block that has one, and only those the person
 * may make. A refused read is not an empty list somebody can believe in, so it is not made at all: whoever
 * writes the briefing of a tour without `Content.Edit` still has the screens, and writes the rest in full.
 */

const siteRows = [
  {
    id: 7,
    kind: 'Page',
    url: '/about',
    title: { en: 'About' },
    ownerDepartment: 'WD',
    visibility: 'Public',
  },
];

const answerOf = (path: string) =>
  path === '/api/content/addresses' ? siteRows : { items: [], total: 0, page: 1, pageSize: 100 };

const i18n = createTestI18n();

let asked: () => string[] = () => [];

beforeEach(() => {
  const get = vi.fn((path: string) => Promise.resolve({ data: answerOf(path), response: new Response() }));
  vi.spyOn(api, 'GET').mockImplementation(get as never);
  asked = () => get.mock.calls.map(([path]) => path);
});

afterEach(() => {
  vi.restoreAllMocks();
});

function mount(wanted: boolean, permissions: readonly string[]) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  client.setQueryData(bootstrapQuery.queryKey, {
    permissions: permissions.map((name) => ({ name, department: 'ED' })),
    // A fresh answer, so the bootstrap itself is not asked for again.
  } as never);
  client.setQueryDefaults(bootstrapQuery.queryKey, { staleTime: Infinity });

  return renderHook(() => useAddressesToLinkTo(wanted), {
    wrapper: ({ children }: { children: ReactNode }) => (
      <I18nextProvider i18n={i18n}>
        <QueryClientProvider client={client}>{children}</QueryClientProvider>
      </I18nextProvider>
    ),
  });
}

test('a block that leads somewhere offers the pages of the site, the screens and the links', async () => {
  const { result } = mount(true, ['Content.Edit', 'Links.View']);

  await waitFor(() => expect(result.current.map((address) => address.path)).toContain('/about'));
  expect(result.current.map((address) => address.path)).toEqual(['/about', ...SITE_SCREENS]);
  expect(asked().sort()).toEqual(['/api/content/addresses', '/api/links']);
});

test('a block that leads nowhere asks nothing', async () => {
  mount(false, ['Content.Edit', 'Links.View']);

  await new Promise((resolve) => setTimeout(resolve, 20));
  expect(asked()).toEqual([]);
});

test('a read the person may not make is not made, and the screens are still offered', async () => {
  const { result } = mount(true, ['Tours.Edit']);

  await new Promise((resolve) => setTimeout(resolve, 20));
  expect(asked()).toEqual([]);
  expect(result.current.map((address) => address.path)).toEqual([...SITE_SCREENS]);
});

test('the list stays the same array until an answer changes it, so a schema is not made again at every drawing', async () => {
  const { result, rerender } = mount(true, ['Content.Edit']);

  await waitFor(() => expect(result.current.map((address) => address.path)).toContain('/about'));
  const before = result.current;
  rerender();

  expect(result.current).toBe(before);
});
