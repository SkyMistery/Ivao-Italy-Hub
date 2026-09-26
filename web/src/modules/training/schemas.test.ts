import { expect, test } from 'vitest';

import type { components } from '../../shared/api/schema';
import { readFields } from '../../shared/forms';

import {
  EMPTY_REQUEST,
  emptySheetItem,
  ratingChoice,
  requestFromFormValues,
  requestSchema,
  requestSearchSchema,
  settingsFromFormValues,
  settingsSchema,
  settingsToFormValues,
  sheetItemFilters,
  sheetItemFromFormValues,
  sheetItemSchema,
  sheetItemToFormValues,
  sheetItemsSearchSchema,
  type TrainingSettings,
} from './schemas';

/**
 * The form of the settings mirrors `TrainingSettings` (design M3 §1.6), which the core keeps as the module's own JSON and
 * the contract therefore does not describe: so the two are held together here, field by field and value by value. The
 * rating of a threshold is the one field that changes shape on the way, a choice carrying its ladder with its number; and
 * so is the rating of an item of the evaluation sheet (A5), whose form is held to the contract below.
 */

const saved: TrainingSettings = {
  minimumHours: [
    { kind: 'Atc', rating: 3, hours: 40 },
    { kind: 'Pilot', rating: 3, hours: 25 },
  ],
  cooldownDays: 5,
  noShowCooldownDays: 14,
  maxResponseDays: 10,
  responseReminderDays: 3,
  conflictPolicy: 'Block',
  conflictKinds: ['event', 'meeting'],
  reminderLeadHours: 24,
  hiddenPositions: ['XX_TWR'],
  theoryExamUrl: 'https://exam.example.org/theory',
};

const known = ['event', 'meeting'];

test('the form has a field for every setting, and nothing else', () => {
  const fields = readFields(settingsSchema({ ratings: [], kinds: [], positions: [] })).map(
    (field) => field.path,
  );

  expect(fields.sort()).toEqual(Object.keys(saved).sort());
});

test('the settings come back from the form as they went in', () => {
  expect(settingsFromFormValues(settingsToFormValues(saved, known))).toEqual(saved);
});

test('the same rung of the two ladders is two different choices', () => {
  const values = settingsToFormValues(saved, known);

  expect(values.minimumHours.map((row) => row.rating)).toEqual([
    ratingChoice('Atc', 3),
    ratingChoice('Pilot', 3),
  ]);
  expect(new Set(values.minimumHours.map((row) => row.rating)).size).toBe(2);
});

test('nothing chosen is sent as nothing: no time limit, and no site of the exam', () => {
  const values = settingsToFormValues({ ...saved, maxResponseDays: null, theoryExamUrl: null }, known);

  expect(values.maxResponseDays).toBeUndefined();
  expect(values.theoryExamUrl).toBe('');

  const back = settingsFromFormValues({ ...values, theoryExamUrl: '   ' });
  expect(back.maxResponseDays).toBeNull();
  expect(back.theoryExamUrl).toBeNull();
});

test('a kind the calendar no longer has is left out, because the form has no box to take it off with', () => {
  expect(settingsToFormValues(saved, ['event']).conflictKinds).toEqual(['event']);
});

// ---- the evaluation sheet (A5) ------------------------------------------------------------------------------------------

const item: components['schemas']['SheetItemDto'] = {
  id: 7,
  ownerDepartment: 'TD',
  kind: 'Pilot',
  rating: 3,
  section: 'Theory',
  title: { it: 'Fraseologia', en: 'Phraseology' },
  sort: 4,
  isActive: false,
  updatedAt: '2026-09-26T10:00:00Z',
  rowVersion: '2026-09-26T10:00:00.123456Z',
};

test('the form of an item has a field for every value a client writes, the ladder inside the rating', () => {
  const fields = readFields(sheetItemSchema([])).map((field) => field.path);
  const written: readonly (keyof components['schemas']['SheetItemWriteDto'])[] = [
    'kind',
    'rating',
    'section',
    'title',
    'sort',
    'isActive',
    'rowVersion',
  ];

  expect(fields.sort()).toEqual(written.filter((key) => key !== 'kind').sort());
});

test('an item comes back from the form as it went in, in every language of the division', () => {
  const values = sheetItemToFormValues(item, ['it', 'en']);

  expect(values.rating).toBe(ratingChoice('Pilot', 3));
  expect(sheetItemFromFormValues(values)).toEqual({
    kind: 'Pilot',
    rating: 3,
    section: 'Theory',
    title: { it: 'Fraseologia', en: 'Phraseology' },
    sort: 4,
    isActive: false,
    rowVersion: '2026-09-26T10:00:00.123456Z',
  });
});

test('a language the item misses is an empty field, for somebody to write', () => {
  expect(sheetItemToFormValues({ ...item, title: { en: 'Phraseology' } }, ['it', 'en']).title).toEqual({
    it: '',
    en: 'Phraseology',
  });
});

test('a new item starts on the sheet it was asked from, in the place after its last item, and active', () => {
  expect(emptySheetItem(ratingChoice('Atc', 5), 3, ['it', 'en'])).toMatchObject({
    rating: 'Atc:5',
    section: 'Practice',
    title: { it: '', en: '' },
    sort: 3,
    isActive: true,
  });
});

test('the list is narrowed to a ladder, and to a rating only with its ladder', () => {
  expect(sheetItemFilters({})).toEqual({});
  expect(sheetItemFilters({ kind: 'Atc' })).toEqual({ kind: 'Atc' });
  expect(sheetItemFilters({ kind: 'Atc', rating: 5 })).toEqual({ kind: 'Atc', rating: '5' });
  // The two ladders number their rungs alike: a number alone says no rating.
  expect(sheetItemFilters({ rating: 5 })).toEqual({});
});

test('the address of the list reads the ladder and the rating as the link wrote them', () => {
  expect(sheetItemsSearchSchema.parse({ kind: 'Pilot', rating: '3' })).toMatchObject({
    kind: 'Pilot',
    rating: 3,
    page: 1,
  });
});

// ---- the request (A6) ---------------------------------------------------------------------------------------------------

const offered = [{ value: 'XXAA_TWR', label: 'XXAA_TWR — Example Tower' }];

test('the form of a request has a field for every value the trainee writes; the ladder, the rating and the answer are the page', () => {
  const fields = readFields(requestSchema(offered)).map((field) => field.path);
  const written: readonly (keyof components['schemas']['TrainingRequestWriteDto'])[] = [
    'kind',
    'rating',
    'position',
    'availabilityText',
    'notesText',
    'theoryPassed',
  ];

  expect(fields.sort()).toEqual(
    written.filter((key) => key !== 'kind' && key !== 'rating' && key !== 'theoryPassed').sort(),
  );
  expect(Object.keys(EMPTY_REQUEST).sort()).toEqual(fields);
});

test('the position is chosen among the ones offered and nothing else, and on a ladder without positions it is never drawn', () => {
  const [position] = readFields(requestSchema(offered));
  expect(position).toMatchObject({ path: 'position', kind: 'suggest', only: true, suggestions: offered });

  const [hidden] = readFields(requestSchema(null));
  expect(hidden).toMatchObject({ path: 'position', meta: { hidden: true } });
});

test('a request goes with its ladder, the rating proposed and the answer, and an empty box as nothing', () => {
  expect(
    requestFromFormValues(
      { position: ' XXAA_TWR ', availabilityText: 'Evenings, UTC+2.\n', notesText: '   ' },
      { kind: 'Atc', number: 5 },
      false,
    ),
  ).toEqual({
    kind: 'Atc',
    rating: 5,
    position: 'XXAA_TWR',
    availabilityText: 'Evenings, UTC+2.',
    notesText: null,
    theoryPassed: false,
  });

  // A ladder without positions, and no question asked: nothing to answer.
  expect(requestFromFormValues(EMPTY_REQUEST, { kind: 'Pilot', number: 2 }, null)).toEqual({
    kind: 'Pilot',
    rating: 2,
    position: null,
    availabilityText: null,
    notesText: null,
    theoryPassed: null,
  });
});

test('the address of the request reads the ladder a link chose, and only a ladder', () => {
  expect(requestSearchSchema.parse({ kind: 'Pilot' })).toEqual({ kind: 'Pilot' });
  expect(requestSearchSchema.parse({})).toEqual({});
  expect(requestSearchSchema.safeParse({ kind: 'Glider' }).success).toBe(false);
});
