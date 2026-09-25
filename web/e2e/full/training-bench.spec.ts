import { expect, test } from '@playwright/test';

import { benchUrl } from './bench';

/**
 * The bench's trainee and trainer (M3, A1, design M3 §8 n.8). The training's round needs somebody who asks for a
 * training — a rating just below the one trained, and hours above any threshold — and somebody who conducts it, a member
 * of the training's staff with a rating at least as high. What the sign in answers is the row it wrote in `hub_users`,
 * so a bench whose people lost their ratings or their hours fails here, and not halfway through a round of the training.
 */

test('the bench signs in a trainee and a trainer with their ratings and their hours', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: benchUrl });

  // The pilot of the tours is the trainee: AS3 and FS3 in IVAO's numbers, the rungs just below ADC and PP.
  const trainee = await context.request.post('/e2e/signin?as=pilot');
  expect(trainee.status(), await trainee.text()).toBe(200);
  expect(await trainee.json()).toMatchObject({
    vid: 999002,
    positions: [],
    ratingAtc: 4,
    ratingPilot: 4,
    hoursAtc: 120,
    hoursPilot: 150,
  });

  // A trainer of the training department, SEC and ATP: above every rating a division trains.
  const trainer = await context.request.post('/e2e/signin?as=trainer');
  expect(trainer.status(), await trainer.text()).toBe(200);
  expect(await trainer.json()).toMatchObject({
    vid: 999004,
    positions: ['IT-T01'],
    ratingAtc: 8,
    ratingPilot: 8,
    hoursAtc: 1500,
    hoursPilot: 900,
  });

  await context.close();
});
