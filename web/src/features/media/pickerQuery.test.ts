import { QueryClient } from '@tanstack/react-query';
import { beforeEach, expect, test, vi } from 'vitest';

import { narrowLibrary, picturesOnly } from '../../shared/ui';

import { mediaPickerQuery } from './queries';

/**
 * What the library of a picture field asks the server (#488, note 2026-11-15-il-selettore-delle-foto): the files whose
 * type is a picture, kept when the field also asks for one category, with no category "Document" to choose and only
 * pictures to upload.
 */

const api = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock('../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../shared/api/client')),
  api: { GET: api.get },
}));

beforeEach(() => {
  api.get.mockReset();
  api.get.mockResolvedValue({ data: { items: [], total: 0 } });
});

/** The query string the library sent, as the server reads it. */
async function asked(query: ReturnType<typeof mediaPickerQuery>): Promise<URLSearchParams> {
  await new QueryClient().fetchQuery(query);
  const options = api.get.mock.calls.at(-1)?.[1] as {
    querySerializer: (query: Record<string, unknown>) => string;
  };
  return new URLSearchParams(options.querySerializer({ page: 1 }));
}

test('the whole library asks for every file, and offers every category', async () => {
  const library = mediaPickerQuery('WD');

  expect((await asked(library)).get('filter[picture]')).toBeNull();
  expect(library.meta?.categories).toContain('Document');
  expect(library.meta?.accept).toBeUndefined();
});

test('the pictures ask for pictures, offer no document category, and upload only pictures', async () => {
  const pictures = picturesOnly(mediaPickerQuery('WD'));

  expect((await asked(pictures)).get('filter[picture]')).toBe('true');
  expect(pictures.meta?.categories).not.toContain('Document');
  expect(pictures.meta?.categories).toContain('Logo');
  expect(pictures.meta?.accept).toBe('image/*');
});

test('pictures of one category are still pictures, and what the person browses stays pictures', async () => {
  const logos = narrowLibrary(picturesOnly(mediaPickerQuery('WD')), 'Logo');

  const sent = await asked(logos);
  expect(sent.get('filter[picture]')).toBe('true');
  expect(sent.get('filter[category]')).toBe('Logo');

  const browse = picturesOnly(mediaPickerQuery('WD')).meta?.browse as (asked: unknown) => typeof logos;
  const searched = await asked(browse({ q: 'hangar', category: 'Banner', page: 2 }));
  expect(searched.get('filter[picture]')).toBe('true');
  expect(searched.get('filter[category]')).toBe('Banner');
});
