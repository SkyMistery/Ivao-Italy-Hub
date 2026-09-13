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

test('the content of every department is one group, and a department opens the same screens filtered', () => {
  // Note 2026-09-13-contenuti-centralizzati: one screen per object, not one per department. The
  // first group opens them on every department; under a department they carry its code.
  const groups = staffDestinations(
    {
      ...bootstrap,
      permissions: [
        { name: 'Content.View', department: 'ED' },
        { name: 'Content.View', department: 'WD' },
        { name: 'Content.ManageTemplates', department: 'WD' },
        { name: 'Links.View', department: 'ED' },
        { name: 'Media.View', department: 'ED' },
      ],
    },
    (key) => key,
  );

  const content = groups.find((group) => group.title === 'backOffice.content');
  expect(content?.items.map((item) => item.href)).toEqual([
    '/staff/content?kind=Page',
    '/staff/content?kind=News',
    '/staff/content?kind=Document',
    '/staff/content?kind=Template',
    '/staff/links',
    '/staff/media',
  ]);

  const events = groups.find((group) => group.code === 'ED');
  const hrefs = events?.items.map((item) => item.href) ?? [];
  expect(hrefs).toContain('/staff/content?kind=News&department=ED');
  expect(hrefs).toContain('/staff/links?department=ED');
  // Templates only where they may be changed: Web yes, Events no.
  expect(hrefs).not.toContain('/staff/content?kind=Template&department=ED');
  expect(groups.find((group) => group.code === 'WD')?.items.map((item) => item.href)).toContain(
    '/staff/content?kind=Template&department=WD',
  );
  // Nothing of the old addresses is left.
  expect(hrefs.some((href) => /\/staff\/ed\/(content|news|documents|templates|links|media)/.test(href))).toBe(
    false,
  );
});

test('somebody who works in one department is not offered the same entries twice', () => {
  const groups = staffDestinations(
    {
      ...bootstrap,
      user: { ...bootstrap.user, departments: ['ED'] },
      permissions: [{ name: 'Content.View', department: 'ED' }],
    },
    (key) => key,
  );

  expect(groups.find((group) => group.title === 'backOffice.content')).toBeUndefined();
  expect(groups.find((group) => group.code === 'ED')?.items.map((item) => item.href)).toContain(
    '/staff/content?kind=Page&department=ED',
  );
});
