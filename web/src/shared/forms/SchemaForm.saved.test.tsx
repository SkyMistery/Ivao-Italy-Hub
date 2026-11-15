import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';
import { z } from 'zod';

import { createTestI18n, renderWithProviders } from '../../test/harness';

import { SchemaForm } from './SchemaForm';
import { localized, writtenValues } from './schema';

/**
 * A value saved by the form, opened again in it. The save leaves out an optional field nobody wrote — a translated note
 * left empty in every language, inside the entry of a list too (`writtenValues`) — so what comes back has no such field,
 * and the form must take it as it took it the first time: the field empty, the rest editable. It did not: the field came
 * back `undefined`, a translated field refuses that, and a live form applied nothing more, without a word (found while
 * checking PR 3.14 on screen: a frequency table saved with a station without a note).
 */

const schema = z.object({
  caption: localized().optional(),
  stations: z.array(
    z.object({
      callsign: z.string(),
      note: localized().optional(),
      settings: z.object({ remark: localized().optional(), hours: z.number().optional() }).optional(),
    }),
  ),
});
type Values = z.infer<typeof schema>;

const labels = {
  test: {
    fields: {
      caption: 'Caption',
      stations: 'Stations',
      'stations.callsign': 'Callsign',
      'stations.note': 'Note',
      'stations.settings': 'Settings',
      'stations.settings.remark': 'Remark',
      'stations.settings.hours': 'Hours',
    },
  },
};

/** What the save stores of a station whose note, remark and hours nobody wrote. */
const saved = writtenValues(schema, {
  caption: { en: '', it: '' },
  stations: [{ callsign: 'XXXX_TWR', note: { en: '', it: '' }, settings: { remark: { en: '', it: '' } } }],
} as Values);

test('what the save stores leaves the unwritten fields out, which is what is opened again', () => {
  expect(saved).toEqual({ stations: [{ callsign: 'XXXX_TWR' }] });
});

test('a live form opened on a saved entry without its optional fields applies what is typed', async () => {
  const user = userEvent.setup();
  const onChange = vi.fn();
  renderWithProviders(
    <SchemaForm schema={schema} defaults={saved} locales={['en', 'it']} labels="test" onChange={onChange} />,
    { i18n: createTestI18n(labels) },
  );

  await user.type(screen.getByLabelText('Callsign'), 'X');

  await waitFor(() => expect(onChange).toHaveBeenCalled());
  const [values] = onChange.mock.lastCall as [Values];
  expect(values.stations[0]?.callsign).toBe('XXXX_TWRX');

  // And what it applies is stored as it was: nothing was written into the note, the remark or the hours, so the save
  // leaves them out again — opening a value changes nothing about it.
  expect(writtenValues(schema, values)).toEqual({ stations: [{ callsign: 'XXXX_TWRX' }] });
});

test('a form sent with a button sends a saved entry without its optional fields', async () => {
  const user = userEvent.setup();
  const onSubmit = vi.fn(() => Promise.resolve());
  renderWithProviders(
    <SchemaForm
      schema={schema}
      defaults={saved}
      locales={['en', 'it']}
      labels="test"
      submitLabel="Save"
      onSubmit={onSubmit}
    />,
    { i18n: createTestI18n(labels) },
  );

  await user.click(screen.getByRole('button', { name: 'Save' }));

  await waitFor(() => expect(onSubmit).toHaveBeenCalled());
});
