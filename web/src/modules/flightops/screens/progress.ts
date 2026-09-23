import type { ProgressUnit } from '../api';

/**
 * How far a pilot got in a tour, as the three places that show it write it (T15b): the cards of `/tours`, the block
 * `flightops.myTours`, the pilot's page of the staff. The measure is the server's (`PilotProgress`): done out of a target,
 * in legs, miles, the goal of an `Open` tour or subtours. Nothing here computes a progress — it only says it.
 */

export interface Measure {
  readonly done: number;
  readonly target: number;
  readonly unit: ProgressUnit;
}

/** The share of the bar, from 0 to 100; nothing when the tour names no target (an `Open` tour without a goal). */
export function percentOf(measure: Measure, completed: boolean): number {
  if (completed) {
    return 100;
  }

  return measure.target <= 0 ? 0 : Math.min(100, Math.round((measure.done / measure.target) * 100));
}

/** The words of the measure: `flightops:progress.{unit}` with the two numbers, the target when there is one. */
export function measureWords(measure: Measure): { key: string; values: { done: number; target: number } } {
  return {
    key: measure.target <= 0 ? 'flightops:progress.noTarget' : `flightops:progress.${measure.unit}`,
    values: { done: measure.done, target: measure.target },
  };
}

/**
 * The years a statistics page offers besides the current one, newest first — the previous one a click away (note
 * 2026-09-24-le-pagine-delle-persone §2). Five: the tours of Toursystem went back that far.
 */
export function earlierYears(current: number, count = 5): number[] {
  return Array.from({ length: count }, (_, index) => current - 1 - index);
}

/** Minutes flown as hours and minutes, for «12 h 05 min». */
export function hoursAndMinutes(minutes: number): { hours: number; minutes: string } {
  const whole = Math.max(0, Math.floor(minutes));
  return { hours: Math.floor(whole / 60), minutes: String(whole % 60).padStart(2, '0') };
}
