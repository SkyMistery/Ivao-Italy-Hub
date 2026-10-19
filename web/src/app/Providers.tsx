import { ThemeProvider, TooltipProvider } from '@ivao/atmosphere-react';
import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import type { i18n as I18n } from 'i18next';
import type { ReactNode } from 'react';
import { I18nextProvider } from 'react-i18next';

import { Toaster } from '../shared/ui/Toaster';

/**
 * Everything the application needs above the router, in one place.
 *
 * It is a component rather than four lines in `main.tsx` for one reason: the test that mounts the
 * shell has to mount **this**, not a list of its own. A test carrying its own copy of the provider
 * stack passes while the real one is missing a provider, which is exactly what happened —
 * `TooltipProvider` was absent, every screen behind a layout died in the root error boundary, and
 * all 74 unit tests stayed green because none of them assembled the tree.
 *
 * So: a provider the application needs goes here, and nowhere else.
 *
 * `Toaster` is here for the same reason and by the same rule. It keeps the queue and draws the
 * viewport a confirmation appears in, `useNotice` pushes into it, and a screen that calls that
 * without this above it gets the core's error rather than a silence. It is the core's, not
 * Atmosphere's `ToastProvider`, so that its three words are said in the language on screen (#266);
 * it sits inside `I18nextProvider` because it asks for them.
 *
 * On `TooltipProvider` in particular — several Atmosphere components wrap themselves in a Radix
 * tooltip, `DarkModeToggle` among them, and it sits in the header of every layout. A tooltip
 * without this provider above it throws rather than degrading.
 */
export function HubProviders({
  i18n,
  queryClient,
  children,
}: {
  i18n: I18n;
  queryClient: QueryClient;
  children: ReactNode;
}) {
  return (
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <ThemeProvider>
          <TooltipProvider>
            <Toaster>{children}</Toaster>
          </TooltipProvider>
        </ThemeProvider>
      </QueryClientProvider>
    </I18nextProvider>
  );
}
