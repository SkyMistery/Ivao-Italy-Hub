import { describe, expect, it } from 'vitest';

import { suggestedPositions } from './positions';

describe('suggestedPositions', () => {
  it('offers the stations of the airport and the centre of the FIR, grouped by each', () => {
    const positions = suggestedPositions('lirf', 'LIRR');

    expect(positions.map((position) => position.value)).toEqual([
      'LIRF_DEL',
      'LIRF_GND',
      'LIRF_TWR',
      'LIRF_APP',
      'LIRF_DEP',
      'LIRR_CTR',
      'LIRR_FSS',
    ]);
    expect(positions[0]?.group).toBe('LIRF');
    expect(positions.at(-1)?.group).toBe('LIRR');
  });

  it('offers nothing while neither is chosen', () => {
    expect(suggestedPositions(undefined, '')).toEqual([]);
  });
});
