import { expect, test, type Page } from '@playwright/test';

import { oneTemplate, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * The frequency table offers the ATC positions of the division, copies the one chosen into its row, and says which row
 * IVAO now describes otherwise, with «Realign» (design of the editor, phase 3, §5.8; Carmine's answer 6 of 31 October
 * 2026). `src/features/content/atcPositions.test.ts` and `src/shared/forms/SchemaForm.fills.test.tsx` say the rest; this
 * is the half that needs a browser — the list opened and clicked, and the panel drawn again after «Realign».
 */

const editor = englishCommon.content.editor;
const table = englishCommon.blocks.frequencyTable;
const words = englishCommon.atcPositions;

const positions = [
  {
    callsign: 'XXXX_TWR',
    type: 'TWR',
    name: 'Example Tower',
    airportIcao: 'XXXX',
    fir: 'XXXF',
    frequency: '118.700',
  },
  {
    callsign: 'XXXF_CTR',
    type: 'CTR',
    name: 'Example Control',
    airportIcao: null,
    fir: 'XXXF',
    frequency: '124.750',
  },
];

async function stubThePositions(page: Page, answer: unknown = positions) {
  await page.route('**/api/atc/positions**', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(answer) }),
  );
}

test.beforeEach(async ({ page }) => {
  await stubTheApiAsStaff(page);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('a position chosen by its callsign copies its name, kind and frequency into the row', async ({
  page,
}) => {
  await stubThePositions(page);

  await page.goto('/staff/content/1');
  await page.getByRole('button', { name: editor.outline }).click();
  await page.getByRole('button', { name: editor.addSection }).first().click();
  await page.getByRole('searchbox', { name: editor.searchComponents }).fill(table.label);
  await page.getByRole('button', { name: table.label, exact: true }).click();
  await page.getByRole('button', { name: englishCommon.form.addEntry, exact: true }).click();

  const callsign = page.getByRole('combobox', { name: table.fields['stations.callsign'] });
  await callsign.click();
  // Part of it typed, as somebody searching does: the choice is the whole callsign, not what was typed with it after.
  await callsign.pressSequentially('XXXX_T');

  // Grouped by FIR, each with its name and frequency.
  await expect(page.getByText('XXXF', { exact: true })).toBeVisible();
  await page.getByRole('option', { name: /Example Tower · 118\.700/ }).click();

  await expect(callsign).toHaveValue('XXXX_TWR');
  // And the list is closed: copying the row beside it does not open it again.
  await expect(page.getByRole('option')).toHaveCount(0);
  await expect(page.getByLabel(table.fields['stations.name'], { exact: true })).toHaveValue('Example Tower');
  await expect(page.getByLabel(table.fields['stations.frequency'], { exact: true })).toHaveValue('118.700');

  // Nothing to realign: the row is what IVAO says.
  await expect(page.getByRole('button', { name: words.realign })).toHaveCount(0);

  // A frequency written over it by hand is a row IVAO now says otherwise, and «Realign» puts IVAO's back.
  const frequency = page.getByLabel(table.fields['stations.frequency'], { exact: true });
  await frequency.fill('118.705');
  await expect(page.getByRole('button', { name: words.realign })).toHaveCount(1);
  await page.getByRole('button', { name: words.realign }).click();
  await expect(frequency).toHaveValue('118.700');
  await expect(page.getByRole('button', { name: words.realign })).toHaveCount(0);

  // On the page, with the name's column, because a row has one.
  await page.getByRole('button', { name: editor.onThePage, exact: true }).click();
  const drawn = page.getByRole('region', { name: editor.preview }).getByRole('table');
  await expect(drawn.getByRole('columnheader', { name: table.columns.name })).toBeVisible();
  await expect(drawn.getByRole('cell', { name: 'Example Tower' })).toBeVisible();
  await expect(drawn.getByRole('cell', { name: '118.700' })).toBeVisible();
});

test('a row IVAO now describes otherwise is flagged, and «Realign» copies it again', async ({ page }) => {
  await stubThePositions(page);
  await page.route('**/api/content/1', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        ...oneTemplate,
        body: {
          schemaVersion: 1,
          sections: [
            {
              id: 'sec-sop',
              layout: 'stacked',
              background: 'none',
              padding: 'md',
              width: 'default',
              blocks: [
                {
                  id: 'blk-frequencies',
                  type: 'frequencyTable',
                  version: 1,
                  props: {
                    stations: [
                      // Written before PR 3.14, with no name: the same as IVAO.
                      { callsign: 'XXXX_TWR', frequency: '118.7', kind: 'TWR', cpdlc: false },
                      // A frequency IVAO has since changed, and a note of the writer's own.
                      {
                        callsign: 'XXXF_CTR',
                        frequency: '125.000',
                        kind: 'CTR',
                        cpdlc: true,
                        note: { en: 'Nights only', it: 'Solo di notte' },
                      },
                    ],
                  },
                },
              ],
              sections: [],
            },
          ],
        },
      }),
    }),
  );

  await page.goto('/staff/content/1');
  await page.getByRole('button', { name: editor.outline }).click();
  // The block in the outline, not the palette's button, which a page full of blocks still lists.
  await page.getByRole('button', { name: table.label, exact: true, disabled: false }).click();

  // Only the second row, with what differs.
  const realign = page.getByRole('button', { name: words.realign });
  await expect(realign).toHaveCount(1);
  await expect(page.getByText('Row 2, XXXF_CTR: IVAO now says otherwise')).toBeVisible();
  await expect(page.getByText('Frequency: 125.000 here, 124.750 on IVAO')).toBeVisible();

  await realign.click();

  await expect(realign).toHaveCount(0);
  await page.getByRole('button', { name: editor.onThePage, exact: true }).click();
  const drawn = page.getByRole('region', { name: editor.preview }).getByRole('table');
  await expect(drawn.getByRole('cell', { name: '124.750' })).toBeVisible();
  await expect(drawn.getByRole('cell', { name: 'Example Control' })).toBeVisible();
  // The rest of the row is the writer's, and stays.
  await expect(drawn.getByRole('cell', { name: 'Nights only' })).toBeVisible();
});

test('the block says when IVAO has not given the frequencies yet', async ({ page }) => {
  await stubThePositions(
    page,
    positions.map((position) => ({ ...position, frequency: null })),
  );

  await page.goto('/staff/content/1');
  await page.getByRole('button', { name: editor.outline }).click();
  await page.getByRole('button', { name: editor.addSection }).first().click();
  await page.getByRole('searchbox', { name: editor.searchComponents }).fill(table.label);
  await page.getByRole('button', { name: table.label, exact: true }).click();

  await expect(page.getByText(words.pending)).toBeVisible();
});
