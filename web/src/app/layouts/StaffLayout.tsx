import { Sidebar, SidebarContainer, SidebarProvider, type SidebarProps } from '@ivao/atmosphere-react';
import { Outlet, useLocation } from '@tanstack/react-router';
import { FileText, Link2, ShieldCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { type Bootstrap, holdsPermissionAnywhere, reachableDepartments } from '../../shared/api/bootstrap';
import { deptParam } from '../../shared/api/department';

import { AppFooter, AppHeader } from './Chrome';
import { RouterAnchor } from './RouterAnchor';

/**
 * The back office. One group per department the member may work in — their own, or all of them
 * when the role reaches everywhere — and under each, the resources of that department. In M0 there
 * are two, `content` and `links`; F8 adds what the modules bring to the same list (design M0 §7.2).
 *
 * The administration group only appears for whoever holds `Admin.Access`. A menu entry that leads
 * to a 403 is a menu entry that teaches people to ignore the menu.
 */

/** The permission `/staff/admin/*` is behind. */
const ADMIN_ACCESS = 'Admin.Access';

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

  if (holdsPermissionAnywhere(bootstrap, ADMIN_ACCESS)) {
    items.push({
      title: t('admin.title'),
      Icon: ShieldCheck,
      items: [
        {
          title: t('uiKit.title'),
          description: t('uiKit.description'),
          Icon: Link2,
          href: '/staff/admin/ui-kit',
        },
      ],
    });
  }

  return (
    <div className="bg-body text-foreground flex min-h-screen flex-col">
      <AppHeader bootstrap={bootstrap} />

      <SidebarProvider>
        <SidebarContainer>
          <Sidebar
            items={items}
            asLink={RouterAnchor}
            isActiveCheck={(href) => location.pathname === href || location.pathname.startsWith(`${href}/`)}
          />
          <main className="w-full flex-1 px-4 py-8">
            <Outlet />
          </main>
        </SidebarContainer>
      </SidebarProvider>

      <AppFooter bootstrap={bootstrap} />
    </div>
  );
}
