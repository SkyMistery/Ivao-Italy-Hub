import { QueryClient } from '@tanstack/react-query';
import { RouterProvider } from '@tanstack/react-router';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import { HubProviders } from './app/Providers';
import { createI18n } from './app/i18n';
import { registry } from './app/registry';
import { createHubRouter } from './app/router';
import { sessionChanged } from './features/me/queries';
import { setUnauthorizedHandler } from './shared/api/client';

// ⚠️ The two typefaces of IVAO, finally loaded. Atmosphere has always *asked* for them —
// `--ivao-font-head: Poppins` and `--ivao-font-sans: "Nunito Sans"` — and never shipped them, so
// until 11 September 2026 every screen of the hub fell back to whatever sans-serif the reader's
// machine had. Found when IVAO's PR department asked for the typeface of va.ivao.aero, which turned
// out to be these same two.
//
// Self-hosted and not fetched from Google: Vite bundles the files, so the site asks no third party
// for anything on a visitor's behalf. Both are under the SIL Open Font License 1.1. Only the weights
// the hub draws with are imported, and each one splits by script, so a page in Italian downloads the
// Latin files and nothing else.
import '@fontsource/poppins/400.css';
import '@fontsource/poppins/500.css';
import '@fontsource/poppins/600.css';
import '@fontsource/poppins/700.css';
import '@fontsource/poppins/800.css';
import '@fontsource/nunito-sans/400.css';
import '@fontsource/nunito-sans/600.css';
import '@fontsource/nunito-sans/700.css';
import '@fontsource/nunito-sans/800.css';
import './styles/index.css';

const queryClient = new QueryClient();

// The router carries the query client, and the root route puts the bootstrap next to it: a guard
// then reads `context.bootstrap` without a fetch of its own (design M0 §7.3). Building it lives in
// `app/router.ts`, which is also where the routes the modules declare join the tree.
const router = createHubRouter(queryClient);

// A 401 means the session is gone: the cached bootstrap is stale and the shell must redraw as
// anonymous rather than keep showing a name. It is the same job the sign out button does, so it is
// the same call — and it needs the router, which is why it is set up after it and not before.
setUnauthorizedHandler(() => {
  void sessionChanged(queryClient, router);
});

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
