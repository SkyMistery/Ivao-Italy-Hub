import { screen } from '@testing-library/react';
import { expect, test } from 'vitest';

import { renderWithProviders } from '../test/harness';

import { CoordinationBlock, FrequencyTableBlock } from './blocks';

/**
 * The two tables of an operational document (G14). What is asserted is what a schema cannot say:
 * that a station's kind and a flag read as words, and that the note is the one translated cell.
 */

test('a frequency table reads the kind and CPDLC as words, and the note in the language on screen', () => {
  renderWithProviders(
    <FrequencyTableBlock
      props={{
        stations: [
          {
            callsign: 'LIRF_TWR',
            frequency: '118.700',
            kind: 'TWR',
            cpdlc: false,
            minimumRating: 'ADC',
            note: { en: 'Runway 16L/16R', it: 'Pista 16L/16R' },
          },
          { callsign: 'LIRR_CTR', frequency: '124.750', kind: 'CTR', cpdlc: true },
        ],
      }}
    />,
  );

  expect(screen.getByText('LIRF_TWR')).toBeInTheDocument();
  expect(screen.getByText('Tower')).toBeInTheDocument();
  expect(screen.getByText('Centre')).toBeInTheDocument();
  expect(screen.getByText('Yes')).toBeInTheDocument();
  expect(screen.getByText('No')).toBeInTheDocument();
  expect(screen.getByText('Runway 16L/16R')).toBeInTheDocument();
});

test('a frequency table draws the name of the stations only when a row has one', () => {
  // Copied from IVAO's position since PR 3.14 (design of the editor, phase 3, §5.8). A table written before has no name
  // anywhere, and keeps the columns it always had.
  const { unmount } = renderWithProviders(
    <FrequencyTableBlock
      props={{ stations: [{ callsign: 'XXXX_TWR', frequency: '118.700', kind: 'TWR', cpdlc: false }] }}
    />,
  );
  expect(screen.queryByRole('columnheader', { name: 'Name' })).not.toBeInTheDocument();
  unmount();

  renderWithProviders(
    <FrequencyTableBlock
      props={{
        stations: [
          { callsign: 'XXXX_TWR', name: 'Example Tower', frequency: '118.700', kind: 'TWR', cpdlc: false },
          { callsign: 'XXXX_GND', frequency: '121.900', kind: 'GND', cpdlc: false },
        ],
      }}
    />,
  );
  expect(screen.getByRole('columnheader', { name: 'Name' })).toBeInTheDocument();
  expect(screen.getByText('Example Tower')).toBeInTheDocument();
  expect(screen.getAllByRole('row')[2]?.querySelectorAll('td, th')).toHaveLength(7);
});

test('a coordination table reads the direction as words', () => {
  renderWithProviders(
    <CoordinationBlock
      props={{
        agreements: [
          { from: 'LIRR_CTR', to: 'LIRF_APP', point: 'TAQ', level: 'FL110', direction: 'inbound' },
          { from: 'LIRF_APP', to: 'LIRR_CTR', direction: 'outbound', note: { en: 'Released for climb' } },
        ],
      }}
    />,
  );

  expect(screen.getByText('TAQ')).toBeInTheDocument();
  expect(screen.getByText('Inbound')).toBeInTheDocument();
  expect(screen.getByText('Outbound')).toBeInTheDocument();
  expect(screen.getByText('Released for climb')).toBeInTheDocument();
});
