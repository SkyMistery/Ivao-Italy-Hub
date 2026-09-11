import { render, screen } from '@testing-library/react';
import { expect, test } from 'vitest';

import type { Bootstrap } from '../../shared/api/bootstrap';

import { staffDestinations } from './staffDestinations';

/**
 * Two departments are told apart by their mark, and the mark is the code.
 *
 * ⚠️ They were not. All nine carried the same `ShieldCheck`, so the back office drew nine identical
 * shields and only the words beside them said which was which — an icon that carries no information
 * is an icon that costs a reader time. Decided after the demo of M1: no icon for a department, the
 * code is the sign (`decisions/2026-09-07-dopo-la-demo.md`).
 *
 * The test mounts what the sidebar mounts rather than comparing two component references, because
 * "these are different" is not the property that matters: what matters is that the code is what
 * ends up on screen.
 */

const bootstrap = {
  user: {
    vid: 704798,
    firstName: 'Test',
    lastName: 'User',
    positions: [],
    isStaff: true,
    isSuperadmin: false,
    hasAllDepartments: false,
    locale: 'en',
    departments: ['WD', 'ED'],
    firs: [],
  },
  permissions: [],
  division: {
    code: 'XX',
    name: { en: 'IVAO Example' },
    locales: ['en'],
    defaultLocale: 'en',
    timezone: 'UTC',
    logoUrl: null,
    faviconUrl: null,
    firStaffScope: 'all',
    siteDepartment: 'WD',
  },
  modules: [],
  navigation: { public: [], footer: [], staff: [] },
  registries: { blocks: [], widgets: [], permissions: [] },
  calendarKinds: [],
  version: '0.0.0-test',
} satisfies Bootstrap;

test('a department is marked by its own code and not by an icon every one of them shares', () => {
  const groups = staffDestinations(bootstrap, (key) => key);

  // By the code, which is what the square draws; the heading beside it says the department's name
  // since 11 September 2026.
  const departments = groups.filter((group) => ['WD', 'ED'].includes(group.code ?? ''));
  expect(departments).toHaveLength(2);

  for (const group of departments) {
    const { unmount } = render(<group.Icon />);
    expect(screen.getByText(group.code!)).toBeInTheDocument();
    expect(group.title).toBe(`departments.${group.code}`);
    unmount();
  }
});
