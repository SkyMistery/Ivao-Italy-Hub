import { expect, test, type Page } from '@playwright/test';

import { oneTemplate, staffBootstrap, stubTheAddressOfAPage, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * The approval of a page in a browser (G19, note 2026-09-13-contenuti-centralizzati §3.2).
 *
 * What the server decides is proven by `PageReviewTests`. What only a browser shows is the screen
 * following it: a coordinator is offered "mark ready" and not "publish", a page waiting cannot be
 * saved, and whoever may approve reads what changed and corrects the address in the same dialog
 * that publishes it.
 */

const review = englishCommon.content.review;

/** A page of events, written by the coordinator of the fixture. */
const aPage = {
  ...oneTemplate,
  id: 20,
  slug: 'airshow',
  path: 'about/airshow',
  parentId: 1,
  ownerDepartment: 'ED',
  isTemplate: false,
  reviewNote: null,
  title: { en: 'Airshow', it: 'Airshow' },
};

/** The same coordinator, granted the approval of pages of their department. */
const approverBootstrap = {
  ...staffBootstrap,
  permissions: [...staffBootstrap.permissions, { name: 'Content.Approve', department: 'ED' }],
};

/** The row as the editor loads it, and every review sent, answered with the row that results. */
async function stubThePage(page: Page, row: Record<string, unknown>, sent: unknown[]): Promise<void> {
  let current = row;

  await page.route('**/api/content/20', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(current) }),
  );

  await page.route('**/api/content/20/review', async (route) => {
    const request = route.request();

    if (request.method() === 'POST') {
      const body = JSON.parse(request.postData() ?? '{}') as { action: string };
      sent.push(body);
      current = {
        ...current,
        status: body.action === 'Ready' ? 'Ready' : body.action === 'Approve' ? 'Published' : 'Draft',
      };
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(current) });
    }

    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        firstPublication: false,
        titleChanged: false,
        path: '/about/airshow',
        sections: [
          { key: 'hero', title: { en: 'Hero', it: 'Hero' }, change: 'Changed' },
          { key: 'closing', title: { en: 'Closing', it: 'Chiusura' }, change: 'Unchanged' },
        ],
        menu: null,
        note: 'Ready for the weekend',
        readyAt: '2026-09-13T10:00:00Z',
        readyByName: 'Test Coordinator',
        blocks: { added: 0, removed: 0, changed: 1 },
        fields: [],
      }),
    });
  });

  // After the single row, which would otherwise answer the tree of pages with a row.
  await stubTheAddressOfAPage(page);
}

test.beforeEach(({ page }) => {
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('a coordinator marks a page ready instead of publishing it, and a page waiting cannot be saved', async ({
  page,
}) => {
  const sent: unknown[] = [];
  await stubTheApiAsStaff(page);
  await stubThePage(page, aPage, sent);

  await page.goto('/staff/content/20');

  await expect(page.getByRole('button', { name: review.ready })).toBeVisible();
  await expect(
    page.getByRole('button', { name: englishCommon.content.editor.publish, exact: true }),
  ).toHaveCount(0);

  await page.getByRole('button', { name: review.ready }).click();
  const dialog = page.getByRole('alertdialog');
  await dialog.getByLabel(review.note).fill('Ready for the weekend');
  await dialog.getByRole('button', { name: review.readyDialog.confirm }).click();

  await expect.poll(() => sent).toEqual([{ action: 'Ready', note: 'Ready for the weekend' }]);

  // Waiting now: withdrawn by its author, and not written by anybody.
  await expect(page.getByRole('button', { name: review.withdraw })).toBeVisible();
  await expect(page.getByRole('button', { name: englishCommon.content.editor.saveDraft })).toBeDisabled();
});

test('a page waiting for approval is not changed by a key either, so nothing is left to save after', async ({
  page,
}) => {
  const sent: unknown[] = [];
  await stubTheApiAsStaff(page, approverBootstrap);
  await stubThePage(
    page,
    {
      ...aPage,
      status: 'Ready',
      body: {
        schemaVersion: 1,
        sections: [
          {
            id: 's_only',
            layout: 'stacked',
            background: 'none',
            padding: 'md',
            width: 'default',
            blocks: [
              {
                id: 'b_title',
                type: 'heading',
                version: 1,
                props: { level: 2, text: { en: 'Waiting heading', it: 'Titolo in attesa' } },
              },
            ],
            sections: [],
          },
        ],
      },
    },
    sent,
  );

  await page.goto('/staff/content/20');

  const editor = englishCommon.content.editor;
  const onThePage = page.getByRole('region', { name: editor.preview });
  const heading = onThePage.getByRole('heading', { name: 'Waiting heading' });

  // Picked, which only shows it: then Delete, Ctrl+D and Ctrl+Z, the keys that reach past the
  // fieldset that switches every button off (issue #332, item 7).
  // Asserted after each key: ⌘Z after Delete would put back what Delete took, and hide it.
  const draft = page.getByRole('status', { name: editor.autosave.title });
  await heading.click();

  await page.keyboard.press('Delete');
  await expect(heading).toHaveCount(1);
  // Nothing became a draft to store, so nothing is stored by itself once the page is approved and
  // the editor opens again.
  await expect(draft).toHaveCount(0);

  await page.keyboard.press('ControlOrMeta+d');
  await expect(heading).toHaveCount(1);
  await expect(draft).toHaveCount(0);

  await page.keyboard.press('ControlOrMeta+z');
  await expect(heading).toHaveCount(1);
  await expect(page.getByRole('button', { name: editor.undo })).toBeDisabled();
  await expect(draft).toHaveCount(0);
});

test('whoever may approve reads what changed and corrects the address while approving', async ({ page }) => {
  const sent: unknown[] = [];
  await stubTheApiAsStaff(page, approverBootstrap);
  await stubThePage(page, { ...aPage, status: 'Ready', reviewNote: 'Ready for the weekend' }, sent);

  await page.goto('/staff/content/20');

  const summary = page.getByRole('list', { name: review.sections });
  await expect(summary.getByRole('listitem').filter({ hasText: 'Hero' })).toContainText(
    review.change.Changed,
  );
  await expect(summary.getByRole('listitem').filter({ hasText: 'Closing' })).toContainText(
    review.change.Unchanged,
  );

  // Published by being approved, not past its review.
  await expect(
    page.getByRole('button', { name: englishCommon.content.editor.publish, exact: true }),
  ).toHaveCount(0);

  await page.getByRole('button', { name: review.approve, exact: true }).click();
  const dialog = page.getByRole('alertdialog');
  // The line starts from the summary above, in words (#352), and goes with the approval as it stands.
  const changelog = dialog.getByLabel(englishCommon.content.editor.publishDialog.changelog);
  await expect(changelog).toHaveValue(/Hero/);
  const line = await changelog.inputValue();
  await dialog.getByLabel(englishCommon.content.fields.slug, { exact: true }).fill('airshow-2026');
  await dialog.getByRole('button', { name: review.approveDialog.confirm }).click();

  await expect
    .poll(() => sent)
    .toEqual([{ action: 'Approve', changelog: line, slug: 'airshow-2026', parentId: 1 }]);
});
