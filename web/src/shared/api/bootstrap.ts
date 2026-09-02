/**
 * The payload of `GET /api/me`, the one endpoint the application bootstraps from: menus, enabled
 * modules, effective permissions and registries all come from here, so nothing about the division
 * is hardcoded in the client.
 *
 * Written by hand on purpose, and only until F5: from there the whole surface of the API is
 * generated from the OpenAPI document into `schema.d.ts`, and this file goes away.
 */

export type Department = 'HQ' | 'SO' | 'FO' | 'AO' | 'TR' | 'MB' | 'EV' | 'PR' | 'WM';

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

/** True when the user holds the permission, on that department or on every one of them. */
export function holdsPermission(bootstrap: Bootstrap, name: string, department?: Department): boolean {
  return bootstrap.permissions.some(
    (permission) =>
      permission.name === name && (permission.department === null || permission.department === department),
  );
}
