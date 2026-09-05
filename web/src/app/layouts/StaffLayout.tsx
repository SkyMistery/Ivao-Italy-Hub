import { Sidebar, type SidebarProps } from '@ivao/atmosphere-react';
import { Outlet, useLocation } from '@tanstack/react-router';
import { Boxes, FileText, KeyRound, Link2, ScrollText, ShieldCheck, Sparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { type Bootstrap, holdsPermissionAnywhere, reachableDepartments } from '../../shared/api/bootstrap';
import { deptParam } from '../../shared/api/department';

import { AppFooter, AppHeader } from './Chrome';
import { RouterAnchor } from './RouterAnchor';

/**
 * The back office. One group per department the member may work in — their own, or all of them
 * when the role reaches everywhere — and under each, the resources of that department: `content`
 * and `links`, plus whatever the modules put in `navigation.staff`, which the server has already
 * narrowed to the entries this person may actually follow (design M0 §7.2).
 *
 * The administration group only appears for whoever holds `Admin.Access`, and each entry inside it
 * only for whoever holds the permission its screen is behind. A menu entry that leads to a 403 is a
 * menu entry that teaches people to ignore the menu.
 */

/**
 * One entry of a sidebar group, as Atmosphere types it. Reached through the props of the component
 * itself rather than by importing its type: the same rule the list engine follows, so that nothing
 * here becomes a dependency the package.json does not declare.
 */
type SidebarEntry = Extract<SidebarProps['items'][number], { items: unknown }>['items'][number];

/** The permission `/staff/admin/*` is behind, and the ones the screens under it are behind. */
const ADMIN_ACCESS = 'Admin.Access';
const PERMISSIONS_MANAGE = 'Permissions.Manage';
const MODULES_MANAGE = 'Modules.Manage';
const AUDIT_VIEW = 'Audit.View';

export function StaffLayout({ bootstrap }: { bootstrap: Bootstrap }) {
  const { t } = useTranslation();
  const location = useLocation();

  const departments = reachableDepartments(bootstrap);

  const items: SidebarProps['items'] = departments.map((department) => ({
    title: department,
    Icon: ShieldCheck,
    items: [
      {
        title: t('content.title'),
        description: t('content.description'),
        Icon: FileText,
        href: `/staff/${deptParam.format(department)}/content`,
      },
      {
        title: t('links.title'),
        description: t('links.description'),
        Icon: Link2,
        href: `/staff/${deptParam.format(department)}/links`,
      },
    ],
  }));

  // What the modules add to the back office. The server has already dropped the entries this
  // person may not follow, so there is nothing to filter here.
  const moduleEntries: SidebarEntry[] = bootstrap.navigation.staff
    .filter((entry) => entry.path !== '/staff')
    .map((entry) => ({ title: t(entry.key), description: '', Icon: Boxes, href: entry.path }));

  if (moduleEntries.length > 0) {
    items.push({ title: t('nav.modules'), Icon: Boxes, items: moduleEntries });
  }

  if (holdsPermissionAnywhere(bootstrap, ADMIN_ACCESS)) {
    const administration: SidebarEntry[] = [];

    if (holdsPermissionAnywhere(bootstrap, PERMISSIONS_MANAGE)) {
      administration.push({
        title: t('grants.title'),
        description: t('grants.description'),
        Icon: KeyRound,
        href: '/staff/admin/permissions',
      });
    }

    if (holdsPermissionAnywhere(bootstrap, MODULES_MANAGE)) {
      administration.push({
        title: t('modules.title'),
        description: t('modules.description'),
        Icon: Boxes,
        href: '/staff/admin/modules',
      });
    }

    if (holdsPermissionAnywhere(bootstrap, AUDIT_VIEW)) {
      administration.push({
        title: t('audit.title'),
        description: t('audit.description'),
        Icon: ScrollText,
        href: '/staff/admin/audit',
      });
    }

    administration.push({
      title: t('uiKit.title'),
      description: t('uiKit.description'),
      Icon: Sparkles,
      href: '/staff/admin/ui-kit',
    });

    items.push({ title: t('admin.title'), Icon: ShieldCheck, items: administration });
  }

  return (
    <div className="bg-body text-foreground flex min-h-screen flex-col">
      <AppHeader bootstrap={bootstrap} />

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
