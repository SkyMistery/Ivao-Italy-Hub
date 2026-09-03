import type { Department } from './bootstrap';

/**
 * The one place that converts a department between the way a URL spells it and the way the API
 * does. `/staff/ed/links` reads well and `ED` is what the contract carries; without a single
 * converter the two spellings drift apart in a route here and a filter there (design M0 §7.3).
 *
 * The routes, the staff sidebar and `filter[ownerDepartment]` all go through this object.
 */

/**
 * Every department, in the order the division reads them. The compile time check below is what
 * keeps this list honest: it is not a copy of the contract, it is the contract's own union spelled
 * out so it can be iterated at runtime, and it stops compiling the day the server adds one.
 */
export const DEPARTMENTS = ['HQ', 'SOD', 'FOD', 'AOD', 'TD', 'MD', 'ED', 'PRD', 'WD'] as const;

type Listed = (typeof DEPARTMENTS)[number];

// Both directions on purpose: one catches a department the server added, the other one we invented.
type MissingFromList = Exclude<Department, Listed>;
type NotInContract = Exclude<Listed, Department>;
const _departmentsMatchTheContract: [MissingFromList, NotInContract] extends [never, never] ? true : never =
  true;
void _departmentsMatchTheContract;

/** True when the string is a department, which is what makes it safe to widen. */
export function isDepartment(value: string): value is Department {
  return (DEPARTMENTS as readonly string[]).includes(value);
}

/** Thrown when a URL names something that is not a department, so a route can answer 404. */
export class UnknownDepartmentError extends Error {
  constructor(readonly raw: string) {
    super(`"${raw}" is not a department of the division.`);
    this.name = 'UnknownDepartmentError';
  }
}

export const deptParam = {
  /** `"ed"` in the URL becomes `"ED"` in the contract. Anything else is not a department. */
  parse(raw: string): Department {
    const upper = raw.toUpperCase();
    if (!isDepartment(upper)) {
      throw new UnknownDepartmentError(raw);
    }
    return upper;
  },

  /** `"ED"` in the contract becomes `"ed"` in the URL. */
  format(department: Department): string {
    return department.toLowerCase();
  },
};
