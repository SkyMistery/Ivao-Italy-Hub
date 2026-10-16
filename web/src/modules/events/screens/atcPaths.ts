import { holdsPermissionAnywhere, type Bootstrap } from '../../../shared/api/bootstrap';
import { EVENTS_VIEW } from '../permissions';

/** The section of the back office where the ATC of the events lives for whoever does not read the events (E11a). */
export const ATC_SECTION = '/staff/events/atc';

/**
 * Whether an event takes no more positions: cancelled, or over — the server refuses a position written on it, and the page offers no
 * «Save» (the review of #256, second reading, point 1).
 */
export function isClosed(
  event: { readonly state: string; readonly endsAtUtc: string },
  now: number = Date.now(),
): boolean {
  // A draft whose end has passed says «Draft» as its state, and the server refuses it as over (E11a's third reading on #256, point 2).
  return event.state === 'Cancelled' || event.state === 'Ended' || Date.parse(event.endsAtUtc) <= now;
}

/**
 * Whether an event has started: from then on its shifts are the register of reliability, and none is taken away (E11b, Carmine's answer on
 * issue #259).
 */
export function hasStarted(event: { readonly startsAtUtc: string }, now: number = Date.now()): boolean {
  return Date.parse(event.startsAtUtc) <= now;
}

/** Where the ATC of an event lives for this reader: the tab of its page, for whoever reads the event, and its own page otherwise. */
export function atcHome(bootstrap: Bootstrap, eventId: number): string {
  return holdsPermissionAnywhere(bootstrap, EVENTS_VIEW)
    ? `/staff/events/${eventId}?tab=atc`
    : `${ATC_SECTION}/${eventId}`;
}
