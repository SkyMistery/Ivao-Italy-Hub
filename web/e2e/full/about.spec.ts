import { expect, test } from '@playwright/test';

import { englishCommon, englishSeed } from '../locales';

import { readInEnglish, signIn } from './bench';

/**
 * The staff directory, against the real API (design M1 §6.1).
 *
 * It is worth a test on the bench and not only in a unit, because the three halves that make it up
 * were built in three different phases and have never met: the **provider** is G4's, the **page**
 * is a row G8 seeded and published, and the **block** on it is what the editor would have placed.
 * Any one of them can be right while the page shows nothing.
 *
 * The visitor here is nobody: the directory is public, and what it may show a stranger is the whole
 * question — a name, a position, and the link to the official IVAO profile.
 */

test('the seeded about page shows the staff of the division, to a visitor who is nobody', async ({
  browser,
}) => {
  // ⚠️ Somebody has to have signed in, because that is what a roster read from logins *is* — and it
  // is done here rather than left to another spec having run first. The first version of this test
  // did leave it to that: it passed on a development bench, whose database keeps every earlier run,
  // and failed in CI on a fresh one. A test that needs state has to make it.
  const staff = await browser.newContext();
  await signIn(staff);
  await staff.close();

  const visitor = await browser.newContext();
  await readInEnglish(visitor);
  const page = await visitor.newPage();

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await page.goto('/about');

  // The page itself: the words of the seed, resolved into the language of this reader. The heading
  // asserted is the **visible** one — the block the seed put at the top — and not the title of the
  // row, which is drawn for screen readers only and would match half of them.
  const heading = englishSeed.seed.pages.about?.intro?.heading ?? '';
  expect(heading, 'the about seed has no heading to look for').not.toBe('');
  await expect(page.getByRole('heading', { name: heading, exact: true })).toBeVisible();

  // The bench signs in as a coordinator of the web department, so that member is on the roster —
  // which is what "whoever has signed in at least once" means, seen from outside. The group is
  // headed by the **name** of the department and not by its code: a reader of a public page is not
  // expected to know that WD is the web team.
  await expect(page.getByRole('heading', { name: englishCommon.departments.WD })).toBeVisible();
  // Matched by where it leads and not by whose name it carries: the promise of §6.1 is that a
  // member of staff is a name and a link to their **official IVAO profile**, and the name on the
  // bench is a setting. (It also stops this assertion catching a menu entry a failed run left
  // behind, which is what a looser locator did.)
  const profiles = page.locator('a[href*="ivao.aero/Member.aspx"]');
  await expect(profiles.first()).toBeVisible();
  await expect(profiles.first()).toHaveAttribute('href', /Id=\d+/);
  await expect(profiles.first()).not.toBeEmpty();

  // And the line that says who is *not* on it, which is the one thing a roster read from logins
  // owes its reader.
  await expect(page.getByText(englishCommon.blocks.staffList.rosterNote)).toBeVisible();

  // ⚠️ No contact data, asked of the page rather than of the payload: the provider is tested for it
  // in `StaffDirectoryExposesNoContactData`, and this is the other end of the same promise. Scoped
  // to the article, because the footer of every page carries the legal links of headquarters.
  await expect(page.getByRole('article').getByText('@', { exact: false })).toHaveCount(0);

  await visitor.close();
});
