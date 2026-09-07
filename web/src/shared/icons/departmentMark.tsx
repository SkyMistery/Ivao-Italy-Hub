import type { ComponentType } from 'react';

import type { Department } from '../api/bootstrap';
import { DEPARTMENTS } from '../api/department';

/**
 * What stands for a department where an icon would go: its code, and nothing else.
 *
 * Decided by Carmine on 7 September 2026 (`decisions/2026-09-07-dopo-la-demo.md`). The back office
 * used to draw the same `ShieldCheck` next to all nine of them — nine identical shields carrying no
 * information, told apart only by the code written beside them. So the code becomes the mark. His
 * reason for not choosing nine icons instead: a fork that is not IVAO rewrites the `Department`
 * enum anyway, so a "department → icon" map would live inside the IVAO perimeter and cost that fork
 * work; and the code is already what the staff say out loud, while nine icons chosen at a desk
 * would be nine new conventions to learn.
 *
 * It is the same rule `DepartmentBadge` follows in a list — "the code is the name" — in the one
 * place a badge does not fit: the sidebar hands its icon a `size-9` box with `p-2` around it, and
 * an Atmosphere `Badge` has padding of its own and would not fit in what is left. So this draws
 * letters and nothing else, inheriting the colour and the ground of the box the sidebar already
 * puts around an icon. That is also what keeps it a different family from the icons of the
 * resources next to it — calendar, news, media, links: those are glyphs, this is type.
 *
 * ⚠️ Not a component of the closed list (plan §8.3): it takes no props, it is only ever mounted in
 * an icon slot, and it is built from data rather than written per department. It lives here because
 * this folder is where a mark `lucide` cannot supply is drawn by hand.
 *
 * One component per department, built once: `staffDestinations` is called on every render of the
 * back office and of the palette, and a component created inside that call would be a new type on
 * every render, remounting for nothing.
 */
export const DEPARTMENT_MARKS = Object.freeze(
  Object.fromEntries(DEPARTMENTS.map((department) => [department, markOf(department)])),
) as Readonly<Record<Department, ComponentType>>;

function markOf(department: Department): ComponentType {
  function DepartmentMark() {
    return (
      // Hidden from a screen reader because the group it marks is titled with the very same code:
      // read out, it would say "WD WD".
      <span aria-hidden className="font-head text-[0.625rem] leading-none font-semibold tracking-tight">
        {department}
      </span>
    );
  }

  DepartmentMark.displayName = `DepartmentMark(${department})`;
  return DepartmentMark;
}
