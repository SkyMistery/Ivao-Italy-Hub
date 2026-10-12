import { holdsPermissionAnywhere, type Bootstrap } from '../../../shared/api/bootstrap';
import { EVENTS_VIEW } from '../permissions';

/** The section of the back office where the ATC of the events lives for whoever does not read the events (E11a). */
export const ATC_SECTION = '/staff/events/atc';

/** Where the ATC of an event lives for this reader: the tab of its page, for whoever reads the event, and its own page otherwise. */
export function atcHome(bootstrap: Bootstrap, eventId: number): string {
  return holdsPermissionAnywhere(bootstrap, EVENTS_VIEW)
    ? `/staff/events/${eventId}?tab=atc`
    : `${ATC_SECTION}/${eventId}`;
}
