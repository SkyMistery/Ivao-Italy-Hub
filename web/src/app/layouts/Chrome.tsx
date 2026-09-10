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
import { Search } from 'lucide-react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { useLogout } from '../../features/me/queries';
import type { Bootstrap } from '../../shared/api/bootstrap';
import { loginHref } from '../../shared/api/client';
import { iconGlyph } from '../../shared/icons/glyphs';
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

/** One entry of the footer menu, as the bootstrap carries it. */
type FooterItem = Bootstrap['navigation']['footer'][number];

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

  // Whether the bar shows the way into the back office. The same question the route guard asks,
  // and asked here so that nobody has to remember the address: a member of staff who lands on the
  // public site had to type `/staff` to get back to work.
  const staff = user !== null && (user.isStaff || user.isSuperadmin);

  return (
    // ⚠️ One row and not two (Carmine, 10 September 2026: the menu can live where the IVAO banner
    // is, and save the space). The menu, the tools and the account all ride in `Navbar`'s own
    // children slot, which it draws at the far end of the same line as the logo and the division's
    // name — so the height of the site's frame is the height of the banner, and nothing else.
    <header className="border-border border-b">
      <Navbar title={title}>
        <div className="text-white [&_a]:text-white [&_button]:text-white">
          <NavigationMenu sections={sections} asLink={RouterAnchor} />
        </div>

        {/* A tool of the frame and not a page of the site, which is why it sits here with the
            language and the theme rather than in the menu: the menu is what the staff writes, and
            a search box is not something anybody should have to remember to add. */}
        <Button
          asChild
          variant="ghost"
          size="sm"
          className="text-white hover:bg-white/10 hover:text-white"
          aria-label={t('search.open')}
          title={t('search.open')}
        >
          <Link to="/search" search={{ q: '', page: 1 }}>
            <Search aria-hidden className="size-4" />
          </Link>
        </Button>

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

        {staff ? (
          // Only for somebody the guard would let in. A button that leads to `/forbidden` is a
          // button that teaches people to distrust the bar it sits in.
          <Button asChild variant="secondary" size="sm">
            <Link to="/staff">{t('nav.staff')}</Link>
          </Button>
        ) : null}

        {user === null ? (
          // A full navigation, not a router link: /auth/login is a Kestrel endpoint.
          //
          // ⚠️ `secondary` and not the primary variant, now that it sits on the banner: the primary
          // button is the same blue as the bar behind it, so the one call to action of the public
          // site was a dark rectangle on a dark rectangle. Measured by looking at it.
          <Button asChild variant="secondary" size="sm">
            <a href={loginHref(window.location.pathname)}>{t('auth.login')}</a>
          </Button>
        ) : (
          <>
            <Button
              asChild
              variant="ghost"
              size="sm"
              className="text-white hover:bg-white/10 hover:text-white"
            >
              <Link to="/me">{displayName(user.firstName, user.lastName, user.vid)}</Link>
            </Button>
            <Button
              variant="ghost"
              size="sm"
              className="text-white hover:bg-white/10 hover:text-white"
              onClick={() => logout.mutate()}
              disabled={logout.isPending}
            >
              {t('auth.logout')}
            </Button>
          </>
        )}
      </Navbar>
    </header>
  );
}

/**
 * The foot of every page (asked for by Carmine on 10 September 2026, with the footer of the UK &
 * Ireland division in front of him): the division on the left, then the columns of links, then one
 * quiet line underneath.
 *
 * ⚠️ **The columns are the footer menu, and nothing here decides what is in them.** A top level
 * entry of `Scope = Footer` is a column and its children are its links, which is a shape
 * `cms_menu_items` has carried since G8 — what was missing was only that an entry could be a
 * *heading*, leading nowhere, and that it could carry a mark. Both are one column of the table
 * each, and both are edited where every other menu entry is edited: the back office of the
 * department that owns the site (design M1 §8.1).
 *
 * ⚠️ And **one rule turns a column into the row of accounts**: a column whose links *all* carry an
 * icon is drawn under the division's own words as a row of marks, rather than as a fourth column of
 * text. It is the one piece of this that is inferred rather than declared, and it is inferred
 * because the alternative was a second field on every menu entry to answer a question only one
 * column in the whole site ever asks.
 */
export function AppFooter({ bootstrap }: { bootstrap: Bootstrap }) {
  const { t, i18n } = useTranslation();

  const division = resolveLocalized(bootstrap.division.name, i18n.language, bootstrap.division.defaultLocale);

  const label = (item: FooterItem) => navLabel(item, t, i18n.language, bootstrap.division.defaultLocale);

  const columns = bootstrap.navigation.footer.filter((item) => item.children.length > 0);
  const loose = bootstrap.navigation.footer.filter((item) => item.children.length === 0);

  const marks = columns.find((column) => column.children.every((child) => child.icon));
  const written = columns.filter((column) => column !== marks);

  // The links of headquarters are content, not code: `locales/{lng}/common.json` carries them, so a
  // division that forks this hub changes them where it changes every other sentence.
  const raw: unknown = t('footer.legal', { returnObjects: true });
  const legal: LegalLink[] = Array.isArray(raw) ? (raw as LegalLink[]) : [];

  return (
    <footer className="border-border mt-12 border-t">
      <div className="mx-auto w-full max-w-6xl px-4 py-10">
        <div className="grid grid-cols-1 gap-8 md:grid-cols-2 lg:grid-cols-4">
          {/* The division: who this site belongs to, what it is for, and where else to find it. */}
          <div className="flex flex-col gap-4 lg:col-span-1">
            <p className="text-foreground text-base font-semibold">{division}</p>
            <p className="text-muted-foreground max-w-xs text-sm">{t('footer.about', { division })}</p>

            {marks === undefined ? null : (
              <nav aria-label={label(marks)} className="flex flex-wrap items-center gap-2">
                {marks.children.map((child) => (
                  <FooterMark key={child.path} path={child.path} label={label(child)} icon={child.icon} />
                ))}
              </nav>
            )}
          </div>

          {written.map((column) => (
            <nav key={column.path || label(column)} className="flex flex-col gap-3">
              {/* The heading of a column may be a link or may lead nowhere, and both are written the
                  same way in the back office: an entry with an address, or one without. */}
              <p className="text-muted-foreground text-xs font-semibold tracking-wider uppercase">
                {column.path ? <FooterEntry path={column.path} label={label(column)} /> : label(column)}
              </p>

              <ul className="flex flex-col gap-2">
                {column.children.map((child) => (
                  <li key={child.path}>
                    <FooterEntry path={child.path} label={label(child)} icon={child.icon} />
                  </li>
                ))}
              </ul>
            </nav>
          ))}
        </div>

        {/* An entry with no children and no column to sit in: the shape the footer had before it had
            columns, kept so that a division that upgrades does not lose the links it already wrote. */}
        {loose.length === 0 ? null : (
          <nav className="mt-8 flex flex-wrap items-center gap-x-4 gap-y-2">
            {loose.map((item) => (
              <FooterEntry key={item.path} path={item.path} label={label(item)} icon={item.icon} />
            ))}
          </nav>
        )}

        <Separator className="my-8" />

        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div className="flex flex-col gap-1">
            <Subtle>{t('footer.disclaimer', { division })}</Subtle>
            <Subtle>{t('footer.version', { version: bootstrap.version })}</Subtle>
          </div>

          <nav className="flex flex-wrap items-center gap-x-4 gap-y-2">
            {legal.map((link) => (
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
        </div>
      </div>
    </footer>
  );
}

/** One account of the division: a mark, with the words it was given as its name. */
function FooterMark({ path, label, icon }: { path: string; label: string; icon: string | null }) {
  const className =
    'text-muted-foreground hover:text-foreground hover:bg-muted flex size-9 items-center justify-center rounded-md transition-colors';

  const glyph = iconGlyph(icon, 'size-4');

  // A name that this release has never heard of draws nothing rather than a wrong picture, which is
  // what `iconGlyph` is for — but the link must survive it, so the words stand in for the mark.
  const inside = glyph ?? label;

  return path.startsWith('/') ? (
    <RouterAnchor href={path} className={className} aria-label={label} title={label}>
      {inside}
    </RouterAnchor>
  ) : (
    <a
      href={path}
      target="_blank"
      rel="noreferrer noopener"
      className={className}
      aria-label={label}
      title={label}
    >
      {inside}
    </a>
  );
}

/**
 * One entry of the footer menu. A path of this site is followed by the router, which keeps the
 * single page application single; anything else is an ordinary link out, and the editor is allowed
 * to write one — `MenuItemWriteDtoValidator` accepts a path or an absolute web address and nothing
 * in between.
 */
function FooterEntry({ path, label, icon = null }: { path: string; label: string; icon?: string | null }) {
  const className =
    'text-muted-foreground hover:text-foreground inline-flex items-center gap-2 text-sm underline-offset-2 hover:underline';

  const inside = (
    <>
      {iconGlyph(icon, 'size-4 shrink-0')}
      {label}
    </>
  );

  return path.startsWith('/') ? (
    <RouterAnchor href={path} className={className}>
      {inside}
    </RouterAnchor>
  ) : (
    <a href={path} target="_blank" rel="noreferrer noopener" className={className}>
      {inside}
    </a>
  );
}

/**
 * The frame the three layouts put their content in.
 *
 * `banner` is drawn between the header and the content, edge to edge: it is where something that
 * belongs to the whole window rather than to the reading column goes, and today that is the live
 * strip of the public site. It is a slot and not a component of its own because the frame decides
 * *where*, and the layout decides *what* — the back office has no banner and asks for none.
 */
export function Shell({
  bootstrap,
  banner,
  children,
}: {
  bootstrap: Bootstrap;
  banner?: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="bg-body text-foreground flex min-h-screen flex-col">
      <AppHeader bootstrap={bootstrap} />
      {banner}
      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-8">{children}</main>
      <AppFooter bootstrap={bootstrap} />
    </div>
  );
}

function displayName(firstName: string, lastName: string, vid: number): string {
  const full = [firstName, lastName].filter((part) => part.length > 0).join(' ');
  return full.length > 0 ? full : String(vid);
}
