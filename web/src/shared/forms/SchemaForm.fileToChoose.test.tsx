import { queryOptions } from '@tanstack/react-query';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';
import { z } from 'zod';

import { createTestI18n, renderWithProviders } from '../../test/harness';
import type { MediaLibraryQuery, PickableMedia } from '../ui';

import { SchemaForm } from './SchemaForm';

/**
 * A live form applies what was written beside a file nobody has chosen yet (#377, note 2026-11-15-il-file-ancora-da-scegliere).
 * The alternative text of an Image block written before its file was lost when the block was left: the form waited for
 * the whole schema, and a required file is not there until somebody chooses it. Publication names that file
 * (`errors.content.mediaToChoose`), so the draft may hold the hole; it must not hold back the rest.
 */

const DIVISION = { defaultLocale: 'en', timezone: 'Europe/Rome' };

const library: MediaLibraryQuery = queryOptions({
  queryKey: ['file-to-choose-test', 'library'] as readonly unknown[],
  queryFn: () => Promise.resolve({ items: [] as PickableMedia[], total: 0 }),
});

const labels = {
  test: { fields: { mediaId: 'Picture', alt: 'Alternative text', caption: 'Caption', items: 'Items' } },
};

function renderLive<TValues extends Record<string, unknown>>(
  schema: z.ZodType<TValues, TValues>,
  defaults: TValues,
  onChange: (values: TValues) => void,
) {
  return renderWithProviders(
    <SchemaForm
      schema={schema}
      defaults={defaults}
      locales={['en']}
      labels="test"
      mediaLibrary={library}
      division={DIVISION}
      onChange={onChange}
    />,
    { i18n: createTestI18n(labels) },
  );
}

test('what is written beside a file still to choose is applied, without the file', async () => {
  const onChange = vi.fn();
  const user = userEvent.setup();
  renderLive(
    z.object({ mediaId: z.number().int().meta({ media: true }), alt: z.string().optional() }),
    { alt: '' } as { mediaId: number; alt?: string },
    onChange,
  );

  await user.type(screen.getByLabelText('Alternative text'), 'An open hangar');

  await waitFor(() => expect(onChange).toHaveBeenCalled());
  const applied = onChange.mock.lastCall?.[0] as Record<string, unknown>;
  expect(applied).toEqual({ alt: 'An open hangar' });
  // Not a stand-in, not an empty key: the file is simply not there.
  expect('mediaId' in applied).toBe(false);
});

test('a file still to choose in an entry of a list holds back nothing either', async () => {
  const onChange = vi.fn();
  const user = userEvent.setup();
  renderLive(
    z.object({
      caption: z.string().optional(),
      items: z.array(z.object({ mediaId: z.number().int().meta({ media: true }) })),
    }),
    { caption: '', items: [{}] } as { caption?: string; items: { mediaId: number }[] },
    onChange,
  );

  await user.type(screen.getByLabelText('Caption'), 'Hangars');

  await waitFor(() => expect(onChange).toHaveBeenCalled());
  expect(onChange.mock.lastCall?.[0]).toEqual({ caption: 'Hangars', items: [{}] });
});

test('anything else the schema refuses still applies nothing, file or no file', async () => {
  const onChange = vi.fn();
  const user = userEvent.setup();
  renderLive(
    z.object({ mediaId: z.number().int().meta({ media: true }), alt: z.string().max(3) }),
    { alt: '' } as { mediaId: number; alt: string },
    onChange,
  );

  // Too long: a refusal of its own, which the field says on screen.
  await user.type(screen.getByLabelText('Alternative text'), 'Too long');
  await new Promise((resolve) => setTimeout(resolve, 250));
  expect(onChange).not.toHaveBeenCalled();
});
