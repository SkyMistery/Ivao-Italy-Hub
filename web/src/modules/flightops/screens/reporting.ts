import { ApiError } from '../../../shared/api/problem';
import type { ChoiceOption } from '../../../shared/forms';
import type { RouteMapLeg, RouteMapStatus } from '../../../shared/ui';
import type {
  AtcContactDto,
  AtcContactWriteDto,
  AtcExemptionWriteDto,
  LegProgress,
  MyTourDto,
  PirepDto,
  PirepStatus,
  PublicLegDto,
  PublicTourDto,
  TrackerSessionDto,
} from '../api';
import type { AskSearch, AtcDeclarationValues } from '../schemas';

/**
 * What the pilot's screens compute, which is little: the server answers every question of the rules (`TourRules`), and
 * these only put its answers where the screen needs them. Pure, so a test reads them without a browser.
 */

/** The four states of a leg, spelled the way `RouteMap` spells them. */
const MAP_STATUS: Readonly<Record<LegProgress, RouteMapStatus>> = {
  Todo: 'todo',
  Done: 'done',
  Pending: 'pending',
  Locked: 'locked',
};

/**
 * A leg as the map draws it. For a visitor, blue once released and grey before; for a signed in pilot, the colour the
 * server gave it — green flown, orange waiting, grey locked (design M2 §8.1). A leg the server did not mention keeps the
 * visitor's colour rather than vanishing.
 */
export function mapLeg(leg: PublicLegDto, mine: MyTourDto | undefined): RouteMapLeg {
  const progress = mine?.legs.find((entry) => entry.id === leg.id)?.progress;

  return {
    id: leg.id,
    from: { code: leg.departureIcao, latitude: leg.departureLatitude, longitude: leg.departureLongitude },
    to: { code: leg.arrivalIcao, latitude: leg.arrivalLatitude, longitude: leg.arrivalLongitude },
    status: progress === undefined ? (leg.released ? 'todo' : 'locked') : MAP_STATUS[progress],
  };
}

/** What the pilot may do with one of their reports: withdraw it while nobody took it, correct it when it was sent back. */
export function reportActions(status: PirepStatus): { withdraw: boolean; correct: boolean } {
  return { withdraw: status === 'Queued', correct: status === 'ToModify' };
}

/**
 * The flights a correction starts from. The server hides a claimed session from the list of choices, and the one a
 * report «to modify» holds is claimed by that very report — so without these the pilot could not keep the flight they
 * already sent, which is the common case (the correction is a missing STAR, not a different flight).
 */
export function sessionsOf(report: PirepDto): TrackerSessionDto[] {
  return report.flights.map((flight) => ({
    id: flight.trackerSessionId,
    callsign: flight.callsign,
    startedAt: flight.takeoffAt,
    endedAt: flight.landingAt ?? flight.takeoffAt,
    departureIcao: flight.departureIcao,
    arrivalIcao: flight.arrivalIcao,
    aircraft: flight.aircraft,
  }));
}

/** The sessions to choose from: the ones kept by the report first, then what the tracker found, each once. */
export function mergeSessions(
  kept: readonly TrackerSessionDto[],
  found: readonly TrackerSessionDto[],
): TrackerSessionDto[] {
  const seen = new Set(kept.map((session) => session.id));

  return [...kept, ...found.filter((session) => !seen.has(session.id))];
}

/**
 * A refusal split by where it belongs on the page. The details form draws its own fields; everything else the server
 * refuses — the tour, the leg, the flights chosen, the diversion — is about the half of the page above it, and would
 * otherwise land on a field the details form does not have and be shown nowhere.
 */
export function splitRefusal(
  error: unknown,
  detailFields: readonly string[],
): { details: Error | null; flight: ApiError | null } {
  if (!(error instanceof ApiError) || error.problem?.errors === undefined) {
    return { details: error instanceof Error ? error : new Error(String(error)), flight: null };
  }

  const inDetails: Record<string, string[]> = {};
  const elsewhere: Record<string, string[]> = {};

  for (const [field, keys] of Object.entries(error.problem.errors)) {
    (detailFields.includes(field) ? inDetails : elsewhere)[field] = keys;
  }

  const flight =
    Object.keys(elsewhere).length === 0
      ? null
      : new ApiError(error.status, { ...error.problem, errors: elsewhere });

  // With nothing of its own the details form hears nothing: a refusal without fields would draw its generic banner
  // under the flight half that has already said what went wrong.
  const details =
    Object.keys(inDetails).length === 0
      ? null
      : new ApiError(error.status, { ...error.problem, errors: inDetails });

  return { details, flight };
}

/** The fields of the controllers' half of the form: a refusal on them is shown there, not under the flight. */
export const ATC_FIELDS: readonly string[] = ['atcContacts', 'exemptions'];

/** What the pilot declared about the controllers, as a correction starts from it: the added, the removed, the exemptions. */
export function declarationOf(report: PirepDto | null): {
  removed: string[];
  declaration: AtcDeclarationValues;
} {
  if (report === null) {
    return { removed: [], declaration: { added: [], exemptions: [] } };
  }

  return {
    removed: report.atcContacts
      .filter((contact) => contact.origin === 'Removed')
      .map((contact) => contact.callsign),
    declaration: {
      added: report.atcContacts
        .filter((contact) => contact.origin === 'Added')
        .map((contact) => ({ callsign: contact.callsign, frequency: contact.frequency ?? '' })),
      exemptions: report.exemptions.map((exemption) => ({
        callsign: exemption.callsign,
        kind: exemption.kind,
        note: exemption.note ?? '',
      })),
    },
  };
}

/**
 * The controllers the report sends: the proposed ones the pilot kept, then the ones they added, each once. The proposed
 * ones taken away are not sent — the server works the proposal out again and writes them down as removed.
 */
export function contactsToSend(
  proposed: readonly AtcContactDto[],
  removed: readonly string[],
  added: AtcDeclarationValues['added'],
): AtcContactWriteDto[] {
  const kept = proposed
    .filter((contact) => !removed.includes(contact.callsign))
    .map((contact) => ({ callsign: contact.callsign, frequency: contact.frequency }));
  const seen = new Set(kept.map((contact) => contact.callsign));
  const mine: AtcContactWriteDto[] = [];

  for (const contact of added) {
    const callsign = contact.callsign.trim().toUpperCase();
    if (callsign !== '' && !seen.has(callsign)) {
      seen.add(callsign);
      mine.push({ callsign, frequency: contact.frequency.trim() === '' ? null : contact.frequency.trim() });
    }
  }

  return [...kept, ...mine];
}

/** The exemptions the report sends: the ones with a position chosen, the note blank when nothing was written. */
export function exemptionsToSend(exemptions: AtcDeclarationValues['exemptions']): AtcExemptionWriteDto[] {
  return exemptions
    .filter((exemption) => exemption.callsign !== '')
    .map((exemption) => ({
      callsign: exemption.callsign,
      kind: exemption.kind,
      note: exemption.note.trim() === '' ? null : exemption.note.trim(),
    }));
}

/** A report the pilot may ask to have explained (§3.10, T14b): one that was decided. */
export function isDecided(status: PirepStatus): boolean {
  return status === 'Accepted' || status === 'ToModify' || status === 'Rejected';
}

/** How a clarification cites the tour's objects, spelled as `FlightOpsReferences` reads them on the server. */
export const references = {
  pirep: (id: number) => `pirep:${id}`,
  leg: (id: number) => `leg:${id}`,
  rule: (tourId: number, ruleId: number) => `rule:${tourId}:${ruleId}`,
} as const;

/** How the page words each object it offers to cite; the page knows the language, this does not. */
export interface ChoiceWords {
  report: (report: PirepDto) => string;
  leg: (leg: PublicLegDto) => string;
  rule: (rule: PublicTourDto['rules'][number]) => string;
}

/**
 * What a pilot may cite in a clarification about a tour, in the page's order: their decided reports, newest first, the legs,
 * the rules in force.
 */
export function clarificationChoices(
  tour: Pick<PublicTourDto, 'id' | 'legs' | 'rules'>,
  reports: readonly PirepDto[],
  words: ChoiceWords,
): ChoiceOption[] {
  return [
    ...reports
      .filter((report) => isDecided(report.status))
      .map((report) => ({ value: references.pirep(report.id), label: words.report(report) })),
    ...tour.legs.map((leg) => ({ value: references.leg(leg.id), label: words.leg(leg) })),
    ...tour.rules.map((rule) => ({ value: references.rule(tour.id, rule.id), label: words.rule(rule) })),
  ];
}

/** The object the pilot came from (`?pirep=`, `?leg=`, `?rule=`), ticked already — when the page offers it at all. */
export function initialReferences(
  search: AskSearch,
  tourId: number,
  choices: readonly ChoiceOption[],
): string[] {
  const wanted = [
    search.pirep === undefined ? null : references.pirep(search.pirep),
    search.leg === undefined ? null : references.leg(search.leg),
    search.rule === undefined ? null : references.rule(tourId, search.rule),
  ];

  return choices.map((choice) => choice.value).filter((value) => wanted.includes(value));
}
