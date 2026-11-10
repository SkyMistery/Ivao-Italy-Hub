import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useMemo, useState } from 'react';
import { expect, test, vi } from 'vitest';
import { z } from 'zod';

import { SchemaForm } from '../../shared/forms/SchemaForm';
import { createTestI18n, renderWithProviders } from '../../test/harness';

import { useAutosave, type SaveOutcome } from './useAutosave';

/**
 * A press that stores the draft first — «Publish», «Download for Claude», «Ready for review», «Open the preview» —
 * pressed right after the last keystroke. The editor hands the autosave the fields through the live form, which
 * applies them a pause after the last letter (`SchemaForm`'s `LIVE_DELAY`); until then the autosave holds the draft
 * without that letter, and `flush` found nothing to store (measured in a browser: «Download for Claude» downloaded
 * the draft without the last word, 40 times out of 40, with the press 30–60 ms after the keystroke).
 *
 * The editor in small: a live form, the snapshot made from what it applies, and a button that flushes first.
 */

const schema = z.object({ slug: z.string().min(1) });
const labels = { test: { fields: { slug: 'Address' } } };

function Editor({ save }: { save: (slug: string) => void }) {
  const [values, setValues] = useState({ slug: 'welcome' });
  const snapshot = useMemo(() => JSON.stringify(values), [values]);
  const [stored, setStored] = useState<boolean | null>(null);

  const autosave = useAutosave({
    enabled: true,
    snapshot,
    busy: false,
    save: (): Promise<SaveOutcome> => {
      save(values.slug);
      return Promise.resolve('saved');
    },
  });

  return (
    <>
      <SchemaForm schema={schema} defaults={values} locales={['en']} labels="test" onChange={setValues} />
      <button type="button" onClick={() => void autosave.flush().then(setStored)}>
        Download
      </button>
      {stored === null ? null : <output>{stored ? 'stored' : 'refused'}</output>}
    </>
  );
}

test('a press right after the last keystroke stores the draft with that keystroke', async () => {
  const save = vi.fn();
  const user = userEvent.setup();
  renderWithProviders(<Editor save={save} />, { i18n: createTestI18n(labels) });

  await user.type(screen.getByLabelText('Address'), '-team');
  await user.click(screen.getByRole('button', { name: 'Download' }));

  await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('stored'));
  expect(save).toHaveBeenCalledWith('welcome-team');
});

test('a press with the keyboard, leaving the field by Tab, stores it too', async () => {
  const save = vi.fn();
  const user = userEvent.setup();
  renderWithProviders(<Editor save={save} />, { i18n: createTestI18n(labels) });

  await user.type(screen.getByLabelText('Address'), '-team');
  await user.tab();
  expect(screen.getByRole('button', { name: 'Download' })).toHaveFocus();
  await user.keyboard('{Enter}');

  await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('stored'));
  expect(save).toHaveBeenCalledWith('welcome-team');
});
