import {
  Boxes,
  CalendarDays,
  FileArchive,
  FileText,
  Images,
  KeyRound,
  LayoutDashboard,
  LayoutTemplate,
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
  type Department,
  holdsPermission,
  holdsPermissionAnywhere,
  menuDepartment,
  reachableDepartments,
} from '../../shared/api/bootstrap';
import { deptParam } from '../../shared/api/department';
import { DEPARTMENT_MARKS } from '../../shared/icons/departmentMark';

/**
 * Everywhere a member of staff may go, grouped the way the back office is: the content of every
 * department first, then one group per department they may work in, then the modules, then the
 * administration.
 *
 * Since 13 September 2026 pages, news, documents, templates, links and media are **one screen per
 * object**, not one per department (note 2026-09-13-contenuti-centralizzati): the first group opens
 * them on every department this person reaches, and the entries under a department open the same
 * screens with that department already chosen.
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
  /**
   * The short code the heading used to be, kept because people type it: somebody looking for the
   * documents of Events writes "ED doc" into the palette, not the word. Absent for the groups that
   * are not a department.
   */
  readonly code?: string;
  readonly Icon: ComponentType<{ className?: string }>;
  readonly items: readonly StaffDestination[];
}

/** The permission `/staff/admin/*` is behind, and the ones the screens under it are behind. */
const ADMIN_ACCESS = 'Admin.Access';
const PERMISSIONS_MANAGE = 'Permissions.Manage';
const MODULES_MANAGE = 'Modules.Manage';
/** Global, like the three above it: the calendar vocabulary belongs to the division. */
const CALENDAR_MANAGE_KINDS = 'Calendar.ManageKinds';
/** Departmental, unlike the four above: templates belong to the department that wrote them. */
const CONTENT_MANAGE_TEMPLATES = 'Content.ManageTemplates';
/** What each screen of the content group is behind, anywhere. */
const CONTENT_VIEW = 'Content.View';
const LINKS_VIEW = 'Links.View';
const MEDIA_VIEW = 'Media.View';
const AUDIT_VIEW = 'Audit.View';

export function staffDestinations(bootstrap: Bootstrap, t: (key: string) => string): StaffDestinationGroup[] {
  const siteOwner = menuDepartment(bootstrap);

  /**
   * The content screens, of every department (`department` absent) or of one. One list, used by
   * both groups, so the two cannot offer different screens.
   */
  const content = (department?: Department): StaffDestination[] => {
    const of = department === undefined ? '' : `&department=${department}`;
    const only = department === undefined ? '' : `department=${department}`;

    return [
      ...(holdsPermissionAnywhere(bootstrap, CONTENT_VIEW)
        ? [
            {
              title: t('content.title'),
              description: t('content.description'),
              Icon: FileText,
              href: `/staff/content?kind=Page${of}`,
            },
            {
              title: t('news.title'),
              description: t('news.description'),
              Icon: Newspaper,
              href: `/staff/content?kind=News${of}`,
            },
            {
              title: t('documents.title'),
              description: t('documents.description'),
              Icon: FileArchive,
              href: `/staff/content?kind=Document${of}`,
            },
          ]
        : []),
      // Templates are offered only to whoever may change them: every staff member *reads* them —
      // that is what makes "new from a template" work across departments — but a menu entry leading
      // to a screen with nothing to do on it would be a menu teaching people to ignore the menu.
      ...((
        department === undefined
          ? holdsPermissionAnywhere(bootstrap, CONTENT_MANAGE_TEMPLATES)
          : holdsPermission(bootstrap, CONTENT_MANAGE_TEMPLATES, department)
      )
        ? [
            {
              title: t('templates.title'),
              description: t('templates.description'),
              Icon: LayoutTemplate,
              href: `/staff/content?kind=Template${of}`,
            },
          ]
        : []),
      ...(holdsPermissionAnywhere(bootstrap, LINKS_VIEW)
        ? [
            {
              title: t('links.title'),
              description: t('links.description'),
              Icon: Link2,
              href: only === '' ? '/staff/links' : `/staff/links?${only}`,
            },
          ]
        : []),
      ...(holdsPermissionAnywhere(bootstrap, MEDIA_VIEW)
        ? [
            {
              title: t('media.title'),
              description: t('media.description'),
              Icon: Images,
              href: only === '' ? '/staff/media' : `/staff/media?${only}`,
            },
          ]
        : []),
    ];
  };

  const groups: StaffDestinationGroup[] = [];

  // Only for somebody who works in more than one department: for everybody else it would be the
  // entries of their department, twice.
  const everyDepartment = reachableDepartments(bootstrap).length > 1 ? content() : [];
  if (everyDepartment.length > 0) {
    groups.push({ title: t('backOffice.content'), Icon: FileText, items: everyDepartment });
  }

  const departments: StaffDestinationGroup[] = reachableDepartments(bootstrap).map((department) => {
    const at = (resource: string) => `/staff/${deptParam.format(department)}${resource}`;

    return {
      // ⚠️ The name and not the code since 11 September 2026 (Carmine: "ED becomes Events, AOD ATC
      // Operations"). The code is still on screen, in the square beside it, so nothing is lost and a
      // newcomer no longer has to know nine acronyms to find their way.
      title: t(`departments.${department}`),
      code: department,
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
        // The content screens of this department: the same screens as the group above, filtered.
        ...content(department),
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

  groups.push(...departments);

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

    if (holdsPermissionAnywhere(bootstrap, CALENDAR_MANAGE_KINDS)) {
      administration.push({
        title: t('calendarKinds.title'),
        description: t('calendarKinds.description'),
        Icon: Tags,
        href: '/staff/admin/calendar-kinds',
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
