import { H2, H3, Lead, P, Subtle } from '@ivao/atmosphere-react';
import type { ComponentType, ReactNode } from 'react';

/**
 * The pieces a page is built out of. They carry no text of their own and no colour of their own:
 * everything they show is passed in, already translated, and every surface is a semantic token so
 * that dark mode is not a second design (docs/UI-GUIDELINES.md).
 */

/** The top of a public page: what this is, in one sentence, with somewhere to go next. */
export function Hero({ title, lead, actions }: { title: string; lead?: string; actions?: ReactNode }) {
  return (
    <section className="bg-card text-card-foreground border-border flex flex-col gap-4 rounded-lg border p-8">
      <H2>{title}</H2>
      {lead === undefined ? null : <Lead>{lead}</Lead>}
      {actions === undefined ? null : <div className="flex flex-wrap gap-3">{actions}</div>}
    </section>
  );
}

/** A heading inside a page, with room for the one action that belongs to that section. */
export function SectionHeader({
  title,
  description,
  actions,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
}) {
  return (
    <div className="border-border flex flex-wrap items-end justify-between gap-3 border-b pb-2">
      <div className="flex flex-col gap-1">
        <H3>{title}</H3>
        {description === undefined ? null : <Subtle>{description}</Subtle>}
      </div>
      {actions === undefined ? null : <div className="flex items-center gap-2">{actions}</div>}
    </div>
  );
}

/**
 * One number that matters. Static in M0: nothing here fetches, the value is handed to it, so the
 * dashboard of M1 can feed it from a widget without this component learning where data comes from.
 */
export function StatTile({
  label,
  value,
  hint,
  Icon,
}: {
  label: string;
  value: string;
  hint?: string;
  Icon?: ComponentType<{ className?: string }>;
}) {
  return (
    <div className="bg-card text-card-foreground border-border flex items-center gap-4 rounded-lg border p-4">
      {Icon === undefined ? null : (
        <span className="bg-muted text-muted-foreground rounded-md p-2">
          <Icon className="size-5" />
        </span>
      )}
      <div className="flex flex-col">
        <Subtle>{label}</Subtle>
        <span className="text-2xl font-semibold tabular-nums">{value}</span>
        {hint === undefined ? null : <Subtle>{hint}</Subtle>}
      </div>
    </div>
  );
}

/**
 * Nothing here yet — and, whenever there is something to be done about it, the way to do it. An
 * empty list that only says "no results" is a dead end.
 */
export function EmptyState({
  title,
  description,
  action,
  Icon,
}: {
  title: string;
  description?: string;
  action?: ReactNode;
  Icon?: ComponentType<{ className?: string }>;
}) {
  return (
    <div className="border-border flex flex-col items-center gap-3 rounded-lg border border-dashed p-10 text-center">
      {Icon === undefined ? null : <Icon className="text-muted-foreground size-8" />}
      <H3>{title}</H3>
      {description === undefined ? null : <P className="text-muted-foreground max-w-prose">{description}</P>}
      {action}
    </div>
  );
}
