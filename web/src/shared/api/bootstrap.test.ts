import { expect, test } from 'vitest';

import { holdsPermission, writableDepartments, type Bootstrap, type BootstrapPermission } from './bootstrap';

/**
 * «Holds this permission on this department, on this row?» answered as the server's `PermissionSet.Has` answers it (issue
 * #319, item 7): the scope of a grant on one row and the FIR of a FIR team narrow it, a permission from outside does not.
 */

const holding = (...permissions: BootstrapPermission[]) =>
  ({ permissions, user: { hasAllDepartments: true, departments: [] } }) as unknown as Bootstrap;

const held = (over: Partial<BootstrapPermission>): BootstrapPermission => ({
  name: 'Sample.Edit',
  department: 'ED',
  resourceScope: null,
  fir: null,
  fromOutside: false,
  ...over,
});

test('a permission on the department reaches it, and every row of it', () => {
  const bootstrap = holding(held({}));

  expect(holdsPermission(bootstrap, 'Sample.Edit', 'ED')).toBe(true);
  expect(
    holdsPermission(bootstrap, 'Sample.Edit', 'ED', { resourceScope: 'sample:item:1', fir: 'XXAA' }),
  ).toBe(true);
  expect(holdsPermission(bootstrap, 'Sample.Edit', 'TD')).toBe(false);
  expect(holdsPermission(bootstrap, 'Sample.View', 'ED')).toBe(false);
});

test('a grant on one row reaches that row and no other, and gives no department', () => {
  const bootstrap = holding(held({ resourceScope: 'sample:item:42' }));

  expect(holdsPermission(bootstrap, 'Sample.Edit', 'ED', { resourceScope: 'sample:item:42' })).toBe(true);
  expect(holdsPermission(bootstrap, 'Sample.Edit', 'ED', { resourceScope: 'sample:item:43' })).toBe(false);
  expect(holdsPermission(bootstrap, 'Sample.Edit', 'ED')).toBe(false);
  expect(writableDepartments(bootstrap, 'Sample.Edit')).toEqual([]);
});

test("a FIR team's permission reaches the rows of its FIR, as IVAO writes them, and no other", () => {
  const bootstrap = holding(held({ fir: 'XXAA' }));

  expect(holdsPermission(bootstrap, 'Sample.Edit', 'ED', { fir: 'XXAA' })).toBe(true);
  expect(holdsPermission(bootstrap, 'Sample.Edit', 'ED', { fir: 'xxaa' })).toBe(true);
  expect(holdsPermission(bootstrap, 'Sample.Edit', 'ED', { fir: 'XXBB' })).toBe(false);
  expect(holdsPermission(bootstrap, 'Sample.Edit', 'ED', { fir: null })).toBe(false);
  expect(holdsPermission(bootstrap, 'Sample.Edit', 'ED')).toBe(false);
});

test('a permission from outside counts on its department, as the handler asks no more', () => {
  const bootstrap = holding(held({ fromOutside: true }));

  expect(holdsPermission(bootstrap, 'Sample.Edit', 'ED')).toBe(true);
  expect(holdsPermission(bootstrap, 'Sample.Edit', 'TD')).toBe(false);
});

test('a global permission is held on every department', () => {
  const bootstrap = holding(held({ department: null }));

  expect(holdsPermission(bootstrap, 'Sample.Edit', 'TD')).toBe(true);
});

test('a permission written without its scope and FIR is held on the department, as one with none', () => {
  const bootstrap = holding({ name: 'Sample.Edit', department: 'ED' } as BootstrapPermission);

  expect(holdsPermission(bootstrap, 'Sample.Edit', 'ED')).toBe(true);
  expect(holdsPermission(bootstrap, 'Sample.Edit', 'ED', { fir: 'XXAA' })).toBe(true);
});
