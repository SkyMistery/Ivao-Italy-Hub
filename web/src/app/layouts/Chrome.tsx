import {
  Button,
  DarkModeToggle,
  Navbar,
  NavigationMenu,
  type NavigationMenuProps,
  Separator,
  Subtle,
} from '@ivao/atmosphere-react';
import { Link } from '@tanstack/react-router';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { useLogout } from '../../features/me/queries';
import type { Bootstrap } from '../../shared/api/bootstrap';
import { loginHref } from '../../shared/api/client';
import { navLabel, resolveLocalized } from '../../shared/i18n/localized';
import { LocaleSwitcher } from '../../shared/ui';

import { RouterAnchor } from './RouterAnchor';

/**
 * The parts every layout shares: the bar at the top and the legal footer at the bottom. The three
 * layouts differ in what they put between them and in who they let in, never in their frame
 * (design M0 §7.2).
 *
 * Nothing here is written in the code. The name of the division, the menu and the languages come
 * from `GET /api/me`; the footer links come from `locales/`, so a fork changes them by translating
 * a file rather than by editing a component.
 */

/** One legal link of the footer, as the language files carry it. */
interface LegalLink {
  readonly label: string;
  readonly href: string;
}

export function AppHeader({ bootstrap }: { bootstrap: Bootstrap }) {
  const { t, i18n } = useTranslation();
  const logout = useLogout();

  const user = bootstrap.user;
  const title = resolveLocalized(bootstrap.division.name, i18n.language, bootstrap.division.defaultLocale);

  // The menu is a table now, and this is where that becomes visible: an entry taken out of
  // `cms_menu_items` leaves the site on the next request, with nothing recompiled (design M1 §8.1).
  const sections: NavigationMenuProps['sections'] = bootstrap.navigation.public.map((item) => {
    const label = navLabel(item, t, i18n.language, bootstrap.division.defaultLocale);

    if (item.children.length === 0) {
      return { title: label, href: item.path };
    }

    // Atmosphere draws a section with `links` as a drop down, and a drop down has no address of
    // its own — so the entry itself becomes the first of its own children rather than a heading
    // that leads nowhere. Depth stops here: the server never sends a third level.
    return {
      title: label,
      links: [item, ...item.children].map((entry) => ({
        title: navLabel(entry, t, i18n.language, bootstrap.division.defaultLocale),
        href: entry.path,
        description: '',
      })),
    };
  });

  return (
    <header className="border-border border-b">
      <Navbar title={title} />

      <div className="mx-auto flex w-full max-w-6xl flex-wrap items-center gap-3 px-4 py-2">
        <NavigationMenu sections={sections} asLink={RouterAnchor} />

        <div className="ml-auto flex items-center gap-2">
          <LocaleSwitcher locales={bootstrap.division.locales} signedIn={user !== null} />
          {/* `title` is the tooltip, `aria-label` is the accessible name: passing only the second
              leaves the tooltip on Atmosphere's own English, and a tooltip is not something a
              screenshot review notices because it only appears on hover.

              `children` is null because the component demands the prop in its types and then
              overwrites it: it draws a sun or a moon from the current theme. Anything passed here
              is dead markup, so the honest thing to pass is nothing. */}
          <DarkModeToggle title={t('theme.toggle')} aria-label={t('theme.toggle')}>
            {null}
          </DarkModeToggle>

          {user === null ? (
            // A full navigation, not a router link: /auth/login is a Kestrel endpoint.
            <Button asChild>
              <a href={loginHref(window.location.pathname)}>{t('auth.login')}</a>
            </Button>
          ) : (
            <>
              <Button asChild variant="ghost">
                <Link to="/me">{displayName(user.firstName, user.lastName, user.vid)}</Link>
              </Button>
              <Button variant="secondary" onClick={() => logout.mutate()} disabled={logout.isPending}>
                {t('auth.logout')}
              </Button>
            </>
          )}
        </div>
      </div>
    </header>
  );
}

export function AppFooter({ bootstrap }: { bootstrap: Bootstrap }) {
  const { t, i18n } = useTranslation();

  // The links of headquarters are content, not code: `locales/{lng}/common.json` carries them, so a
  // division that forks this hub changes them where it changes every other sentence.
  const raw: unknown = t('footer.legal', { returnObjects: true });
  const links: LegalLink[] = Array.isArray(raw) ? (raw as LegalLink[]) : [];

  return (
    <footer className="border-border mt-12 border-t">
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-3 px-4 py-6">
        {/* Two rows of links, and they are two different things. The first is what the staff put in
            the footer menu — pages of this site, edited in the back office like the top menu. The
            second is the legal links of headquarters, which are the same for every division and
            live in `locales/` because they are words and not rows. */}
        {bootstrap.navigation.footer.length > 0 && (
          <nav className="flex flex-wrap items-center gap-x-4 gap-y-2">
            {bootstrap.navigation.footer.flatMap((item) => [item, ...item.children]).map((item) => (
              <FooterEntry
                key={item.path}
                path={item.path}
                label={navLabel(item, t, i18n.language, bootstrap.division.defaultLocale)}
              />
            ))}
          </nav>
        )}

        <nav className="flex flex-wrap items-center gap-x-4 gap-y-2">
          {links.map((link) => (
            <a
              key={link.href}
              href={link.href}
              target="_blank"
              rel="noreferrer noopener"
              className="text-muted-foreground hover:text-foreground text-sm underline-offset-2 hover:underline"
            >
              {link.label}
            </a>
          ))}
        </nav>

        <Separator />

        <Subtle>
          {t('footer.disclaimer', {
            division: resolveLocalized(
              bootstrap.division.name,
              i18n.language,
              bootstrap.division.defaultLocale,
            ),
          })}
        </Subtle>
        <Subtle>{t('footer.version', { version: bootstrap.version })}</Subtle>
      </div>
    </footer>
  );
}

/**
 * One entry of the footer menu. A path of this site is followed by the router, which keeps the
 * single page application single; anything else is an ordinary link out, and the editor is allowed
 * to write one — `MenuItemWriteDtoValidator` accepts a path or an absolute web address and nothing
 * in between.
 */
function FooterEntry({ path, label }: { path: string; label: string }) {
  const className = 'text-muted-foreground hover:text-foreground text-sm underline-offset-2 hover:underline';

  return path.startsWith('/') ? (
    <RouterAnchor href={path} className={className}>
      {label}
    </RouterAnchor>
  ) : (
    <a href={path} target="_blank" rel="noreferrer noopener" className={className}>
      {label}
    </a>
  );
}

/** The frame the three layouts put their content in. */
export function Shell({ bootstrap, children }: { bootstrap: Bootstrap; children: ReactNode }) {
  return (
    <div className="bg-body text-foreground flex min-h-screen flex-col">
      <AppHeader bootstrap={bootstrap} />
      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-8">{children}</main>
      <AppFooter bootstrap={bootstrap} />
    </div>
  );
}

function displayName(firstName: string, lastName: string, vid: number): string {
  const full = [firstName, lastName].filter((part) => part.length > 0).join(' ');
  return full.length > 0 ? full : String(vid);
}
