import {
  Button,
  DarkModeToggle,
  IVAOLogo,
  NavbarContainer,
  NavigationMenu,
  type NavigationMenuProps,
  Separator,
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
      {/* ⚠️ `NavbarContainer` and not `Navbar`, and the brand block written out here.
          `Navbar` puts its children in a box of their own at the far end of the line, which cannot
          be made to grow — so the menu could only ever be pushed against the tools on the right.
          Three zones with a middle that grows is the only way to centre it (Carmine, 10 September
          2026), and the price is these eight lines: the logo, the diagonal and the name, which are
          Atmosphere's own `IVAOLogo` and its own colours. */}
      <NavbarContainer className="gap-4">
        <div className="flex shrink-0 items-center gap-3">
          <div className="block md:hidden">
            <IVAOLogo color="white" onlyIcon />
          </div>
          <div className="hidden md:block">
            <IVAOLogo color="white" />
          </div>
          <div className="bg-ocean-400 dark:bg-fuselage-400 h-8 w-0.5" />
          <h1 className="text-lg font-semibold text-white">{title}</h1>
        </div>

        <div className="flex flex-1 justify-center text-white [&_a]:text-white [&_button]:text-white">
          <NavigationMenu sections={sections} asLink={RouterAnchor} />
        </div>

        <div className="flex shrink-0 items-center gap-1">
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
        </div>
      </NavbarContainer>
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
    // ⚠️ The same ground as the bar at the top (Carmine, 10 September 2026), which is why the
    // colours inside are written out rather than taken from the theme: `text-muted-foreground` and
    // `Subtle` are dark on light, and on this blue they would be hard to read in the light theme and
    // invisible in the dark one. A band that carries its own background answers for its own
    // contrast — the tokens answer for the page, and this is no longer the page.
    <footer className="bg-atmos-700 dark:bg-fuselage-800 mt-12 text-white">
      <div className="mx-auto w-full max-w-6xl px-4 py-10">
        {/* ⚠️ Centred rather than pinned to the left edge (Carmine, 10 September 2026), and that is
            why this is a wrapping flex row and not a grid: a grid of four columns holding two leaves
            two empty tracks on the right and the whole band reads as unfinished. A row that centres
            what it actually has looks deliberate whether a division writes one column or four. */}
        <div className="flex flex-wrap justify-center gap-x-16 gap-y-8">
          {/* The division: who this site belongs to, what it is for, and where else to find it. */}
          <div className="flex max-w-xs flex-col items-center gap-4 text-center">
            <p className="text-base font-semibold text-white">{division}</p>
            <p data-secondary className="text-sm text-white/70">
              {t('footer.about', { division })}
            </p>

            {/* ⚠️ The notice that this is not real aviation. It used to sit on the line at the
                bottom and was moved here when that line became the copyright: it is a thing worth
                saying about the division, and the alternative to moving it was dropping it. */}
            <p data-secondary className="text-sm text-white/60">
              {t('footer.disclaimer', { division })}
            </p>

            {marks === undefined ? null : (
              <nav aria-label={label(marks)} className="flex flex-wrap items-center gap-2">
                {marks.children.map((child) => (
                  <FooterMark key={child.path} path={child.path} label={label(child)} icon={child.icon} />
                ))}
              </nav>
            )}
          </div>

          {written.map((column) => (
            <nav key={column.path || label(column)} className="flex flex-col items-center gap-3">
              {/* The heading of a column may be a link or may lead nowhere, and both are written the
                  same way in the back office: an entry with an address, or one without. */}
              <p data-secondary className="text-xs font-semibold tracking-wider text-white/60 uppercase">
                {column.path ? <FooterEntry path={column.path} label={label(column)} /> : label(column)}
              </p>

              <ul className="flex flex-col items-center gap-2">
                {column.children.map((child) => (
                  <li key={child.path}>
                    <FooterEntry path={child.path} label={label(child)} icon={child.icon} />
                  </li>
                ))}
              </ul>
            </nav>
          ))}

          {/* The links of headquarters, which are the same for every division and live in `locales/`
              because they are words and not rows. A column like any other since the line at the
              bottom stopped carrying links — and the heading is a word of the language files, not a
              menu entry, because nobody in a division edits these. */}
          {legal.length === 0 ? null : (
            <nav className="flex flex-col items-center gap-3">
              <p data-secondary className="text-xs font-semibold tracking-wider text-white/60 uppercase">
                {t('footer.legalHeading')}
              </p>

              <ul className="flex flex-col items-center gap-2">
                {legal.map((link) => (
                  <li key={link.href}>
                    <a
                      href={link.href}
                      target="_blank"
                      rel="noreferrer noopener"
                      data-secondary
                      className="text-sm text-white/70 underline-offset-2 hover:text-white hover:underline"
                    >
                      {link.label}
                    </a>
                  </li>
                ))}
              </ul>
            </nav>
          )}
        </div>

        {/* An entry with no children and no column to sit in: the shape the footer had before it had
            columns, kept so that a division that upgrades does not lose the links it already wrote. */}
        {loose.length === 0 ? null : (
          <nav className="mt-8 flex flex-wrap items-center justify-center gap-x-4 gap-y-2">
            {loose.map((item) => (
              <FooterEntry key={item.path} path={item.path} label={label(item)} icon={item.icon} />
            ))}
          </nav>
        )}

        <Separator className="my-8 bg-white/20" />

        {/* ⚠️ Two sentences and nothing else (Carmine, 10 September 2026): who this belongs to and
            which release it is, and what it is part of. **No links** — the ones that used to sit
            here are a column above now. A line at the bottom of a page is where the eye stops, and
            everything put there competes with the two facts that belong there.

            The year is the browser's. A year written into a language file is a year that is wrong
            every January, in every language at once. */}
        <div className="flex flex-col gap-2 text-center md:flex-row md:items-center md:justify-between md:text-left">
          <p data-secondary className="text-sm text-white/60">
            {t('footer.rights', {
              year: new Date().getFullYear(),
              division,
              version: bootstrap.version,
            })}
          </p>

          <p data-secondary className="text-sm text-white/60">
            {t('footer.partOf')}
          </p>
        </div>
      </div>
    </footer>
  );
}

/** One account of the division: a mark, with the words it was given as its name. */
function FooterMark({ path, label, icon }: { path: string; label: string; icon: string | null }) {
  const className =
    'flex size-9 items-center justify-center rounded-md text-white/70 transition-colors hover:bg-white/10 hover:text-white';

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
    'inline-flex items-center gap-2 text-sm text-white/70 underline-offset-2 hover:text-white hover:underline';

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
