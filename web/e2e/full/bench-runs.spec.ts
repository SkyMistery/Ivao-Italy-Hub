import { expect } from '@playwright/test';

import { giveSessions, runJob, takeSessions, test } from './bench';

/**
 * What the bench can ask besides signing in (M4, E10w; note `decisions/2026-10-18-il-banco-fa-girare-un-job.md`): a job run now,
 * and sessions on the network for one of its people. A round that needs a job — `events-roster` proposing and publishing a
 * roster, `events-after` checking an evening — no longer waits for the job's hour, and a report of support of a person of the
 * bench has something to be checked against. This spec watches the two addresses on the published bench; a round uses them.
 */

const pilotVid = 999002;

test('the bench gives one of its people sessions on the network and runs a job now', async ({
  request,
  afterwards,
}) => {
  // Three hours ago, to the minute: the tower first, then the flight from Rome to Milan.
  const at = new Date(Date.now() - 3 * 3_600_000);
  at.setUTCSeconds(0, 0);
  const later = (seconds: number) => new Date(at.getTime() + seconds * 1000).toISOString();

  afterwards(() => takeSessions(request, pilotVid));
  const sessions = await giveSessions(request, pilotVid, at);

  expect(
    sessions.map(({ callsign, connection, startedAt, endedAt, departureIcao, arrivalIcao }) => ({
      callsign,
      connection,
      startedAt: new Date(startedAt).toISOString(),
      endedAt: new Date(endedAt).toISOString(),
      departureIcao,
      arrivalIcao,
    })),
  ).toEqual([
    {
      callsign: 'LIRF_TWR',
      connection: 'Atc',
      startedAt: at.toISOString(),
      endedAt: later(2454),
      departureIcao: null,
      arrivalIcao: null,
    },
    {
      callsign: 'TST001',
      connection: 'Pilot',
      startedAt: later(3008),
      endedAt: later(3008 + 4312),
      departureIcao: 'LIRF',
      arrivalIcao: 'LIMC',
    },
  ]);

  // A job of the core, run now whatever its hour: the answer leaves with its row ended.
  const ran = await runJob(request, 'media-expiry');
  expect(ran.run).toMatchObject({ status: 'succeeded' });
  expect(ran.run?.finishedAt).not.toBeNull();

  // A job the hub has none of, and a VID that is none of the bench's people.
  expect((await request.post('/e2e/jobs/no-such-job')).status()).toBe(404);
  expect((await request.put('/e2e/sessions/761020', { data: { atUtc: at.toISOString() } })).status()).toBe(
    404,
  );
});
