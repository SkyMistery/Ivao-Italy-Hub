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
