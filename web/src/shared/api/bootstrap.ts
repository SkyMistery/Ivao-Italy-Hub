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
 * The department codes IVAO itself uses. Not a mechanical suffix: ATC operations is `AOD` but
 * training is `TD`, and headquarters is plain `HQ`.
 */
export type Department = components['schemas']['Department'];

/** A field translated into the languages of the division. */
export type LocalizedString = components['schemas']['LocalizedOfstring'];

/** True when the user holds the permission, on that department or on every one of them. */
export function holdsPermission(bootstrap: Bootstrap, name: string, department?: Department): boolean {
  return bootstrap.permissions.some(
    (permission) =>
      permission.name === name && (permission.department === null || permission.department === department),
  );
}
