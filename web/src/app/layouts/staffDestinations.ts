import {
  Boxes,
  CalendarDays,
  FileArchive,
  FileText,
  Images,
  KeyRound,
  LayoutDashboard,
  Link2,
  Mail,
  Menu as MenuIcon,
  Newspaper,
  ScrollText,
  ShieldCheck,
  Sparkles,
  Tags,
} from 'lucide-react';
import type { ComponentType } from 'react';

import {
  type Bootstrap,
  holdsPermissionAnywhere,
  menuDepartment,
  reachableDepartments,
} from '../../shared/api/bootstrap';
import { deptParam } from '../../shared/api/department';
import { DEPARTMENT_MARKS } from '../../shared/icons/departmentMark';

/**
 * Everywhere a member of staff may go, grouped the way the back office is: one group per department
 * they may work in, then the modules, then the administration.
 *
 * It lives on its own because **two** things need it and neither may hold its own copy: the sidebar
 * draws it, and the ⌘K palette offers it next to the search results (design M1 §7). A second list
 * is how a screen ends up reachable from the sidebar and not from the palette, or the other way
 * round — and nobody notices for a month.
 *
 * What it does **not** do is decide who may go there. Every entry here is already behind a
 * permission the caller checked: the departments are the ones the bootstrap says this person
 * reaches, and the administration entries are asked for one at a time. A menu entry leading to a
 * 403 is a menu entry that teaches people to ignore the menu.
 */

/** One place to go. `description` is what the sidebar shows under it; the palette ignores it. */
export interface StaffDestination {
  readonly title: string;
  readonly description: string;
  readonly href: string;
  readonly Icon: ComponentType<{ className?: string }>;
}

/** A heading and what is under it. */
export interface StaffDestinationGroup {
  readonly title: string;
  readonly Icon: ComponentType<{ className?: string }>;
  readonly items: readonly StaffDestination[];
}

/** The permission `/staff/admin/*` is behind, and the ones the screens under it are behind. */
const ADMIN_ACCESS = 'Admin.Access';
const PERMISSIONS_MANAGE = 'Permissions.Manage';
const MODULES_MANAGE = 'Modules.Manage';
const AUDIT_VIEW = 'Audit.View';

export function staffDestinations(bootstrap: Bootstrap, t: (key: string) => string): StaffDestinationGroup[] {
  const siteOwner = menuDepartment(bootstrap);

  const groups: StaffDestinationGroup[] = reachableDepartments(bootstrap).map((department) => {
    const at = (resource: string) => `/staff/${deptParam.format(department)}${resource}`;

    return {
      title: department,
      // The code is the mark: no icon for a department (decided 7 Sep 2026, after the demo). All
      // nine used to carry the same shield, which told nobody anything.
      Icon: DEPARTMENT_MARKS[department],
      items: [
        // The home of the department, and the first entry because it is where `/staff` lands.
        {
          title: t('dashboard.short'),
          description: t('dashboard.description'),
          Icon: LayoutDashboard,
          href: at(''),
        },
        {
          title: t('content.title'),
          description: t('content.description'),
          Icon: FileText,
          href: at('/content'),
        },
        { title: t('news.title'), description: t('news.description'), Icon: Newspaper, href: at('/news') },
        {
          title: t('documents.title'),
          description: t('documents.description'),
          Icon: FileArchive,
          href: at('/documents'),
        },
        {
          title: t('calendar.title'),
          description: t('calendar.description'),
          Icon: CalendarDays,
          href: at('/calendar'),
        },
        {
          title: t('categories.title'),
          description: t('categories.description'),
          Icon: Tags,
          href: at('/categories'),
        },
        {
          title: t('contacts.title'),
          description: t('contacts.description'),
          Icon: Mail,
          href: at('/contacts'),
        },
        { title: t('links.title'), description: t('links.description'), Icon: Link2, href: at('/links') },
        { title: t('media.title'), description: t('media.description'), Icon: Images, href: at('/media') },
        // The menu of the site belongs to one department, so the entry exists under that one and
        // nowhere else. Which department it is comes from the bootstrap and never from here.
        ...(department === siteOwner
          ? [
              {
                title: t('menu.title'),
                description: t('menu.description'),
                Icon: MenuIcon,
                href: at('/menu'),
              },
            ]
          : []),
      ],
    };
  });

  // What the modules add to the back office. The server has already dropped the entries this person
  // may not follow, so there is nothing to filter here.
  const modules = bootstrap.navigation.staff
    .filter((entry) => entry.path !== '/staff')
    .map((entry) => ({
      title: entry.key === null ? entry.path : t(entry.key),
      description: '',
      Icon: Boxes,
      href: entry.path,
    }));

  if (modules.length > 0) {
    groups.push({ title: t('nav.modules'), Icon: Boxes, items: modules });
  }

  if (holdsPermissionAnywhere(bootstrap, ADMIN_ACCESS)) {
    const administration: StaffDestination[] = [];

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

    groups.push({ title: t('admin.title'), Icon: ShieldCheck, items: administration });
  }

  return groups;
}
