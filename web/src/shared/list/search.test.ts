import { expect, test } from 'vitest';

import { listQuerySerializer, listSearchSchema, toQuery } from './search';

/**
 * The five parameters of `CrudListRequest`, and the one thing that is deliberately not in it.
 */

test('an empty search is the first page of the default size', () => {
  expect(listSearchSchema.parse({})).toEqual({ page: 1, pageSize: 25, dir: 'asc' });
});

test('the page size is capped where the engine caps it', () => {
  expect(listSearchSchema.safeParse({ pageSize: 500 }).success).toBe(false);
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
