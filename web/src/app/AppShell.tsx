import { Button, Navbar } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { bootstrapQuery, useLogout } from '../features/me/queries';
import { loginHref } from '../shared/api/client';

/**
 * The frame around every page in F2: the division name and a way in and out.
 * F6 replaces it with the three real layouts (`_public`, `_member`, `_staff`).
 */
export function AppShell({ children }: { children: ReactNode }) {
  const { t, i18n } = useTranslation();
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const logout = useLogout();

  const user = bootstrap?.user ?? null;
  const title = bootstrap
    ? resolveDivisionName(bootstrap.division.name, i18n.language, bootstrap.division.defaultLocale)
    : t('app.title');

  return (
    <div className="min-h-screen bg-body text-foreground">
      <Navbar title={title} />
      <div className="mx-auto flex max-w-3xl items-center justify-end gap-3 px-4 pt-4">
        {user ? (
          <>
            <Link to="/me" className="text-sm underline">
              {t('auth.signedInAs', { name: displayName(user.firstName, user.lastName, user.vid) })}
            </Link>
            <Button variant="secondary" onClick={() => logout.mutate()} disabled={logout.isPending}>
              {t('auth.logout')}
            </Button>
          </>
        ) : (
          // A full navigation, not a router link: /auth/login is a Kestrel endpoint.
          <Button asChild>
            <a href={loginHref(window.location.pathname)}>{t('auth.login')}</a>
          </Button>
        )}
      </div>
      <main className="mx-auto flex max-w-3xl flex-col gap-4 px-4 py-8">{children}</main>
    </div>
  );
}

/** Current language, then the fallback of the division, then whatever is there. */
function resolveDivisionName(name: Record<string, string>, locale: string, defaultLocale: string): string {
  return (
    name[locale] ?? name[locale.split('-')[0] ?? ''] ?? name[defaultLocale] ?? Object.values(name)[0] ?? ''
  );
}

function displayName(firstName: string, lastName: string, vid: number): string {
  const full = [firstName, lastName].filter((part) => part.length > 0).join(' ');
  return full.length > 0 ? full : String(vid);
}
