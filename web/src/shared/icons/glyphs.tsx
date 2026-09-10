import type { ReactElement } from 'react';

import { ICON_NAMES, iconByName } from './index';

/**
 * The picture of an icon of the allow list, ready to put in a page.
 *
 * ⚠️ **Drawn once and kept, never looked up while something renders.** A component read out of a
 * map inside a render is one React has to treat as new on every pass — it remounts what it draws,
 * and `react-hooks/static-components` refuses it outright. Elements are values; components are not.
 *
 * One table per set of classes, built the first time that set is asked for and kept afterwards.
 * There are two in the whole hub — the size a property form uses and the size a footer uses — and
 * making it a cache rather than two hand written tables is what keeps this a single mechanism.
 */
const TABLES = new Map<string, Readonly<Record<string, ReactElement>>>();

export function iconGlyph(name: string | null | undefined, className: string): ReactElement | null {
  if (name === null || name === undefined || name.length === 0) {
    return null;
  }

  let table = TABLES.get(className);

  if (table === undefined) {
    table = Object.fromEntries(
      ICON_NAMES.map((each) => {
        const Icon = iconByName(each)!;
        return [each, <Icon aria-hidden className={className} />];
      }),
    );

    TABLES.set(className, table);
  }

  // A name this release has never heard of draws nothing rather than a wrong picture, which is the
  // same answer `iconByName` gives and for the same reason.
  return table[name] ?? null;
}
