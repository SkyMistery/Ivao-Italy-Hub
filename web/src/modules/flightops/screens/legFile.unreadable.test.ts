import { expect, test, vi } from 'vitest';

import type * as Sheet from '../../../shared/files/sheet';

import { readLegFile } from './legFile';

/**
 * A sheet read but not made rows — the core's reader throws on it — is a file that cannot be read, one problem, as it was
 * before the reader moved to the core (review of PR 3.12, finding 4).
 */
vi.mock('../../../shared/files/sheet', async (original) => ({
  ...(await original<typeof Sheet>()),
  readWorkbook: () =>
    Promise.resolve({
      sheets: ['legs'],
      cells: () => {
        throw new Error('a sheet SheetJS cannot make rows of');
      },
    }),
}));

test('a sheet that throws while it is made rows is one problem, not an exception', async () => {
  const file = await readLegFile(new Blob(['departure;arrival\r\nEDDF;EDDM\r\n']));

  expect(file.rows).toEqual([]);
  expect(file.problems).toEqual([{ line: null, key: 'unreadable' }]);
});
