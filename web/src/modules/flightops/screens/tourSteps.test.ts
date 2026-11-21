import { expect, test } from 'vitest';

import { tourEditorSearchSchema } from '../schemas';

import {
  neighbours,
  openStep,
  routeParts,
  stepOfField,
  stepOfProblem,
  stepsToFix,
  tourSteps,
} from './tourSteps';

test('a tour has the six steps in the order it is written, a template no check and no route but an Open goal', () => {
  expect(tourSteps({ kind: 'Free', isTemplate: false })).toEqual([
    'kind',
    'identity',
    'route',
    'rules',
    'briefing',
    'check',
  ]);
  expect(tourSteps({ kind: 'Free', isTemplate: true })).toEqual(['kind', 'identity', 'rules', 'briefing']);
  expect(tourSteps({ kind: 'Open', isTemplate: true })).toEqual([
    'kind',
    'identity',
    'route',
    'rules',
    'briefing',
  ]);
});

test('the route holds the legs, the hubs, the subtours or the goal, by kind', () => {
  expect(routeParts({ kind: 'Sequential', isTemplate: false })).toEqual(['legs']);
  expect(routeParts({ kind: 'Distance', isTemplate: false })).toEqual(['legs']);
  expect(routeParts({ kind: 'Hub', isTemplate: false })).toEqual(['hubs', 'legs']);
  expect(routeParts({ kind: 'Container', isTemplate: false })).toEqual(['subtours']);
  expect(routeParts({ kind: 'Open', isTemplate: false })).toEqual(['goal']);
  expect(routeParts({ kind: 'Open', isTemplate: true })).toEqual(['goal']);
  expect(routeParts({ kind: 'Hub', isTemplate: true })).toEqual([]);
});

// The fields are the ones the server files its problems under (`TourReadiness.ProblemsAsync`, `TourShape.Problems`).
test('every problem of "ready" sends the staff to the step its field is drawn in', () => {
  expect(stepOfField('kind')).toBe('kind');
  for (const field of [
    'title',
    'summary',
    'slug',
    'releaseAt',
    'closeAt',
    'referenceAircraftIcao',
    'awardId',
  ]) {
    expect(stepOfField(field)).toBe('identity');
  }
  for (const field of [
    'legs',
    'legs.3',
    'hubs',
    'hubs.LIRF',
    'rotations.LIRF.2',
    'subtours',
    'openGoal',
    'openGoalParameters.nm',
  ]) {
    expect(stepOfField(field)).toBe('route');
  }
  for (const field of [
    'dailyLegLimit',
    'requiredNm',
    'requiredSubtours',
    'constraints',
    'allowedAircraft[0].icao',
  ]) {
    expect(stepOfField(field)).toBe('rules');
  }
  expect(stepOfField('briefing.sections[0].blocks[1].props.text')).toBe('briefing');
  expect(stepOfField('status')).toBe('check');
});

test('a problem of a step the tour lacks is shown at the check, and the steps to fix are told apart', () => {
  const template = tourSteps({ kind: 'Free', isTemplate: true });
  expect(stepOfProblem('legs', template)).toBe('check');

  const tour = tourSteps({ kind: 'Hub', isTemplate: false });
  expect([...stepsToFix(['title', 'legs.2', 'hubs.LIRF', 'dailyLegLimit'], tour)].sort()).toEqual([
    'identity',
    'route',
    'rules',
  ]);
  expect(stepsToFix([], tour).size).toBe(0);
});

test('the steps around one are those the tour has', () => {
  const template = tourSteps({ kind: 'Free', isTemplate: true });
  expect(neighbours(template, 'identity')).toEqual({ previous: 'kind', next: 'rules' });
  expect(neighbours(template, 'kind')).toEqual({ previous: null, next: 'identity' });
  expect(neighbours(template, 'briefing')).toEqual({ previous: 'rules', next: null });
});

test('a new tour opens at its kind, then at its name; one that exists at its name, or at the step asked', () => {
  const steps = tourSteps({ kind: 'Free', isTemplate: false });
  expect(openStep({ asked: undefined, steps, isNew: true, kindChosen: false })).toBe('kind');
  expect(openStep({ asked: undefined, steps, isNew: true, kindChosen: true })).toBe('identity');
  expect(openStep({ asked: undefined, steps, isNew: false, kindChosen: false })).toBe('identity');
  expect(openStep({ asked: 'rules', steps, isNew: false, kindChosen: false })).toBe('rules');

  const template = tourSteps({ kind: 'Free', isTemplate: true });
  expect(openStep({ asked: 'route', steps: template, isNew: false, kindChosen: false })).toBe('identity');
});

test('an address written before the steps lands on the step that holds what it named', () => {
  const tab = (value: string) => tourEditorSearchSchema.parse({ tab: value }).tab;
  expect(tab('settings')).toBe('identity');
  expect(tab('legs')).toBe('route');
  expect(tab('hubs')).toBe('route');
  expect(tab('subtours')).toBe('route');
  expect(tab('open')).toBe('route');
  expect(tab('callsigns')).toBe('rules');
  expect(tab('rules')).toBe('rules');
  expect(tab('nonsense')).toBeUndefined();
  expect(tourEditorSearchSchema.parse({ kind: 'Hub' }).kind).toBe('Hub');
  expect(tourEditorSearchSchema.parse({ kind: 'Plane' }).kind).toBeUndefined();
});
