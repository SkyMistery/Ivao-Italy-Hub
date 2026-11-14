import { expect, test, vi } from 'vitest';

import { readWorkbook } from './sheet';

/**
 * The library of sheets is a chunk of its own, loaded when a file is read; after a deploy the page may ask for a chunk
 * that is no longer there. That is a file that cannot be read, said as such, not a promise that rejects with nothing on
 * screen (review of PR 3.12, finding 4).
 */
vi.mock('xlsx', () => {
  throw new Error('Failed to fetch dynamically imported module');
});

test('a library of sheets that does not load is a file that cannot be read, not an exception', async () => {
  await expect(readWorkbook(new Blob(['Day;From\r\nMonday;18:00\r\n']))).resolves.toBeNull();
});
