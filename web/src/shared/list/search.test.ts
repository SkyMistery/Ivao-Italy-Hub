import { expect, test } from 'vitest';

import { listQuerySerializer, listSearchSchema, toQuery } from './search';

/**
 * The five parameters of `CrudListRequest`, and the one thing that is deliberately not in it.
 */

test('an empty search is the first page of the default size', () => {
  expect(listSearchSchema.parse({})).toEqual({ page: 1, pageSize: 25, dir: 'asc' });
});

test('the page size is capped where the engine caps it', () => {
  // Never past the cap; since issue #332, item 5, an address over it reads as the default rather than as an error screen.
  expect(listSearchSchema.parse({ pageSize: 100 }).pageSize).toBe(100);
  expect(listSearchSchema.parse({ pageSize: 500 }).pageSize).toBe(25);
});

test('a parameter written by hand that the list does not accept reads as its default, and keeps the others (issue #332, item 5)', () => {
  expect(listSearchSchema.parse({ page: 0, pageSize: 10, dir: 'sideways', sort: 7, q: 'tower' })).toEqual({
    page: 1,
    pageSize: 10,
    dir: 'asc',
    q: 'tower',
  });
});

test('a parameter with nothing in it is not sent at all', () => {
  const query = toQuery(listSearchSchema.parse({ q: '' }));

  expect(query).not.toHaveProperty('q');
  expect(query).not.toHaveProperty('sort');
});

test('a filter is spelled the way the engine reads it', () => {
  const serialize = listQuerySerializer({ ownerDepartment: 'ED' });

  expect(serialize({ page: 2, pageSize: 25, dir: 'desc', sort: 'updatedAt' })).toBe(
    'page=2&pageSize=25&dir=desc&sort=updatedAt&filter%5BownerDepartment%5D=ED',
  );
});

test('a value that is not a parameter is dropped rather than stringified', () => {
  const serialize = listQuerySerializer({});

  expect(serialize({ page: 1, nonsense: { a: 1 }, missing: undefined })).toBe('page=1');
});
