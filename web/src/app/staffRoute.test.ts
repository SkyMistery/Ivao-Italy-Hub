import { expect, test } from 'vitest';

import type { Bootstrap, BootstrapPermission } from '../shared/api/bootstrap';
import type { RouteDefinition } from '../shared/modules';

import { opensStaffRoute } from './staffRoute';

/**
 * A module's route of the back office opens as the server's list behind it does (#427): `permission` held anywhere, and with
 * `unscoped` held without a row scope — so that a grant on one event does not lead to a register that answers 403 (note
 * 2026-10-17-il-permesso-senza-ambito, `HasUnscoped`).
 */

const route = (definition: Partial<RouteDefinition>): RouteDefinition => ({
  path: '/staff/sample/register',
  component: () => null,
  area: 'staff',
  ...definition,
});

// Only the permissions are read: the rest of the bootstrap is no business of the guard.
const holding = (...permissions: BootstrapPermission[]) => ({ permissions }) as Bootstrap;

const onOneRow = { name: 'Sample.View', department: 'ED', resourceScope: 'sample:item:42' };
const onTheDepartment = { name: 'Sample.View', department: 'ED', resourceScope: null };
const everywhere = { name: 'Sample.View', department: null, resourceScope: null };

test('a route without unscoped opens to the permission held anywhere, a grant on one row included', () => {
  const definition = route({ permission: 'Sample.View' });

  expect(opensStaffRoute(holding(onOneRow), definition)).toBe(true);
  expect(opensStaffRoute(holding(onTheDepartment), definition)).toBe(true);
  expect(opensStaffRoute(holding(), definition)).toBe(false);
});

test('an unscoped route opens to the permission held without a row scope, and not to a grant on one row', () => {
  const definition = route({ permission: 'Sample.View', unscoped: true });

  expect(opensStaffRoute(holding(onOneRow), definition)).toBe(false);
  expect(opensStaffRoute(holding(onTheDepartment), definition)).toBe(true);
  expect(opensStaffRoute(holding(everywhere), definition)).toBe(true);
  // A grant on one row next to the same permission held on the department: the department opens it.
  expect(opensStaffRoute(holding(onOneRow, onTheDepartment), definition)).toBe(true);
  // Another permission held without a scope opens nothing.
  expect(opensStaffRoute(holding({ ...everywhere, name: 'Sample.Edit' }), definition)).toBe(false);
});

test('a route that names no permission opens to the staff, unscoped or not', () => {
  expect(opensStaffRoute(holding(), route({}))).toBe(true);
  expect(opensStaffRoute(holding(), route({ unscoped: true }))).toBe(true);
});
