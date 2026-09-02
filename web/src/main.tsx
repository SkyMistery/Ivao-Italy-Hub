import { ThemeProvider } from '@ivao/atmosphere-react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider, createRouter } from '@tanstack/react-router';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { I18nextProvider } from 'react-i18next';

import { createI18n } from './app/i18n';
import { routeTree } from './routeTree.gen';
import { bootstrapKey } from './features/me/queries';
import { setUnauthorizedHandler } from './shared/api/client';
import './styles/index.css';

const queryClient = new QueryClient();

// A 401 means the session is gone: the cached bootstrap is stale and the shell must redraw as
// anonymous rather than keep showing a name.
setUnauthorizedHandler(() => {
  void queryClient.invalidateQueries({ queryKey: bootstrapKey });
});

const router = createRouter({ routeTree });

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
    <I18nextProvider i18n={createI18n()}>
      <QueryClientProvider client={queryClient}>
        <ThemeProvider>
          <RouterProvider router={router} />
        </ThemeProvider>
      </QueryClientProvider>
    </I18nextProvider>
  </StrictMode>,
);
