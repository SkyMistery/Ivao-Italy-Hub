import { readFileSync } from 'node:fs';

import { expect, test, type Page } from '@playwright/test';

import { staffBootstrap, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/** The words of the grants screen, read from the English language file as `./locales` reads the rest of it. */
interface GrantStrings {
  readonly fields: { readonly positionFirTeam: string; readonly value: string };
  readonly hints: { readonly positionLevels: string };
  readonly options: { readonly positionLevels: { readonly Coordinator: string } };
}

const grants = (
  JSON.parse(readFileSync(new URL('../../locales/en/common.json', import.meta.url), 'utf8')) as {
    grants: GrantStrings;
  }
).grants;

/**
 * The form of a grant offers its third subject, the team of a FIR (M3, A11a, note 2026-09-27-i-capi-fir-sul-loro-fir): a switch
 * next to the department of a position, the same levels, and what leaves the browser is the team and its levels with no member
 * and no department. Whether that permission may go to a FIR team at all is the server's to answer.
 */
const administrator = {
  ...staffBootstrap,
  permissions: [...staffBootstrap.permissions, { name: 'Permissions.Manage', department: null }],
  registries: {
    blocks: [],
    permissions: [
      { name: 'Links.View', isGlobal: false },
      { name: 'Permissions.Manage', isGlobal: true },
    ],
  },
};

/** Picks a value in one of Atmosphere's selects, a button and a list in a portal rather than a `<select>`. */
async function choose(page: Page, label: string, option: string): Promise<void> {
  await page.getByText(label, { exact: true }).locator('..').getByRole('combobox').click();
  await page.getByRole('option', { name: option, exact: true }).click();
}

test('the form of a grant offers the team of a FIR, and sends it with its levels', async ({ page }) => {
  await stubTheApiAsStaff(page, administrator);

  // The list the form goes back to once saved: empty, as a division with no grant by hand has it.
  await page.route('**/api/admin/grants?**', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items: [], page: 1, pageSize: 25, total: 0 }),
    }),
  );

  // The write, answered with the grant as the server would store it.
  await page.route('**/api/admin/grants', (route) => {
    const sent = JSON.parse(route.request().postData() ?? '{}') as Record<string, unknown>;
    return route.fulfill({
      status: 201,
      contentType: 'application/json',
      body: JSON.stringify({
        ...sent,
        id: 7,
        resourceScope: null,
        suspendedAt: null,
        createdAt: '2026-09-28T00:00:00Z',
        createdBy: 111111,
        updatedAt: '2026-09-28T00:00:00Z',
        updatedBy: 111111,
        rowVersion: '2026-09-28T00:00:00Z',
      }),
    });
  });

  await page.goto('/staff/admin/permissions/new');

  const team = page.getByRole('switch', { name: grants.fields.positionFirTeam });
  await expect(team).not.toBeChecked();
  await expect(page.getByText(grants.hints.positionLevels)).toBeVisible();

  const written = page.waitForRequest(
    (request) => request.url().endsWith('/api/admin/grants') && request.method() === 'POST',
  );

  await team.click();
  await page.getByRole('checkbox', { name: grants.options.positionLevels.Coordinator, exact: true }).click();
  await choose(page, grants.fields.value, 'Links.View');
  await page.getByRole('button', { name: englishCommon.common.save, exact: true }).click();

  expect(JSON.parse((await written).postData() ?? '{}')).toMatchObject({
    vid: null,
    positionDepartment: null,
    positionFirTeam: true,
    positionLevels: ['Coordinator'],
    value: 'Links.View',
  });

  // Saved, the form goes back to the list, which adds its paging to the address.
  await expect(page).toHaveURL(/\/staff\/admin\/permissions(\?|$)/);
});
