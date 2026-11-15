import { expect, test } from 'vitest';

import { frequencyTableSchema } from '../../blocks/schemas';
import { atcPositionListOf, namesAtcPositions, readFields, suggestAtcPositions } from '../../shared/forms';

import { copiedFrom, driftOf, frequenciesPending, positionSuggestions, realigned } from './atcPositions';
import type { AtcPositionDto } from './queries';

/**
 * The ATC positions of the division in the frequency table (design of the editor, phase 3, §5.8): offered by the callsign,
 * copied into the row, and compared with the rows already written.
 */

const tower: AtcPositionDto = {
  callsign: 'XXXX_TWR',
  type: 'TWR',
  name: 'Example Tower',
  airportIcao: 'XXXX',
  fir: 'XXXF',
  frequency: '118.700',
};
const sector: AtcPositionDto = {
  callsign: 'XXXF_CTR',
  type: 'CTR',
  name: 'Example Control',
  airportIcao: null,
  fir: 'XXXF',
  frequency: '124.750',
};
const positions = [tower, sector];

const where = atcPositionListOf(frequencyTableSchema)!;

test('the frequency table keeps its positions in its stations, by their callsign', () => {
  expect(where.list).toBe('stations');
  expect(where.field).toBe('callsign');
  expect(namesAtcPositions(frequencyTableSchema)).toBe(true);
  expect(atcPositionListOf(null)).toBeNull();
});

test('the callsign offers the positions, grouped by FIR, each with what it copies', () => {
  const suggestions = positionSuggestions(positions, where.entry);

  expect(suggestions[0]).toEqual({
    value: 'XXXX_TWR',
    label: 'Example Tower · 118.700',
    group: 'XXXF',
    fills: { name: 'Example Tower', kind: 'TWR', frequency: '118.700' },
  });

  // Drawn as a suggested field, and only the callsign.
  const stations = readFields(suggestAtcPositions(frequencyTableSchema, suggestions)).find(
    (node) => node.path === 'stations',
  );
  const children = stations?.kind === 'list' ? stations.children : [];
  expect(children.find((node) => node.path === 'stations.callsign')?.kind).toBe('suggest');
  expect(children.find((node) => node.path === 'stations.frequency')?.kind).toBe('text');
});

test('what a position copies is what the row has a field for, and what IVAO has given', () => {
  // A kind the table does not list is not copied, and neither is an empty name or a frequency not synchronised yet.
  expect(copiedFrom({ ...tower, type: 'XYZ', name: '', frequency: null }, where.entry)).toEqual({});
  expect(copiedFrom(sector, where.entry)).toEqual({
    name: 'Example Control',
    kind: 'CTR',
    frequency: '124.750',
  });
});

test('a row is flagged when IVAO now says otherwise, and only then', () => {
  const props = {
    stations: [
      // As IVAO says: the frequency written with fewer decimals is the same one, and no name written is not a difference.
      { callsign: 'XXXX_TWR', frequency: '118.7', kind: 'TWR', cpdlc: false },
      // A new frequency, a kind left at its default, a name IVAO changed.
      { callsign: 'xxxf_ctr', name: 'Old Control', frequency: '125.000', kind: 'TWR', cpdlc: false },
      // A station IVAO does not list is the writer's own.
      { callsign: 'XXXX_GND', frequency: '121.900', kind: 'GND', cpdlc: false },
    ],
  };

  expect(driftOf(props, where, positions)).toEqual([
    {
      index: 1,
      callsign: 'XXXF_CTR',
      changes: [
        { field: 'callsign', written: 'xxxf_ctr', ivao: 'XXXF_CTR' },
        { field: 'name', written: 'Old Control', ivao: 'Example Control' },
        { field: 'kind', written: 'TWR', ivao: 'CTR' },
        { field: 'frequency', written: '125.000', ivao: '124.750' },
      ],
    },
  ]);
  expect(driftOf({}, where, positions)).toEqual([]);
});

test('realigning copies the row again, and leaves the rest of it and the other rows alone', () => {
  const props = {
    caption: { en: 'Stations' },
    stations: [
      { callsign: 'XXXX_TWR', frequency: '118.700', kind: 'TWR', cpdlc: false },
      { callsign: 'xxxf_ctr', frequency: '125.000', kind: 'TWR', cpdlc: true, note: { en: 'Night only' } },
    ],
  };

  expect(realigned(props, where, positions, 1)).toEqual({
    caption: { en: 'Stations' },
    stations: [
      props.stations[0],
      {
        callsign: 'XXXF_CTR',
        name: 'Example Control',
        frequency: '124.750',
        kind: 'CTR',
        cpdlc: true,
        note: { en: 'Night only' },
      },
    ],
  });

  // A row that names no listed position, or no row at all: nothing to copy.
  expect(realigned(props, where, [], 1)).toBe(props);
  expect(realigned(props, where, positions, 5)).toBe(props);
});

test('the block says when IVAO has not given the frequencies yet', () => {
  expect(
    frequenciesPending([
      { ...tower, frequency: null },
      { ...sector, frequency: null },
    ]),
  ).toBe(true);
  expect(frequenciesPending(positions)).toBe(false);
  expect(frequenciesPending([])).toBe(false);
});
