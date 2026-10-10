/**
 * The moments of the bookings of an event as data (design M4 §3.3, §7.1, E6b): how long until its bookings open, whether a slot can
 * still be booked, and whether today is the event's day — when its page shows who is online at its airports. Plain TypeScript beside
 * the components, so that a test reads it without drawing anything, and with the clock handed in.
 */

/** How long until a moment: whole days, and the hours, minutes and seconds after them as `03:15:42`. None once it has come. */
export function countdown(nowMs: number, untilMs: number): { days: number; time: string } | null {
  const left = Math.floor((untilMs - nowMs) / 1000);
  if (left <= 0) {
    return null;
  }

  const two = (value: number) => String(value).padStart(2, '0');
  const days = Math.floor(left / 86_400);
  const hours = Math.floor((left % 86_400) / 3600);
  const minutes = Math.floor((left % 3600) / 60);

  return { days, time: `${two(hours)}:${two(minutes)}:${two(left % 60)}` };
}

/**
 * Whether the bookings of an event are open at `nowMs` (§3.3): from their opening, unless the event is cancelled. Each slot closes on
 * its own at its off block (`slotOpen`). The server decides; the page only offers what it would accept.
 */
export function bookingsOpen(
  event: { readonly bookingOpensAtUtc: string | null; readonly cancelledAt: string | null },
  nowMs: number,
): boolean {
  return (
    event.cancelledAt === null &&
    event.bookingOpensAtUtc !== null &&
    Date.parse(event.bookingOpensAtUtc) <= nowMs
  );
}

/** Whether a slot can still be booked, or withdrawn, at `nowMs`: until its off block, which is already closed (§3.3, §3.6). */
export function slotOpen(slot: { readonly offBlockUtc: string }, nowMs: number): boolean {
  return nowMs < Date.parse(slot.offBlockUtc);
}

/** The day of an instant where the division lives, as `2026-10-26`. */
export function dayIn(instant: Date, timeZone: string): string {
  const parts = new Intl.DateTimeFormat('en', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(instant);
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((entry) => entry.type === type)?.value ?? '';

  return `${part('year')}-${part('month')}-${part('day')}`;
}

/**
 * Whether `now` falls on the day of an event (design M4 §7.1, E6b): its days where the division lives, from the day it starts to the
 * day it ends — an evening is one day, a night across midnight two; an event that ends at midnight ends on the day before.
 */
export function onTheEventsDay(startsAtUtc: string, endsAtUtc: string, now: Date, timeZone: string): boolean {
  const today = dayIn(now, timeZone);
  const first = dayIn(new Date(startsAtUtc), timeZone);
  const last = dayIn(new Date(Date.parse(endsAtUtc) - 1), timeZone);

  return first <= today && today <= last;
}
