import { readFileSync, rmSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

/**
 * A recorded flight, flown again for the bench (M2, T11b).
 *
 * The flights in `tests/fixtures/ivao/` are real and they are from June: no report window reaches them, so a pilot of
 * the bench could never report one. The server is not bent to reach them — there is no clock to move and no window to
 * widen in the product. Instead the round writes a **copy** of one recorded session next to the originals, with every
 * instant moved so that it took off yesterday, under the VID the bench signs in as, and with a session identifier of
 * its own; `FixtureIvaoApiClient` reads it exactly as it reads the originals, and the copy goes away when the spec does.
 *
 * Two things are changed besides the time, both because the bench is small: the VID, and the two airports — the bench
 * knows five airports of the world (`airports-world.json`) and the recorded flight went from Florence to Geneva. The
 * route, the track and the plans are otherwise the recorded ones, which is the point of recording them.
 *
 * The copies are ignored by git (`.gitignore`): ten digit identifiers starting with 9, which no recorded session has.
 */

const fixtures = fileURLToPath(new URL('../../../tests/fixtures/ivao/', import.meta.url));

/** The flight copied: LIRQ → LXGB, three revisions of its plan, 962 points of track. */
const RECORDED = 62747397;
const RECORDED_VID = 780001;

const ISO = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$/;

export interface ReplayedFlight {
  /** The session identifier the tracker answers with. */
  readonly sessionId: number;
  /** Takes the copy back. */
  readonly remove: () => void;
}

export function replayFlight({
  vid,
  departure,
  arrival,
  hoursAgo = 24,
}: {
  vid: number;
  departure: string;
  arrival: string;
  hoursAgo?: number;
}): ReplayedFlight {
  const read = (name: string) => JSON.parse(readFileSync(`${fixtures}${name}`, 'utf8')) as unknown;

  const session = (read(`tracker-sessions-${RECORDED_VID}.json`) as { id: number; createdAt: string }[]).find(
    (entry) => entry.id === RECORDED,
  );
  if (session === undefined) {
    throw new Error(`The recorded session ${RECORDED} is not in the fixtures any more.`);
  }

  const sessionId = 9_000_000_000 + (Date.now() % 1_000_000_000);
  const shift = Date.now() - hoursAgo * 3_600_000 - Date.parse(session.createdAt);

  const move = (value: unknown, key?: string): unknown => {
    if (Array.isArray(value)) {
      return value.map((item) => move(item));
    }

    if (value !== null && typeof value === 'object') {
      return Object.fromEntries(Object.entries(value).map(([name, inner]) => [name, move(inner, name)]));
    }

    if (typeof value === 'string' && ISO.test(value)) {
      return new Date(Date.parse(value) + shift).toISOString();
    }

    switch (key) {
      case 'userId':
        return vid;
      case 'sessionId':
        return sessionId;
      case 'departureId':
      case 'departure_id':
        return departure;
      case 'arrivalId':
      case 'arrival_id':
        return arrival;
      default:
        return value;
    }
  };

  const files = {
    [`tracker-sessions-${vid}.json`]: [{ ...(move(session) as object), id: sessionId }],
    [`tracker-flightplans-${sessionId}.json`]: move(read(`tracker-flightplans-${RECORDED}.json`)),
    [`tracker-tracks-${sessionId}.json`]: move(read(`tracker-tracks-${RECORDED}.json`)),
  };

  for (const [name, content] of Object.entries(files)) {
    writeFileSync(`${fixtures}${name}`, JSON.stringify(content));
  }

  return {
    sessionId,
    remove: () => {
      for (const name of Object.keys(files)) {
        rmSync(`${fixtures}${name}`, { force: true });
      }
    },
  };
}
