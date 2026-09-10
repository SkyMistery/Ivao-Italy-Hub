import { Outlet, useLocation } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { SearchPalette } from '../../features/search/SearchPalette';
import type { Bootstrap } from '../../shared/api/bootstrap';
import { StaffSidebar, type StaffSidebarGroup } from '../../shared/ui';

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

  const groups: StaffSidebarGroup[] = staffDestinations(bootstrap, t).map((group) => ({
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

      {/* ⚠️ `StaffSidebar` *is* the `<aside>`, exactly as Atmosphere's own was: it is not a panel to
          wrap in a shell of ours. Wrapping the one it replaced put both the sidebar and this
          `<main>` inside a 288px aside, so every back office screen was drawn in a narrow column
          with the rest of the window empty, and the collapse button appeared twice. The row is
          ours to make; the aside is not.

          Why it is ours since 10 September 2026 rather than the library's: the collapse button had
          to move to the top and lose its label, and neither is reachable from outside. The reasons
          are in the component. */}
      <div className="flex flex-1 items-stretch">
        <StaffSidebar
          groups={groups}
          asLink={RouterAnchor}
          isActiveCheck={(href) => location.pathname === href || location.pathname.startsWith(`${href}/`)}
        />
        <main className="flex min-w-0 flex-1 flex-col gap-6 px-4 py-8">
          {/* Everywhere in the back office, because a palette that only opens on one screen is a
              palette nobody learns (design M1 §7) — and since the demo it brings a visible box
              with it, because a shortcut nobody is told about is a shortcut nobody uses.

              ⚠️ At the top of the content column and not inside the sidebar, which is Atmosphere's
              own component and has no slot to put anything in. Wrapping it in a column of our own
              is exactly what once drew the whole back office inside a 255 pixel aside (HANDOFF
              §13), and a search box is not worth doing that again. */}
          <SearchPalette bootstrap={bootstrap} />

          <Outlet />
        </main>
      </div>

      <AppFooter bootstrap={bootstrap} />
    </div>
  );
}
