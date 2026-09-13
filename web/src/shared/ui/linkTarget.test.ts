import { expect, test } from 'vitest';

import { linkTarget } from './linkTarget';

test('an address with a query becomes a path and a search, and one without stays a path', () => {
  expect(linkTarget('/staff/content?kind=News&department=TD')).toEqual({
    to: '/staff/content',
    search: { kind: 'News', department: 'TD' },
  });

  // No `search` at all rather than an empty one: the router reads an empty object as "no parameters
  // on the new page", which would throw away the paging of a list somebody is returning to.
  expect(linkTarget('/staff/ed/calendar')).toEqual({ to: '/staff/ed/calendar' });
});
