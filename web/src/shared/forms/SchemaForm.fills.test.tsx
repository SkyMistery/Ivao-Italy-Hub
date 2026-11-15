import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';
import { z } from 'zod';

import { createTestI18n, renderWithProviders } from '../../test/harness';

import { SchemaForm } from './SchemaForm';
import type { Suggestion } from './schema';

/**
 * A suggestion that copies what it carries into the fields beside it (`Suggestion.fills`): an ATC position chosen in a
 * frequency table copies its name, kind and frequency into the row (design of the editor, phase 3, §5.8).
 */

const positions: Suggestion[] = [
  {
    value: 'XXXX_TWR',
    label: 'Example Tower · 118.700',
    group: 'XXXX',
    fills: { name: 'Example Tower', kind: 'TWR', frequency: '118.700' },
  },
  { value: 'XXXX_APP', label: 'Example Radar', group: 'XXXX', fills: { name: 'Example Radar', kind: 'APP' } },
];

const schema = z.object({
  stations: z.array(
    z.object({
      callsign: z.string().meta({ suggestions: positions }),
      name: z.string().optional(),
      frequency: z.string(),
      kind: z.enum(['TWR', 'APP', 'CTR']).default('CTR'),
    }),
  ),
});
type Values = z.infer<typeof schema>;

const labels = {
  test: {
    fields: {
      stations: 'Stations',
      'stations.callsign': 'Callsign',
      'stations.name': 'Name',
      'stations.frequency': 'Frequency',
      'stations.kind': 'Kind',
    },
  },
};

function renderStations(defaults: Values, onSubmit: (values: Values) => Promise<unknown>) {
  return renderWithProviders(
    <SchemaForm
      schema={schema}
      defaults={defaults}
      locales={['en']}
      labels="test"
      submitLabel="Save"
      onSubmit={onSubmit}
    />,
    { i18n: createTestI18n(labels) },
  );
}

test('choosing a suggestion copies what it carries into the fields of its own row', async () => {
  const user = userEvent.setup();
  const onSubmit = vi.fn(() => Promise.resolve());
  renderStations({ stations: [{ callsign: '', frequency: '', kind: 'CTR' }] }, onSubmit);

  await user.click(screen.getByLabelText('Callsign'));
  await user.click(await screen.findByText('Example Tower · 118.700'));

  expect(screen.getByLabelText('Callsign')).toHaveValue('XXXX_TWR');
  expect(screen.getByLabelText('Name')).toHaveValue('Example Tower');
  expect(screen.getByLabelText('Frequency')).toHaveValue('118.700');

  await user.click(screen.getByRole('button', { name: 'Save' }));
  expect(onSubmit).toHaveBeenCalledWith({
    stations: [{ callsign: 'XXXX_TWR', name: 'Example Tower', frequency: '118.700', kind: 'TWR' }],
  });
});

test('a suggestion copies only what it carries, and typing copies nothing', async () => {
  const user = userEvent.setup();
  const onSubmit = vi.fn(() => Promise.resolve());
  renderStations({ stations: [{ callsign: '', frequency: '121.500', kind: 'CTR' }] }, onSubmit);

  // A position whose frequency IVAO has not given yet leaves the one written by hand where it is.
  await user.click(screen.getByLabelText('Callsign'));
  await user.click(await screen.findByText('Example Radar'));
  expect(screen.getByLabelText('Frequency')).toHaveValue('121.500');

  // A callsign typed in full is the field's own text, not a choice: the row is left as it is.
  await user.clear(screen.getByLabelText('Callsign'));
  await user.type(screen.getByLabelText('Callsign'), 'XXXX_TWR');
  await user.keyboard('{Escape}');
  expect(screen.getByLabelText('Name')).toHaveValue('Example Radar');

  await user.click(screen.getByRole('button', { name: 'Save' }));
  expect(onSubmit).toHaveBeenCalledWith({
    stations: [{ callsign: 'XXXX_TWR', name: 'Example Radar', frequency: '121.500', kind: 'APP' }],
  });
});
