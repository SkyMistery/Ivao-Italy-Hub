import { QueryClient } from '@tanstack/react-query';
import { RouterProvider } from '@tanstack/react-router';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import { HubProviders } from './app/Providers';
import { createI18n } from './app/i18n';
import { registry } from './app/registry';
import { createHubRouter } from './app/router';
import { bootstrapKey } from './features/me/queries';
import { setUnauthorizedHandler } from './shared/api/client';
import './styles/index.css';

const queryClient = new QueryClient();

// A 401 means the session is gone: the cached bootstrap is stale and the shell must redraw as
// anonymous rather than keep showing a name.
setUnauthorizedHandler(() => {
  void queryClient.invalidateQueries({ queryKey: bootstrapKey });
});

// The router carries the query client, and the root route puts the bootstrap next to it: a guard
// then reads `context.bootstrap` without a fetch of its own (design M0 §7.3). Building it lives in
// `app/router.ts`, which is also where the routes the modules declare join the tree.
const router = createHubRouter(queryClient);

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router;
  }
}

const container = document.getElementById('root');
if (!container) {
  throw new Error('Root container #root is missing from index.html');
}

createRoot(container).render(
  <StrictMode>
    <HubProviders i18n={createI18n(registry.i18nNamespaces)} queryClient={queryClient}>
      <RouterProvider router={router} />
    </HubProviders>
  </StrictMode>,
);
