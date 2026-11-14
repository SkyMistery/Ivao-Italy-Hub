import { expect, test } from '@playwright/test';

import { stubTheBlockData, stubThePublishedPage } from './fixtures';

/**
 * PR 3.13 of the editor's phase 3, on the published page: questions and answers with a title, numbers and the first
 * answer open (§5.4), and a figure under a word of the editor's own (§5.7). `src/blocks/questions.test.tsx` says the
 * rest; this is the half that needs a browser — the page as a visitor gets it, opened and clicked.
 */

const SLUG = 'questions-demo';

const en = (value: string) => ({ en: value, it: value });

const body = {
  schemaVersion: 1,
  sections: [
    {
      id: 's_1',
      layout: 'stacked',
      background: 'none',
      padding: 'md',
      width: 'default',
      blocks: [
        {
          id: 'b_questions',
          type: 'accordion',
          version: 1,
          props: {
            title: en('Before you connect'),
            numbered: true,
            initially: 'first',
            allowMultiple: false,
            items: [
              {
                question: en('When?'),
                answer: en('At the top of the hour.\n\n- with a flight plan\n- with a squawk'),
              },
              { question: en('Where?'), answer: en('On the ground frequency.') },
            ],
          },
        },
        {
          id: 'b_stats',
          type: 'stats',
          version: 1,
          props: {
            columns: 2,
            metrics: [
              { metric: 'knownMembers', label: en('pilots and controllers') },
              { metric: 'staffMembers' },
            ],
          },
        },
      ],
      sections: [],
    },
  ],
};

test.beforeEach(async ({ page }) => {
  await stubThePublishedPage(page, SLUG, body);
  await stubTheBlockData(page, 'stats', {
    metrics: [
      { metric: 'knownMembers', value: 1284 },
      { metric: 'staffMembers', value: 37 },
    ],
  });

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('the questions stand under their title, numbered, with the first answer open', async ({ page }) => {
  await page.goto(`/${SLUG}`);

  await expect(page.getByRole('heading', { level: 2, name: 'Before you connect' })).toBeVisible();

  const first = page.getByRole('button', { name: '1. When?' });
  const second = page.getByRole('button', { name: '2. Where?' });

  await expect(first).toHaveAttribute('aria-expanded', 'true');
  await expect(second).toHaveAttribute('aria-expanded', 'false');
  // One answer, several paragraphs and a list: the Markdown of the answer is drawn, not printed.
  await expect(page.getByText('At the top of the hour.')).toBeVisible();
  await expect(page.getByRole('listitem').filter({ hasText: 'with a squawk' })).toBeVisible();

  // One at a time, as `allowMultiple` says: opening the second closes the first.
  await second.click();

  await expect(page.getByText('On the ground frequency.')).toBeVisible();
  await expect(first).toHaveAttribute('aria-expanded', 'false');
});

test('a figure carries the word the editor wrote, and the one without keeps the hub’s', async ({ page }) => {
  await page.goto(`/${SLUG}`);

  await expect(page.getByText('1,284')).toBeVisible();
  await expect(page.getByText('pilots and controllers', { exact: true })).toBeVisible();
  await expect(page.getByText('staff', { exact: true })).toBeVisible();
  await expect(page.getByText('members', { exact: true })).toHaveCount(0);
});
