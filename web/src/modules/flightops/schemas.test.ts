import { expect, test } from 'vitest';

import type { components } from '../../shared/api/schema';

import { tourSchema } from './schemas';

/**
 * The form of a tour mirrors `TourWriteDto` (design M0 §7.5), in both directions — with one field left out on
 * purpose: the briefing is edited by the editor of blocks (T6b), and the form sends `null`, which keeps it.
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
  'awardId',
  'rowVersion',
];

test('the tour form carries the fields of TourWriteDto but the briefing', () => {
  expect(Object.keys(tourSchema().shape).sort()).toEqual([...TOUR_FIELDS].sort());
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
