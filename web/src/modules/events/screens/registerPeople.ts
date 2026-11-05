import type { TFunction } from 'i18next';

import { isErased, personName, type NamedPerson } from '../../../shared/ui';

/** The people of the registers (design M4 §3.7, §4.5, E13b) as their pages link and title them: plain TypeScript, read by a test. */

/**
 * The page of one person in a register, or none for a person whose data was erased (#333, item 2): their rows of the events that are over
 * stay, under the pseudonym, and there is nobody left to open.
 */
export function registerHref(register: string, vid: number): string | null {
  return isErased(vid) ? null : `${register}/${String(vid)}`;
}

/**
 * The title of one person's page in a register: their name as the register lists it, and before the list answers — or when it has no row
 * of theirs — the person of the address, never a pseudonym as a number (#333, item 2).
 */
export function registerTitle(vid: number, person: NamedPerson | undefined, t: TFunction): string {
  return personName(person ?? { vid, name: null }, t);
}
