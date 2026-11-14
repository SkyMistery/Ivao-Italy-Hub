import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';

import type * as Sheet from '../../shared/files/sheet';

import englishCommon from '../../../../locales/en/common.json';
import { renderWithProviders } from '../../test/harness';

import { TableTools } from './TableTools';

/**
 * A file read but not made a table — the reader throws on one of its sheets — is a file that cannot be read, said on
 * screen, not a promise that rejects with nothing on it (review of PR 3.12, finding 4).
 */
vi.mock('../../shared/files/sheet', async (original) => ({
  ...(await original<typeof Sheet>()),
  readWorkbook: () =>
    Promise.resolve({
      sheets: ['Hours'],
      cells: () => {
        throw new Error('a sheet SheetJS cannot make rows of');
      },
    }),
}));

test('a sheet that throws while it is read is said as a file that cannot be read, and changes nothing', async () => {
  const user = userEvent.setup();
  const onApply = vi.fn();
  renderWithProviders(<TableTools props={{}} locales={['it', 'en']} onApply={onApply} />);

  await user.upload(
    screen.getByLabelText(englishCommon.blocks.table.tools.fromFile),
    new File(['Day;From\r\n'], 'hours.csv', { type: 'text/csv' }),
  );

  expect(await screen.findByRole('alert')).toHaveTextContent(englishCommon.blocks.table.tools.unreadable);
  expect(onApply).not.toHaveBeenCalled();
});
