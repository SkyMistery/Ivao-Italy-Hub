import { Sidebar, type SidebarProps } from '@ivao/atmosphere-react';
import { Outlet, useLocation } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { SearchPalette } from '../../features/search/SearchPalette';
import type { Bootstrap } from '../../shared/api/bootstrap';

import { AppFooter, AppHeader } from './Chrome';
import { RouterAnchor } from './RouterAnchor';
import { staffDestinations } from './staffDestinations';

/**
 * The back office. One group per department the member may work in — their own, or all of them
 * when the role reaches everywhere — and under each, the resources of that department, plus
 * whatever the modules put in `navigation.staff` and the administration screens (design M0 §7.2).
 *
 * ⚠️ Where those entries come from is `staffDestinations`, and deliberately not this file: the ⌘K
 * palette offers the same places, and two lists is how a screen ends up reachable from one and not
 * from the other.
 */
export function StaffLayout({ bootstrap }: { bootstrap: Bootstrap }) {
  const { t } = useTranslation();
  const location = useLocation();

  const items: SidebarProps['items'] = staffDestinations(bootstrap, t).map((group) => ({
    title: group.title,
    Icon: group.Icon,
    items: group.items.map((item) => ({
      title: item.title,
      description: item.description,
      Icon: item.Icon,
      href: item.href,
    })),
  }));

  return (
    <div className="bg-body text-foreground flex min-h-screen flex-col">
      <AppHeader bootstrap={bootstrap} />

      {/* Everywhere in the back office, because a palette that only opens on one screen is a
          palette nobody learns (design M1 §7). */}
      <SearchPalette bootstrap={bootstrap} />

      {/* `Sidebar` is the whole thing: it brings its own `SidebarProvider` and its own
          `SidebarContainer`, and `SidebarContainer` is not a two column shell -- it *is* the
          `<aside>`, `w-72` wide. Wrapping our own around it put both the real sidebar and this
          `<main>` inside a 288px aside, so every back office screen was drawn in a narrow column
          with the rest of the window empty, and the collapse button appeared twice. The row is
          ours to make; the sidebar is not. */}
      <div className="flex flex-1 items-stretch">
        <Sidebar
          items={items}
          asLink={RouterAnchor}
          isActiveCheck={(href) => location.pathname === href || location.pathname.startsWith(`${href}/`)}
        />
        <main className="min-w-0 flex-1 px-4 py-8">
          <Outlet />
        </main>
      </div>

      <AppFooter bootstrap={bootstrap} />
    </div>
  );
}
