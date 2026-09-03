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
