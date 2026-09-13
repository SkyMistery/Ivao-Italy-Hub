import { Label, Select } from '@ivao/atmosphere-react';

import { NO_CHOICE } from '../forms';

/**
 * One filter over a list: a select with a way back to "everything", the same gesture an optional
 * field of the form generator has, because a filter you cannot clear is a filter that traps
 * whoever set it.
 *
 * It belongs to `DataList` — it is what goes in its `toolbar` — and not to the closed list of
 * components as a twenty-second: it was written twice already, in the public list and in the public
 * calendar, and the back office needed it a third time on 13 September 2026, when the department
 * left the address of its screens and became a filter (note 2026-09-13-contenuti-centralizzati).
 * The third copy is where it became one.
 */
export function ListFilter({
  id,
  label,
  none,
  value,
  onChange,
  items,
  className = 'min-w-48',
}: {
  id: string;
  label: string;
  /** What "no filter" is called, and the placeholder while nothing is chosen. */
  none: string;
  value: string | undefined;
  onChange: (value: string | undefined) => void;
  items: readonly { value: string; label: string }[];
  className?: string;
}) {
  return (
    <div className={`flex flex-col gap-1 ${className}`}>
      <Label htmlFor={id}>{label}</Label>
      <Select
        // Measured, not assumed: Atmosphere's `Select` forwards `id` to the trigger, which is what
        // makes the label above actually name it. Without it the label points at nothing and the
        // control is a button a screen reader reads as its placeholder and nothing else.
        id={id}
        {...(value === undefined ? {} : { value })}
        onValueChange={(chosen) => onChange(chosen === NO_CHOICE ? undefined : chosen)}
        placeholder={none}
        items={[{ value: NO_CHOICE, label: none }, ...items]}
      />
    </div>
  );
}
