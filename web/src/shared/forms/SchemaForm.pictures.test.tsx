import { queryOptions } from '@tanstack/react-query';
import { screen } from '@testing-library/react';
import { expect, test, vi } from 'vitest';
import { z } from 'zod';

import { createTestI18n, renderWithProviders } from '../../test/harness';
import type { MediaLibraryQuery, PickableMedia } from '../ui';

import { SchemaForm } from './SchemaForm';

/**
 * A field that draws a picture is offered pictures, and the file of a document is offered every file (#488, note
 * 2026-11-15-il-selettore-delle-foto). The library narrows itself through what its query carries (`picturesOnly`), as it
 * does for one category; the generator only says which.
 */

const DIVISION = { defaultLocale: 'en', timezone: 'Europe/Rome' };

const PICTURE: PickableMedia = {
  id: 42,
  fileName: 'hangar.png',
  contentType: 'image/png',
  alt: { en: 'An open hangar', it: 'Un hangar aperto' },
  url: '/media/42/hangar.png',
};

const DOCUMENT: PickableMedia = {
  id: 43,
  fileName: 'rules.pdf',
  contentType: 'application/pdf',
  alt: { en: '', it: '' },
  url: '/media/43/rules.pdf',
};

const LOGO: PickableMedia = { ...PICTURE, id: 7, fileName: 'partner-logo.png' };

const answering = (key: string, items: PickableMedia[]): MediaLibraryQuery =>
  queryOptions({
    queryKey: ['pictures-test', key] as readonly unknown[],
    queryFn: () => Promise.resolve({ items, total: items.length }),
  });

/** A library that knows how to offer only its pictures, and one category of them. */
function library() {
  const pictures = vi.fn((): MediaLibraryQuery => ({
    ...answering('pictures', [PICTURE]),
    meta: { narrow: (category: string) => answering(`pictures-${category}`, [LOGO]) },
  }));
  const narrow = vi.fn((category: string) => answering(`any-${category}`, [LOGO, DOCUMENT]));
  const query: MediaLibraryQuery = { ...answering('any', [PICTURE, DOCUMENT]), meta: { pictures, narrow } };
  return { query, pictures, narrow };
}

function render(
  schema: z.ZodType<Record<string, unknown>, Record<string, unknown>>,
  mediaLibrary: MediaLibraryQuery,
) {
  return renderWithProviders(
    <SchemaForm
      schema={schema}
      defaults={{}}
      locales={['en']}
      labels="test"
      submitLabel="Save"
      onSubmit={() => Promise.resolve()}
      mediaLibrary={mediaLibrary}
      division={DIVISION}
    />,
    { i18n: createTestI18n({ test: { fields: { file: 'File' } } }) },
  );
}

test('a media field draws a picture, and is offered no document', async () => {
  const { query, pictures } = library();
  render(z.object({ file: z.number().optional().meta({ media: true }) }), query);

  expect(await screen.findByRole('button', { name: /hangar\.png/ })).toBeTruthy();
  expect(screen.queryByRole('button', { name: /rules\.pdf/ })).toBeNull();
  expect(pictures).toHaveBeenCalled();
});

test('the file of a document is offered every file', async () => {
  const { query, pictures } = library();
  render(z.object({ file: z.number().optional().meta({ media: true, mediaAnyFile: true }) }), query);

  expect(await screen.findByRole('button', { name: /rules\.pdf/ })).toBeTruthy();
  expect(screen.getByRole('button', { name: /hangar\.png/ })).toBeTruthy();
  expect(pictures).not.toHaveBeenCalled();
});

test('a field of one category is offered the pictures of that category', async () => {
  const { query, narrow } = library();
  render(z.object({ file: z.number().optional().meta({ media: true, mediaCategory: 'Logo' }) }), query);

  expect(await screen.findByRole('button', { name: /partner-logo\.png/ })).toBeTruthy();
  expect(screen.queryByRole('button', { name: /rules\.pdf/ })).toBeNull();
  // Narrowed from the pictures, and not from the whole library.
  expect(narrow).not.toHaveBeenCalled();
});
