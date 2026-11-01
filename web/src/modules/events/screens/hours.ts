import type { MomentOptions } from '../../../shared/i18n/useMoment';

/** `useMoment`'s formatter, handed in so that a test reads this without drawing anything. */
type Moment = (value: unknown, options?: MomentOptions) => string;

/**
 * The end of some hours, as a line writes it after their start (#333, item 6): the time alone on the start's day, and the date with it
 * on another — a turn or an hour that ends at 02:45 the next day never reads as a bare 02:45. As the training's `spanText` writes a
 * date that ends on another day. In UTC, as every moment of the events.
 */
export function endOfHours(startsAtUtc: string, endsAtUtc: string, moment: Moment): string {
  return moment(startsAtUtc, { time: false }) === moment(endsAtUtc, { time: false })
    ? moment(endsAtUtc, { date: false })
    : moment(endsAtUtc);
}
