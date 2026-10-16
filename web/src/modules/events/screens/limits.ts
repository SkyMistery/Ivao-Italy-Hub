import { ApiError } from '../../../shared/api/problem';

/**
 * The refusals of a booking for the limits of whoever books and does not fly (design M4 §3.7, E13b; note
 * 2026-10-16-i-no-show-i-registri-e-i-limiti, reading 8): the server says why with a key, and the page reads the numbers and the date from
 * the member's record (`LimitsNotice`).
 */

/** The two keys a booking is refused with for the limits of whoever books and does not fly, as `BookingLimits` spells them. */
export const LIMIT_KEYS = [
  'events:errors.bookingRestrictedWindow',
  'events:errors.bookingRestrictedEvent',
] as const;

/** Whether a refusal of the server, or the reason of a leg not booked, is one of the limits. */
export function isLimitRefusal(failure: unknown): boolean {
  return failure instanceof ApiError
    ? Object.values(failure.problem?.errors ?? {}).some((keys) => keys.some((key) => isLimitKey(key)))
    : false;
}

export function isLimitKey(key: string): boolean {
  return (LIMIT_KEYS as readonly string[]).includes(key);
}
