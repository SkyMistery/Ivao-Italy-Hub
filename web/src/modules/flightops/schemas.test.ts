import { expect, test } from 'vitest';

import type { components } from '../../shared/api/schema';

import { allowedFromFormValues } from './api';
import { tourSchema } from './schemas';

/**
 * The form of a tour mirrors `TourWriteDto` (design M0 §7.5), in both directions — with one field left out on
 * purpose: the briefing is edited by the editor of blocks (T6b), and the form sends `null`, which keeps it. The
 * aircraft admitted (T7a) are one field of the payload and two lists of the form: the types under the payload's name,
 * so that a refusal lands on them, and the groups beside them, because a form repeats objects.
 */

type TourWriteDto = components['schemas']['TourWriteDto'];

const TOUR_FIELDS: readonly Exclude<keyof TourWriteDto, 'briefing'>[] = [
  'ownerDepartment',
  'isTemplate',
  'slug',
  'kind',
  'title',
  'summary',
  'coverMediaId',
  'bannerMediaId',
  'showPreview',
  'releaseAt',
  'closeAt',
  'reportWindowDays',
  'progression',
  'hubRotationOrder',
  'requiresProcedures',
  'dailyLegLimit',
  'minPilotRating',
  'referenceAircraftIcao',
  'requiredNm',
  'allowedAircraft',
  'awardId',
  'rowVersion',
];

test('the tour form carries the fields of TourWriteDto but the briefing, and the aircraft as two lists', () => {
  expect(Object.keys(tourSchema().shape).sort()).toEqual([...TOUR_FIELDS, 'allowedGroups'].sort());
});

test('the two lists of the form become the aircraft a tour admits, empty entries dropped', () => {
  expect(
    allowedFromFormValues(
      [{ icao: ' a320 ' }, { icao: '' }, { icao: 'A20N' }],
      [{ groupId: '7' }, { groupId: undefined }, { groupId: '' }],
    ),
  ).toEqual({ types: ['A320', 'A20N'], groupIds: [7] });
  expect(allowedFromFormValues([], [])).toEqual({ types: [], groupIds: [] });
});

test('a template carries no address, dates or award, and a public tour no longer offers its kind', () => {
  const template = tourSchema({ isTemplate: true }).shape;
  expect(template.slug.meta()).toMatchObject({ hidden: true });
  expect(template.releaseAt.meta()).toMatchObject({ hidden: true });
  expect(template.closeAt.meta()).toMatchObject({ hidden: true });
  expect(template.awardId.meta()).toMatchObject({ hidden: true });

  expect(tourSchema({ kindLocked: true }).shape.kind.meta()).toMatchObject({ hidden: true });
  expect(tourSchema().shape.kind.meta()).toMatchObject({ hidden: false });
});

test('the pictures of a tour are chosen from the library', () => {
  expect(tourSchema().shape.bannerMediaId.meta()).toMatchObject({ media: true });
  expect(tourSchema().shape.coverMediaId.meta()).toMatchObject({ media: true });
});
