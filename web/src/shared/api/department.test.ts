import { expect, test } from 'vitest';

import { DEPARTMENTS, UnknownDepartmentError, deptParam } from './department';

/**
 * The single point that converts a department between the URL and the contract. If a second one
 * ever appears, this is the test that stops meaning anything — which is why the routes, the sidebar
 * and the list filter all go through this object.
 */

test('reads a department out of the URL, however it is cased', () => {
  expect(deptParam.parse('ed')).toBe('ED');
  expect(deptParam.parse('ED')).toBe('ED');
  expect(deptParam.parse('Aod')).toBe('AOD');
});

test('writes a department into the URL in lower case', () => {
  expect(deptParam.format('ED')).toBe('ed');
  expect(DEPARTMENTS.map((department) => deptParam.format(department))).toEqual([
    'hq',
    'sod',
    'fod',
    'aod',
    'td',
    'md',
    'ed',
    'prd',
    'wd',
  ]);
});

test('round trips every department the contract declares', () => {
  for (const department of DEPARTMENTS) {
    expect(deptParam.parse(deptParam.format(department))).toBe(department);
  }
});

test('refuses something that is not a department, so a route can answer 404', () => {
  expect(() => deptParam.parse('admin')).toThrow(UnknownDepartmentError);
  expect(() => deptParam.parse('')).toThrow(UnknownDepartmentError);
});
