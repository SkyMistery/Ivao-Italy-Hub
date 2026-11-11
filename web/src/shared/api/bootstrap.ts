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

/** What a row says about itself that a permission may be narrowed to: its scope (a grant on one row) and its FIR. */
export interface PermissionRow {
  readonly resourceScope?: string | null;
  readonly fir?: string | null;
}

/**
 * True when the user holds the permission on that department — and, given a row, on that row — as the server's
 * `PermissionSet.Has` answers it (issue #319, item 7). A permission with no department is held everywhere.
 *
 * - **A grant on one row** (`resourceScope`) reaches only the row with the same scope. Without a row it reaches nothing: a
 *   grant on one row gives no department (`CLAUDE.md` §2).
 * - **A FIR team's permission** (`fir`) reaches only a row of that FIR, compared without regard to case as IVAO writes
 *   them; never a row of another FIR, one with none, or no row.
 * - **A permission held from outside** (`fromOutside`) counts on its department like any other: it is the permission on that
 *   department's rows, and the handler asks no more.
 *
 * Without these the screen offered actions the server refused. Two functions, not one with an optional department, for the
 * same reason the server has `Has` and `HasAny` rather than one method (docs/internal/decisions/2026-09-03-has-and-has-any.md):
 * a single one leaves the caller guessing what leaving the department out was supposed to mean, and the likeliest guess —
 * "any department" — is the opposite of what it did.
 */
export function holdsPermission(
  bootstrap: Bootstrap,
  name: string,
  department: Department,
  row: PermissionRow = {},
): boolean {
  const scope = row.resourceScope ?? null;
  const fir = row.fir ?? null;

  return bootstrap.permissions.some((permission) => {
    // The server always says both, null for none; a permission written without them — a fixture of the smoke bench,
    // which the old question never needed — is read the same way, and not as narrowed to `undefined`.
    const heldScope = permission.resourceScope ?? null;
    const heldFir = permission.fir ?? null;

    return (
      permission.name === name &&
      (permission.department === null || permission.department === department) &&
      (heldScope === null || heldScope === scope) &&
      (heldFir === null || (fir !== null && heldFir.toLowerCase() === fir.toLowerCase()))
    );
  });
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
 * True when the user holds the permission **without a row scope**: on one department, on all of them, on a FIR, from
 * outside, or as a global permission — anything but a grant on single rows. It is the server's `HasUnscoped`
 * (note 2026-10-17-il-permesso-senza-ambito), asked by a screen of the whole division — a register — that a grant on one
 * event does not open. A super administrator's bootstrap carries every permission with no scope, so it holds them all.
 */
export function holdsPermissionUnscoped(bootstrap: Bootstrap, name: string): boolean {
  return bootstrap.permissions.some(
    (permission) => permission.name === name && permission.resourceScope === null,
  );
}

/**
 * Whether the user may change what holds for the whole division — the settings of the content
 * (`decisions/2026-11-04-la-pagina-dello-stile.md`, Carmine on 4 November 2026: «Solo chi ha tutti»): the superadmin, or
 * a role that reaches every department, with the permission. `hasAllDepartments` is the server's fact of the role
 * (`RolePermissionMatrix.ReachesEveryDepartment`), read here and never worked out again.
 */
export function mayChangeTheWholeDivision(bootstrap: Bootstrap, name: string): boolean {
  const user = bootstrap.user;
  return (
    user !== null && (user.isSuperadmin || user.hasAllDepartments) && holdsPermissionAnywhere(bootstrap, name)
  );
}

/**
 * The department the site itself belongs to: its menu, its templates and the pages the installation
 * was born with. It comes from the bootstrap and is never written here — a client that knew which
 * department that is would be a client that knows which division it is running for.
 */
export function menuDepartment(bootstrap: Bootstrap): Department | null {
  const code = bootstrap.division.siteDepartment;
  return isDepartment(code) ? code : null;
}

/**
 * The departments a staff member may work in: their own, or every one of them when the role
 * reaches everywhere. `hasAllDepartments` is a fact of the role, stated by the server; it is not
 * read off the shape of the permission list, for the same reason the server does not read it that
 * way (design M0 §3.3).
 */
export function reachableDepartments(bootstrap: Bootstrap): Department[] {
  const user = bootstrap.user;
  if (!user) {
    return [];
  }

  return user.hasAllDepartments ? [...DEPARTMENTS] : user.departments.filter(isDepartment);
}

/**
 * The departments this person may **create** a row of something in: those they reach and hold the
 * permission on. It is what a "new" button of a screen that is not about one department asks
 * (note 2026-09-13-contenuti-centralizzati): one answer, and the button goes there; several, and
 * the screen asks which. The server refuses any other department anyway. A permission held on one
 * row, or on the rows of one FIR, opens no department here (issue #319, item 7), as the server's
 * `Has` on a department with no row: the rows these buttons create carry neither.
 */
export function writableDepartments(bootstrap: Bootstrap, permission: string): Department[] {
  return reachableDepartments(bootstrap).filter((department) =>
    holdsPermission(bootstrap, permission, department),
  );
}
