import { Badge } from '@ivao/atmosphere-react';
import { useTranslation } from 'react-i18next';

/**
 * The three badges of M0. They exist so that a department, a visibility and an on/off state look
 * the same everywhere: a coordinator learns the colours once and reads every list with them.
 *
 * None of them carries a sentence — the label is an i18n key resolved here, which is why a screen
 * never writes `Public` or `Attivo` in a cell.
 *
 * They take a plain string because a list cell hands them whatever the row carries; a value the
 * design system does not know is drawn grey rather than dropped, which is what a badge should do
 * the day the server adds a fifth visibility.
 */

/** Colour per visibility, so "who can see this" is legible before the word is read. */
const VISIBILITY_COLOUR = {
  Public: 'green',
  Members: 'blue',
  Staff: 'orange',
  Department: 'purple',
} as const;

export type Visibility = keyof typeof VISIBILITY_COLOUR;

export function DepartmentBadge({ department }: { department: string }) {
  // The code is the name: IVAO's own department codes are what staff say out loud, so translating
  // them would make the badge harder to read, not easier.
  return <Badge variant="flat" color="indigo" text={department} />;
}

export function VisibilityBadge({ visibility }: { visibility: string }) {
  const { t } = useTranslation();
  const colour = VISIBILITY_COLOUR[visibility as Visibility] ?? 'gray';

  return <Badge variant="flat" color={colour} text={t(`visibility.${visibility}`)} />;
}

/** On or off, for anything a division switches: a link that is published, a module that is up. */
export function StatusBadge({ active }: { active: boolean }) {
  const { t } = useTranslation();

  return (
    <Badge
      variant="flat"
      hasDot
      color={active ? 'green' : 'gray'}
      text={active ? t('status.active') : t('status.inactive')}
    />
  );
}

/** Colour per ladder, so a controller's rating and a pilot's are told apart before they are read. */
const RATING_COLOUR = {
  Atc: 'blue',
  Pilot: 'green',
} as const;

/**
 * An IVAO rating, as text and never as one of IVAO's pictures (M3, A1, decision note of 25 September 2026). The short
 * name is the badge — ADC, PP: what the staff says out loud, like a department's code — and the full name, from the
 * core's language files, is its title and what a screen reader hears.
 *
 * It takes the short name, not IVAO's number: the server reads it out of the vocabulary of the ratings, and the browser
 * holds no copy of IVAO's list. A short name the language files do not know is still drawn, with itself as its name.
 */
export function RatingBadge({ kind, shortName }: { kind: string; shortName: string }) {
  const { t } = useTranslation();
  const name = t(`ratings.${kind}.${shortName}`, { defaultValue: shortName });
  const colour = RATING_COLOUR[kind as keyof typeof RATING_COLOUR] ?? 'gray';

  return (
    <span title={name} className="inline-flex">
      <Badge variant="flat" color={colour} text={shortName} />
      <span className="sr-only">{name}</span>
    </span>
  );
}
