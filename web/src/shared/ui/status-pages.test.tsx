import { screen } from '@testing-library/react';
import { RouterProvider, createMemoryHistory, createRootRoute, createRouter } from '@tanstack/react-router';
import userEvent from '@testing-library/user-event';
import { expect, test } from 'vitest';

import { ApiError } from '../api/problem';
import { renderWithProviders } from '../../test/harness';

import { RouteError } from './status-pages';

/**
 * What a route draws when it throws (issue #332, item 5): the router's default, as `createHubRouter` sets it. A route
 * whose loader fails is the case, on a real router in memory.
 */
function renderFailing(failures: readonly Error[]) {
  let asked = 0;
  const router = createRouter({
    routeTree: createRootRoute({
      loader: () => {
        const failure = failures[asked];
        asked += 1;
        if (failure !== undefined) {
          throw failure;
        }
        return 'read';
      },
      component: () => <p>The page</p>,
    }),
    history: createMemoryHistory({ initialEntries: ['/'] }),
    defaultErrorComponent: RouteError,
  });
  renderWithProviders(<RouterProvider router={router} />);
}

test('a 404 is "not found", as the guards say it', async () => {
  renderFailing([new ApiError(404, undefined)]);
  expect(await screen.findByRole('heading', { name: 'This page does not exist' })).toBeInTheDocument();
});

test('a 403 is "not for you"', async () => {
  renderFailing([new ApiError(403, undefined)]);
  expect(await screen.findByRole('heading', { name: 'This is not for you' })).toBeInTheDocument();
});

test("a failure of the server is said in the reader's language, never as the raw message, and trying again reads the route again", async () => {
  renderFailing([new ApiError(500, undefined)]);

  expect(await screen.findByRole('heading', { name: 'This page could not be loaded' })).toBeInTheDocument();
  expect(screen.queryByText(/The API answered/)).toBeNull();

  await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
  expect(await screen.findByText('The page')).toBeInTheDocument();
});
