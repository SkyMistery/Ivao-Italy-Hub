import type { Resizable } from '../shared/modules';

/**
 * The width of a block that has one of its own in its column (`BlockRegistration.resizable`, design of the editor's
 * phase 3, §5.3), read once for the renderer, the handle on its edge, the panel and the import, so that the four never
 * disagree about a body nobody has rewritten. The three functions are the alignment's (`alignOf`, `carriesOldAlign`,
 * `placedAt` in `envelope.ts`) for a property instead of the envelope.
 */

const isWidth = (resizable: Resizable, value: unknown): value is number =>
  typeof value === 'number' && Number.isInteger(value) && value >= resizable.min && value <= resizable.max;

/**
 * Whether the props still say the width the way a body written before the property did — the picture's `width` —
 * and not yet the property itself.
 */
export function carriesOldSize(resizable: Resizable, props: Record<string, unknown>): boolean {
  const old = resizable.old;
  return (
    old !== undefined &&
    !Object.hasOwn(props, resizable.prop) &&
    typeof props[old.prop] === 'string' &&
    Object.hasOwn(old.values, props[old.prop] as string)
  );
}

/**
 * The width a block is drawn at, in percent of its column: its property, or else what an older body wrote in its place
 * (a third of the column is 33), or else the whole column. A value outside the limits is no width at all.
 */
export function sizeOf(resizable: Resizable, props: Record<string, unknown>): number {
  const own = props[resizable.prop];
  if (isWidth(resizable, own)) {
    return own;
  }

  if (resizable.old !== undefined && carriesOldSize(resizable, props)) {
    return resizable.old.values[props[resizable.old.prop] as string] ?? resizable.max;
  }

  return resizable.max;
}

/**
 * The props of a block at `width`: the property written, and the older way of saying it taken out, so that the old one
 * can never come back from under the new.
 */
export function sizedAt(
  resizable: Resizable,
  props: Record<string, unknown>,
  width: number,
): Record<string, unknown> {
  const old = resizable.old?.prop;
  return {
    ...Object.fromEntries(Object.entries(props).filter(([key]) => key !== old)),
    [resizable.prop]: width,
  };
}

/** A width the pointer or the keyboard asked for, on the nearest step and within the limits. */
export function snapped(resizable: Resizable, width: number): number {
  const stepped = Math.round(width / resizable.step) * resizable.step;
  return Math.min(resizable.max, Math.max(resizable.min, stepped));
}
