import { request, type FullConfig } from '@playwright/test';

import { runJob } from './bench';

/**
 * Before the first spec, the whole snapshot of the reference data: airports, aircraft types, ATC positions, FRAs.
 *
 * A fresh installation's start downloads only the centres (issue #325, item 7; note
 * `decisions/2026-11-18-il-primo-allineamento-sotto-blocco.md`, the maintainer's answer «B, solo i centri»): the rest is the
 * `ref-data-sync` job's, at the first round of the jobs after the start. The bench in CI starts on an empty database and
 * its specs write events and tours between Rome and Milan at once, so it asks for the job now, as a spec asks for any other
 * (`runJob`), instead of waiting for the round. On a bench database that already has the snapshot, the run refreshes it.
 *
 * `runJob` answers when the run has **ended**, and refuses a run that did not happen. A run that happened and did not
 * succeed — a fixture missing, IVAO's client reading nothing — stops the round here, with the job's own words, rather than
 * as dozens of specs refused one by one for an airport nobody knows.
 */
export default async function globalSetup(config: FullConfig): Promise<void> {
  const baseURL = config.projects[0]?.use.baseURL;
  if (!baseURL) {
    throw new Error('The full round has no baseURL: playwright.full.config.ts gives it one.');
  }

  const context = await request.newContext({ baseURL });
  try {
    const ran = await runJob(context, 'ref-data-sync');
    if (ran.run?.status !== 'succeeded' || ran.run.finishedAt === null) {
      throw new Error(
        `The reference data the round needs could not be synchronised, so no spec runs: ref-data-sync ended ` +
          `${ran.run ? `"${ran.run.status}" (${ran.run.message ?? 'no message'})` : 'without a row in hub_jobs_log'}.`,
      );
    }
  } finally {
    await context.dispose();
  }
}
