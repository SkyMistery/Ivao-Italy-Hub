import type { TFunction } from 'i18next';

import type { RouteMapLeg, RouteMapTrack } from '../../../shared/ui';
import type { PirepStatus, ReviewDto, ReviewFlightDto, ReviewPlanDto, ReviewTrackDto } from '../api';

/**
 * What the validation pages compute, pure, so a test can read it without a browser (T13b). Nothing here decides: the
 * suggestion, what the reader may do and every refusal are the server's (`PirepReview`); this is how its answer is drawn.
 */

/** The two orders of the queue (design M2 §4.1), as the preference `flightops.reviewQueueOrder` keeps them. */
export const REVIEW_QUEUE_ORDERS = ['date', 'tour'] as const;
export type ReviewQueueOrder = (typeof REVIEW_QUEUE_ORDERS)[number];

export const REVIEW_QUEUE_ORDER_PREFERENCE = 'flightops.reviewQueueOrder';

/** A stored preference read back: anything but «tour» — nothing chosen, or a value from another day — is by date. */
export function queueOrderOf(stored: unknown): ReviewQueueOrder {
  return stored === 'tour' ? 'tour' : 'date';
}

/** The column of the list an order sorts on. By tour, the server keeps the date inside each tour (T13a). */
export function queueSortOf(order: ReviewQueueOrder): string {
  return order === 'tour' ? 'tourId' : 'queuedAt';
}

/** The colours of a report's status, the pilot's page's and the staff's alike. */
export const REPORT_STATUS_COLOURS: Readonly<
  Record<PirepStatus, 'blue' | 'green' | 'orange' | 'red' | 'gray'>
> = {
  Queued: 'blue',
  InReview: 'blue',
  Accepted: 'green',
  ToModify: 'orange',
  Rejected: 'red',
  Withdrawn: 'gray',
};

/** The revision of the plan that held at take-off, which the page draws first and marks (§4.3). */
export function planAtTakeoff(flight: ReviewFlightDto): ReviewPlanDto | null {
  return flight.flightPlans.find((plan) => plan.revision === flight.planAtTakeoffRevision) ?? null;
}

/** Minutes as a plan writes them: 0930, 0145. */
export function hhmm(minutes: number | null): string {
  if (minutes === null) {
    return '—';
  }

  const hours = Math.floor(minutes / 60);
  return `${String(hours).padStart(2, '0')}${String(minutes % 60).padStart(2, '0')}`;
}

/**
 * The note of a step of the history: a key of the module for the steps the server writes itself («taken», «sent»), the
 * validator's own words for a reopening. A key is recognised by the namespace it names.
 */
export function eventNote(note: string | null, t: TFunction): string | null {
  if (note === null || note === '') {
    return null;
  }

  return /^flightops:[a-zA-Z.]+$/.test(note) ? t(note) : note;
}

/**
 * The map of the validation page: the leg as it was frozen, and — when the flight ended elsewhere — each flight between
 * the airports it actually joined, drawn as «waiting». An airport the reference data has no position for is left out, and
 * so is the line that needs it.
 */
export function reviewMapLegs(review: ReviewDto): RouteMapLeg[] {
  const where = (icao: string) => {
    const airport = review.airports.find((entry) => entry.icao === icao);
    return airport === undefined || airport.latitude === null || airport.longitude === null
      ? null
      : { code: icao, latitude: airport.latitude, longitude: airport.longitude };
  };

  const pairs: { id: string; from: string; to: string; status: 'todo' | 'pending' }[] = [
    { id: 'leg', from: review.leg.departureIcao, to: review.leg.arrivalIcao, status: 'todo' },
  ];

  if (review.isDiversion) {
    for (const flight of review.flights) {
      pairs.push({
        id: `flight-${flight.seq}`,
        from: flight.departureIcao,
        to: flight.arrivalIcao,
        status: 'pending',
      });
    }
  }

  return pairs.flatMap((pair) => {
    const from = where(pair.from);
    const to = where(pair.to);
    return from === null || to === null ? [] : [{ id: pair.id, from, to, status: pair.status }];
  });
}

/** The tracks the server still has, as the map draws them; a flight whose track has gone is simply not drawn. */
export function reviewMapTracks(tracks: readonly ReviewTrackDto[] | undefined): RouteMapTrack[] {
  return (tracks ?? []).flatMap((track) =>
    track.points === null || track.points.length < 2
      ? []
      : [{ id: track.seq, points: track.points.map(({ latitude, longitude }) => ({ latitude, longitude })) }],
  );
}

/** The outcomes a decision can have, in the order the form offers them. */
export const DECISION_OUTCOMES = ['Accepted', 'ToModify', 'Rejected'] as const;
export type DecisionOutcome = (typeof DECISION_OUTCOMES)[number];
