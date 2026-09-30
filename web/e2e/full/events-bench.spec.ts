import { expect, test } from '@playwright/test';

import { englishSeed } from '../locales';

import { benchUrl } from './bench';

/**
 * The bench's coordinator of the events, and the words of the events in the calendar (M4, E1, note
 * `decisions/2026-09-30-i-tipi-degli-eventi-e-l-ed-sul-banco.md`). The web master of the bench reaches every department
 * and holds every permission of every module, so the round of the events signs in as somebody who holds only what the
 * division gives the events department. And the type of an event is a word of the calendar, which the bootstrap carries
 * to every select and every chip: the form of an event takes its type from there. What the sign in answers is the row it
 * wrote, so a bench that lost its coordinator fails here, and not halfway through a round of the events.
 */

/** The four words of the events, by key, with the English word of the seed's own language file. */
const theEvents = [
  ['rfe', englishSeed.seed.calendarKinds.rfe],
  ['rfo', englishSeed.seed.calendarKinds.rfo],
  ['mse', englishSeed.seed.calendarKinds.mse],
  ['online-day', englishSeed.seed.calendarKinds.onlineDay],
] as const;

interface Bootstrap {
  readonly user: {
    readonly vid: number;
    readonly positions: readonly string[];
    readonly hasAllDepartments: boolean;
    readonly departments: readonly string[];
  } | null;
  readonly calendarKinds: readonly {
    readonly key: string;
    readonly label: Readonly<Record<string, string>>;
    readonly colour: string;
  }[];
}

test('the bench signs in a coordinator of the events, and its bootstrap offers the words of the events', async ({
  browser,
}) => {
  const context = await browser.newContext({ baseURL: benchUrl });

  const coordinator = await context.request.post('/e2e/signin?as=events');
  expect(coordinator.status(), await coordinator.text()).toBe(200);
  expect(await coordinator.json()).toMatchObject({ vid: 999005, positions: ['IT-EC'] });

  const me = await context.request.get('/api/me');
  expect(me.status(), await me.text()).toBe(200);
  const bootstrap = (await me.json()) as Bootstrap;

  // A coordinator of the events department and of nothing else: unlike the web master, it does not reach every
  // department, which is the whole reason it is on the bench.
  expect(bootstrap.user).toMatchObject({
    vid: 999005,
    positions: ['IT-EC'],
    hasAllDepartments: false,
    departments: ['ED'],
  });

  // The words of the events, each in every language of the division — in English the word of the seed's own file, and
  // in Italian a word rather than the key of one.
  for (const [key, english] of theEvents) {
    const kind = bootstrap.calendarKinds.find((candidate) => candidate.key === key);
    expect(kind, `the calendar kind ${key}`).toBeDefined();
    expect(kind?.label.en).toBe(english);
    expect(kind?.label.it ?? '').not.toBe('');
    expect(kind?.label.it).not.toContain('seed.calendarKinds');
  }

  await context.close();
});
