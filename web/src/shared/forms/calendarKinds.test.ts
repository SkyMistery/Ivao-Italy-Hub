import { expect, test } from 'vitest';

import { calendarSchema, linkListSchema } from '../../blocks/schemas';

import { chooseCalendarKinds, namesCalendarKinds, readFields, type FieldNode } from './schema';

/**
 * The calendar block's kinds are chosen from the calendar's own list, never typed (issue #360). The
 * kinds are rows, so the schema only says what the field holds and the editor hands the choices in.
 */

const kinds = [
  { value: 'meeting', label: 'Meeting' },
  { value: 'exam', label: 'Exam' },
];

function kindField(fields: FieldNode[]): FieldNode | undefined {
  const list = fields.find((field) => field.path === 'kinds');
  return list?.kind === 'list' ? list.children.find((child) => child.path === 'kinds.kind') : undefined;
}

test('a kind inside the list of a calendar block becomes a select of the kinds handed in', () => {
  expect(namesCalendarKinds(calendarSchema)).toBe(true);

  const field = kindField(readFields(chooseCalendarKinds(calendarSchema, kinds)));

  expect(field).toMatchObject({ kind: 'text', choices: kinds });
});

test('the schema of the registry is left as it was, and a block without kinds is not touched', () => {
  chooseCalendarKinds(calendarSchema, kinds);

  // Typed by nobody, but not closed either until somebody hands the choices in: free text.
  expect(kindField(readFields(calendarSchema))).toMatchObject({ kind: 'text', choices: null });
  expect(namesCalendarKinds(linkListSchema)).toBe(false);
});

test('the other properties keep what they were', () => {
  const before = readFields(calendarSchema).map((field) => [field.path, field.kind]);
  const after = readFields(chooseCalendarKinds(calendarSchema, kinds)).map((field) => [
    field.path,
    field.kind,
  ]);

  expect(after).toEqual(before);
});
