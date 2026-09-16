import { expect, test } from 'vitest';

import type { AwardAssignmentWriteDto, AwardWriteDto } from './queries';
import { awardAssignmentSchema, awardSchema } from './schema';

/**
 * The two form schemas are mirrors of their write DTOs, in both directions (design M0 §7.5): a field
 * the server added and the form lacks is a field nobody can fill in, and a field the form has and the
 * server does not know is a save that quietly did nothing.
 */

const AWARD_FIELDS: readonly (keyof AwardWriteDto)[] = [
  'ownerDepartment',
  'name',
  'description',
  'criteria',
  'imageMediaId',
  'isActive',
  'rowVersion',
];

const ASSIGNMENT_FIELDS: readonly (keyof AwardAssignmentWriteDto)[] = [
  'awardId',
  'vid',
  'reason',
  'signalId',
  'rowVersion',
];

test('the award form carries exactly the fields of AwardWriteDto', () => {
  expect(Object.keys(awardSchema.shape).sort()).toEqual([...AWARD_FIELDS].sort());
});

test('the assignment form carries exactly the fields of AwardAssignmentWriteDto', () => {
  expect(Object.keys(awardAssignmentSchema().shape).sort()).toEqual([...ASSIGNMENT_FIELDS].sort());
});

test('the picture of an award is chosen from the library, and the line of the queue is never a field', () => {
  expect(awardSchema.shape.imageMediaId.meta()).toMatchObject({ media: true });
  expect(awardAssignmentSchema().shape.signalId.meta()).toMatchObject({ hidden: true });
});
