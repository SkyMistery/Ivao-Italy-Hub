import { expect, test, type Locator } from '@playwright/test';

import { stubTheApi, stubTheBlockData } from './fixtures';

/**
 * `/calendar` once a browser has laid it out.
 *
 * What the calendar *says* — an instant in UTC and in the division's own time, an all-day entry
 * with no local line — is already checked in `src/shared/ui/CalendarView.test.tsx`, and none of it
 * needed a browser. What needs one is the half jsdom cannot reach: a month grid that is seven
 * columns and not a column of thirty-five. Over there "seven columns" is an assertion about a class
 * name, which is a spelling test (implementation plan M1 §A.9, HANDOFF §13).
 */

const en = (value: string) => ({ en: value, it: value });

/** Three entries in one month, one of them all day. The dates are fixed so nothing drifts. */
const entries = {
  items: [
    {
      id: 1,
      kind: 'meeting',
      title: en('Staff meeting'),
      startsAt: '2026-09-15T14:00:00.000Z',
      allDay: false,
      department: 'ED',
    },
    {
      id: 2,
      kind: 'deadline',
      title: en('Applications close'),
      startsAt: '2026-09-20T00:00:00.000Z',
      allDay: true,
      department: 'ED',
    },
    {
      id: 3,
      kind: 'meeting',
      title: en('Second meeting'),
      startsAt: '2026-09-15T18:00:00.000Z',
      allDay: false,
      department: 'WD',
    },
  ],
};

test.beforeEach(async ({ page }) => {
  await stubTheApi(page);
  await stubTheBlockData(page, 'calendar', entries);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

/** Where a thing actually is, in pixels, once the browser has finished with it. */
async function boxOf(locator: Locator) {
  const box = await locator.boundingBox();
  expect(box, 'the element is not laid out at all').not.toBeNull();
  return box!;
}

test('the month is a grid of seven columns on a desktop and two on a phone', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/calendar?view=month&on=2026-09-15');

  await expect(page.getByRole('heading', { name: 'Calendar', level: 1 })).toBeVisible();

  const squares = page.getByRole('gridcell');

  // September 2026 begins on a Tuesday, so the grid opens on Monday the 31st of August and runs to
  // Sunday the 4th of October: five complete weeks.
  await expect(squares).toHaveCount(35);

  const boxes = await Promise.all(Array.from({ length: 8 }, (_, index) => boxOf(squares.nth(index))));

  // Seven on a line and the eighth underneath. Either half alone passes on a single column: "same
  // line" is true of one square, and "different x" is true of nothing at all.
  for (let column = 1; column < 7; column += 1) {
    expect(boxes[column]!.y).toBeCloseTo(boxes[0]!.y, 0);
    expect(boxes[column]!.x).toBeGreaterThan(boxes[column - 1]!.x);
  }

  expect(boxes[7]!.y).toBeGreaterThan(boxes[0]!.y);
  expect(boxes[7]!.x).toBeCloseTo(boxes[0]!.x, 0);

  // And a square is a square somebody can read an entry in, not a sliver.
  expect(boxes[0]!.width).toBeGreaterThan(80);
  expect(boxes[0]!.height).toBeGreaterThan(80);

  // On a phone seven columns of forty pixels would be a grid nobody can read, so it gives way.
  await page.setViewportSize({ width: 375, height: 900 });
  const narrow = await Promise.all(Array.from({ length: 3 }, (_, index) => boxOf(squares.nth(index))));

  expect(narrow[2]!.y).toBeGreaterThan(narrow[0]!.y);
});

test('an entry is drawn in the square of its day, and both times are on it', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/calendar?view=month&on=2026-09-15');

  const meeting = await boxOf(page.getByText('Staff meeting'));
  const fifteenth = await boxOf(page.getByRole('gridcell').nth(14));

  // The fifteenth of September is the fifteenth square: the grid opened on the 31st of August, so
  // the first of September is the second square. The entry is inside it and not merely on the page.
  expect(meeting.y).toBeGreaterThanOrEqual(fifteenth.y);
  expect(meeting.y).toBeLessThan(fifteenth.y + fifteenth.height);
  expect(meeting.x).toBeGreaterThanOrEqual(fifteenth.x - 1);

  // The fixture's division sits in Europe/Rome, so the two readings of an afternoon differ by two
  // hours. A fixture in UTC would have made them identical and this assertion meaningless — which
  // is exactly how a screen showing UTC twice went unnoticed in M0 (HANDOFF §13).
  await expect(page.getByText(/2:00\sPM UTC/).first()).toBeVisible();
  await expect(page.getByText(/4:00\sPM local/).first()).toBeVisible();
});

test('moving to the next month puts the month in the address', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/calendar?view=month&on=2026-09-15');

  await page.getByRole('button', { name: 'Next' }).click();

  // Where the calendar is looking is part of the address, so a visitor can send it to somebody.
  await expect(page).toHaveURL(/on=2026-10-15/);
});
