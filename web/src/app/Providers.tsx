import { ThemeProvider, TooltipProvider } from '@ivao/atmosphere-react';
import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import type { i18n as I18n } from 'i18next';
import type { ReactNode } from 'react';
import { I18nextProvider } from 'react-i18next';

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
          <TooltipProvider>{children}</TooltipProvider>
        </ThemeProvider>
      </QueryClientProvider>
    </I18nextProvider>
  );
}
