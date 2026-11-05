import { holdsPermissionAnywhere, holdsPermissionUnscoped, type Bootstrap } from '../shared/api/bootstrap';
import type { RouteDefinition } from '../shared/modules';

/**
 * Whether a module's route of the back office opens to this reader, after the staff guard: the permission its manifest
 * names, held anywhere — or, when the route says `unscoped`, held without a row scope, the question the server asks of the
 * list behind it (`HasUnscoped`, note 2026-10-17-il-permesso-senza-ambito). A route that names no permission opens to the
 * staff; `unscoped` without a permission says nothing.
 */
export function opensStaffRoute(bootstrap: Bootstrap, definition: RouteDefinition): boolean {
  if (definition.permission === undefined) {
    return true;
  }

  return definition.unscoped === true
    ? holdsPermissionUnscoped(bootstrap, definition.permission)
    : holdsPermissionAnywhere(bootstrap, definition.permission);
}
