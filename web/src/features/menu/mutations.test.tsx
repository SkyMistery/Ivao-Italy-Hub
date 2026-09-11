import { QueryClient, QueryClientProvider, QueryObserver } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { expect, test, vi } from 'vitest';

import { menuDetailKey, menuKey } from './queries';

/**
 * Deleting a row tells the caller it worked, even while the cache is still busy.
 *
 * ⚠️ It did not, and the shape of the defect is worth keeping. The delete mutation **awaited**
 * `invalidateQueries`, which waits for every *active* query under that key to refetch — including
 * the detail query of the screen doing the deleting, whose row has just stopped existing. That
 * refetch 404s and retries, so `onSuccess` never settled, and the callbacks passed to `mutate`
 * never ran. On screen: the row vanished from the database, the page stayed open, and nothing said
 * anything.
 *
 * It only became reachable when the detail screens started reading the query instead of the route
 * loader (`decisions/2026-09-07-il-loader-non-e-la-riga.md`): before that the detail query had no
 * observer, so invalidating it refetched nothing. One correction uncovered the next.
 *
 * The test puts a query that never settles in the way, which is what a retrying 404 is.
 */

const api = vi.hoisted(() => ({ delete: vi.fn() }));

vi.mock('../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../shared/api/client')),
  api: { DELETE: api.delete },
}));

const { useDeleteMenuItem } = await import('./mutations');

test('the caller is told the delete worked without waiting for the cache', async () => {
  api.delete.mockResolvedValue({ data: undefined, response: new Response(null, { status: 204 }) });

  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  // The screen doing the deleting, watching the row it is about to delete. A promise that never
  // settles is what the refetch of a deleted row looks like while it is still retrying.
  const watching = new QueryObserver(client, {
    queryKey: menuDetailKey(7),
    queryFn: () => new Promise(() => {}),
  });

  const unsubscribe = watching.subscribe(() => {});

  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );

  try {
    const { result } = renderHook(() => useDeleteMenuItem(), { wrapper });

    const told = vi.fn();
    result.current.mutate(7, { onSuccess: told });

    // A second, not a minute: what is asserted is that the caller does not wait for the cache.
    await waitFor(() => expect(told).toHaveBeenCalled(), { timeout: 1_000 });

    // And the deletion really was asked for, rather than the callback firing on nothing.
    expect(api.delete).toHaveBeenCalledWith('/api/menu/{id}', { params: { path: { id: '7' } } });

    // The invalidation still happened; it just is not what the screen waits for.
    expect(client.getQueryState(menuDetailKey(7))?.isInvalidated).toBe(true);
    expect(menuKey).toBeDefined();
  } finally {
    unsubscribe();
    client.clear();
  }
});
