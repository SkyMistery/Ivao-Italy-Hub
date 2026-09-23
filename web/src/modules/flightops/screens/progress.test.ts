import { describe, expect, it } from 'vitest';

import { earlierYears, hoursAndMinutes, measureWords, percentOf } from './progress';

describe('the progress of a pilot, said', () => {
  it('fills the bar by the share done, and whole once the tour is completed', () => {
    expect(percentOf({ done: 3, target: 8, unit: 'Legs' }, false)).toBe(38);
    expect(percentOf({ done: 900, target: 600, unit: 'Miles' }, false)).toBe(100);
    // A leg added after the completion does not take the bar back (§3.11: a completion is never taken away).
    expect(percentOf({ done: 8, target: 9, unit: 'Legs' }, true)).toBe(100);
  });

  it('draws nothing of a tour with no target, rather than dividing by zero', () => {
    expect(percentOf({ done: 2, target: 0, unit: 'Goal' }, false)).toBe(0);
    expect(measureWords({ done: 2, target: 0, unit: 'Goal' }).key).toBe('flightops:progress.noTarget');
  });

  it('says the measure in the unit of its kind', () => {
    expect(measureWords({ done: 1, target: 3, unit: 'Subtours' })).toEqual({
      key: 'flightops:progress.Subtours',
      values: { done: 1, target: 3 },
    });
  });

  it('offers the five years before the current one, newest first', () => {
    expect(earlierYears(2026)).toEqual([2025, 2024, 2023, 2022, 2021]);
  });

  it('writes minutes as hours and two-digit minutes', () => {
    expect(hoursAndMinutes(725)).toEqual({ hours: 12, minutes: '05' });
    expect(hoursAndMinutes(0)).toEqual({ hours: 0, minutes: '00' });
  });
});
