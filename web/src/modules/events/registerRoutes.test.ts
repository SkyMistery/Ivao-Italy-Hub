import { expect, test } from 'vitest';

import { registry } from '../../app/registry';
import { opensStaffRoute } from '../../app/staffRoute';
import type { Bootstrap, BootstrapPermission } from '../../shared/api/bootstrap';

/**
 * The registers of the events count the whole division (E13b): their lists answer only whoever holds their permission without a row scope
 * (`HasUnscoped`), and their menu entries say so (`Unscoped: true`). Their routes say it too (#427, `unscoped`), so a grant on one event
 * does not lead to a page that answers 403. Read from the registry, the routes the router mounts.
 */

const REGISTERS = [
  '/staff/events/controllers',
  '/staff/events/controllers/$vid',
  '/staff/events/pilots',
  '/staff/events/pilots/$vid',
];

const registers = registry.routes.filter((route) => REGISTERS.includes(route.path));

// Only the permissions are read: the rest of the bootstrap is no business of the guard.
const holding = (...permissions: BootstrapPermission[]) => ({ permissions }) as Bootstrap;

const held = (name: string, resourceScope: string | null = null): BootstrapPermission => ({
  name,
  department: 'ED',
  resourceScope,
});

test('the four registers open to no grant on one event', () => {
  expect(registers.map((route) => route.path)).toEqual(REGISTERS);

  const oneEvent = holding(
    held('EventAtc.View', 'events:event:42'),
    held('EventBookings.View', 'events:event:42'),
  );
  for (const route of registers) {
    expect(opensStaffRoute(oneEvent, route)).toBe(false);
  }
});

test('they open to their permission held without a row scope', () => {
  const unscoped = holding(held('EventAtc.View'), held('EventBookings.View'));
  for (const route of registers) {
    expect(opensStaffRoute(unscoped, route)).toBe(true);
  }
});

test('the page of an event still opens to a grant on that event', () => {
  const page = registry.routes.find((route) => route.path === '/staff/events/$id');

  expect(page).toBeDefined();
  expect(opensStaffRoute(holding(held('Events.View', 'events:event:42')), page!)).toBe(true);
});
