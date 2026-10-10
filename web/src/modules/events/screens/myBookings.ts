import type { LocalizedString } from '../../../shared/api/bootstrap';
import type { MyBookingDto } from '../api';

/**
 * A pilot's own bookings as data (design M4 §7.1, §7.3, E6b): the ones still to fly and the past ones, and both under their events —
 * what `/events/mine` and the block `events.myEvents` draw —, and who reads the page of an event as its slots see them. Plain
 * TypeScript beside the components, so that a test reads it without drawing anything, and with the clock handed in.
 */

/** The member's own page of the events (design M4 §7.1): their bookings, past ones too. */
export const MY_EVENTS_PAGE = '/events/mine';

/**
 * Who is reading the page of an event, as the booking of a slot needs to know (E6b): signed in or not, and their own bookings of
 * the event by slot — read from their own list, never from the page, which says of a slot only whether it is taken.
 */
export interface SlotViewer {
  readonly signedIn: boolean;
  readonly mine: ReadonlyMap<number, MyBookingDto>;
}

/** A visitor: nothing booked, and asked to sign in to book. */
export const VISITOR: SlotViewer = { signedIn: false, mine: new Map() };

/** A booking as the server lists it for its pilot (`MyBookingDto`): what the groups and the split read of it. */
export interface OwnBooking {
  readonly id: number;
  readonly eventId: number;
  readonly eventSlug: string;
  readonly eventTitle: LocalizedString;
  readonly eventState: string;
  readonly offBlockUtc: string;
  readonly onBlockUtc: string;
}

/**
 * Still to fly, or flown: a flight whose on block is to come is still to fly — one in the air too —, by its off block; the others are
 * past, the latest first.
 */
export function splitBookings<TBooking extends OwnBooking>(
  bookings: readonly TBooking[],
  nowMs: number,
): { upcoming: TBooking[]; past: TBooking[] } {
  const byOffBlock = (one: TBooking, other: TBooking) =>
    Date.parse(one.offBlockUtc) - Date.parse(other.offBlockUtc) || one.id - other.id;
  const ahead = (booking: TBooking) => Date.parse(booking.onBlockUtc) > nowMs;

  return {
    upcoming: bookings.filter(ahead).sort(byOffBlock),
    past: bookings.filter((booking) => !ahead(booking)).sort((one, other) => byOffBlock(other, one)),
  };
}

/** The bookings of one event, with what its title and its state say. */
export interface BookingGroup<TBooking extends OwnBooking> {
  readonly eventId: number;
  readonly slug: string;
  readonly title: LocalizedString;
  readonly state: string;
  readonly bookings: TBooking[];
}

/** The bookings under their events: each event once, in the order its first booking comes, and its bookings in theirs. */
export function bookingsByEvent<TBooking extends OwnBooking>(
  bookings: readonly TBooking[],
): BookingGroup<TBooking>[] {
  const groups = new Map<number, BookingGroup<TBooking>>();

  for (const booking of bookings) {
    const group = groups.get(booking.eventId) ?? {
      eventId: booking.eventId,
      slug: booking.eventSlug,
      title: booking.eventTitle,
      state: booking.eventState,
      bookings: [],
    };
    group.bookings.push(booking);
    groups.set(booking.eventId, group);
  }

  return [...groups.values()];
}
