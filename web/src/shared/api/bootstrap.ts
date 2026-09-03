/**
 * The payload of `GET /api/me`, the one endpoint the application bootstraps from: menus, enabled
 * modules, effective permissions and registries all come from here, so nothing about the division
 * is hardcoded in the client.
 *
 * Written by hand on purpose, and only until F5: from there the whole surface of the API is
 * generated from the OpenAPI document into `schema.d.ts`, and this file goes away.
 */

/**
 * The department codes IVAO itself uses. Not a mechanical suffix: ATC operations is `AOD` but
 * training is `TD`, and headquarters is plain `HQ`.
 */
export type Department = 'HQ' | 'SOD' | 'FOD' | 'AOD' | 'TD' | 'MD' | 'ED' | 'PRD' | 'WD';

/** A field translated into the languages of the division. */
export type LocalizedString = Record<string, string>;

export interface NavItem {
  /** A translation key such as `nav.home`, never text. */
  key: string;
  path: string;
}

export interface BootstrapUser {
  vid: number;
  firstName: string;
  lastName: string;
  positions: string[];
  isStaff: boolean;
  isSuperadmin: boolean;
  locale: string;
  departments: Department[];
  firs: string[];
}

export interface BootstrapPermission {
  name: string;
  /** Null means the permission is held on every department. */
  department: Department | null;
}

export interface BootstrapDivision {
  code: string;
  name: LocalizedString;
  locales: string[];
  defaultLocale: string;
  timezone: string;
  firStaffScope: 'all' | 'own';
}

export interface BootstrapModule {
  key: string;
  department: Department | null;
  enabled: boolean;
  maintenance: boolean;
}

export interface Bootstrap {
  user: BootstrapUser | null;
  permissions: BootstrapPermission[];
  division: BootstrapDivision;
  modules: BootstrapModule[];
  navigation: {
    public: NavItem[];
    staff: NavItem[];
  };
  registries: {
    blocks: string[];
    widgets: string[];
  };
  version: string;
}

/**
 * True when the user holds the permission on that department. A permission with no department is
 * held everywhere.
 *
 * Two functions, not one with an optional department, for the same reason the server has `Has` and
 * `HasAny` rather than one method (docs/internal/decisions/2026-09-03-has-and-has-any.md): a single
 * one leaves the caller guessing what leaving the department out was supposed to mean, and the
 * likeliest guess — "any department" — is the opposite of what it did.
 */
export function holdsPermission(bootstrap: Bootstrap, name: string, department: Department): boolean {
  return bootstrap.permissions.some(
    (permission) =>
      permission.name === name && (permission.department === null || permission.department === department),
  );
}

/**
 * True when the user holds the permission somewhere: on one department, on all of them, or as a
 * global permission. It answers "may they do this at all" — whether to show a menu entry, whether
 * to open a list — and the department is then checked row by row.
 */
export function holdsPermissionAnywhere(bootstrap: Bootstrap, name: string): boolean {
  return bootstrap.permissions.some((permission) => permission.name === name);
}
