import { DEPARTMENTS, isDepartment } from './department';
import type { components } from './schema';

/**
 * The payload of `GET /api/me`, the one endpoint the application bootstraps from: menus, enabled
 * modules, effective permissions and registries all come from here, so nothing about the division
 * is hardcoded in the client.
 *
 * Every type below is an alias of the generated contract, not a copy of it: when the server moves a
 * field, this file does not have to be remembered (design M0 section 7.4). What stays hand written
 * is only what the client does with the payload.
 */

export type Bootstrap = components['schemas']['BootstrapResponse'];
export type BootstrapUser = components['schemas']['BootstrapUser'];
export type BootstrapPermission = components['schemas']['BootstrapPermission'];
export type BootstrapDivision = components['schemas']['BootstrapDivision'];
export type BootstrapModule = components['schemas']['BootstrapModule'];
export type NavItem = components['schemas']['NavItem'];

/**
 * One word of the division's calendar vocabulary, as the bootstrap carries it: the key an entry
 * stores, the word in each language, and the colour of its chip. The list the division decides is
 * `/api/calendar-kinds`; this is the half everybody who draws a chip needs, visitors included.
 */
export type CalendarKind = components['schemas']['BootstrapCalendarKind'];

/**
 * The department codes IVAO itself uses. Not a mechanical suffix: ATC operations is `AOD` but
 * training is `TD`, and headquarters is plain `HQ`.
 */
export type Department = components['schemas']['Department'];

/** A field translated into the languages of the division. */
export type LocalizedString = components['schemas']['LocalizedOfstring'];

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

/**
 * The departments a staff member may work in: their own, or every one of them when the role
 * reaches everywhere. `hasAllDepartments` is a fact of the role, stated by the server; it is not
 * read off the shape of the permission list, for the same reason the server does not read it that
 * way (design M0 §3.3).
 */
/**
 * The department the site itself belongs to: its menu, its templates and the pages the installation
 * was born with. It comes from the bootstrap and is never written here — a client that knew which
 * department that is would be a client that knows which division it is running for.
 */
export function menuDepartment(bootstrap: Bootstrap): Department | null {
  const code = bootstrap.division.siteDepartment;
  return isDepartment(code) ? code : null;
}

export function reachableDepartments(bootstrap: Bootstrap): Department[] {
  const user = bootstrap.user;
  if (!user) {
    return [];
  }

  return user.hasAllDepartments ? [...DEPARTMENTS] : user.departments.filter(isDepartment);
}
