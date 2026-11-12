import { expect, test } from 'vitest';

import { ApiError } from './problem';
import { retryQuery } from './retry';

test('an answer about the request is not asked again, a failure of the server or the network once (issue #332, item 5)', () => {
  for (const status of [400, 401, 403, 404, 409, 429]) {
    expect(retryQuery(0, new ApiError(status, undefined))).toBe(false);
  }

  expect(retryQuery(0, new ApiError(500, undefined))).toBe(true);
  expect(retryQuery(1, new ApiError(503, undefined))).toBe(false);

  expect(retryQuery(0, new TypeError('Failed to fetch'))).toBe(true);
  expect(retryQuery(1, new TypeError('Failed to fetch'))).toBe(false);
});
