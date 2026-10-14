import { holdsPermissionAnywhere, type Bootstrap } from '../../../shared/api/bootstrap';
import { EVENTS_VIEW } from '../permissions';

/** The section of the back office where the ATC of the events lives for whoever does not read the events (E11a). */
export const ATC_SECTION = '/staff/events/atc';

/**
 * Whether an event takes no more positions: cancelled, or over — the server refuses a position written on it, and the page offers no
 * «Save» (the review of #256, second reading, point 1).
 */
export function isClosed(event: { readonly state: string }): boolean {
  return event.state === 'Cancelled' || event.state === 'Ended';
}

/** Where the ATC of an event lives for this reader: the tab of its page, for whoever reads the event, and its own page otherwise. */
export function atcHome(bootstrap: Bootstrap, eventId: number): string {
  return holdsPermissionAnywhere(bootstrap, EVENTS_VIEW)
    ? `/staff/events/${eventId}?tab=atc`
    : `${ATC_SECTION}/${eventId}`;
}
