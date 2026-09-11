import { AlertDescription, AlertRoot, AlertTitle } from '@ivao/atmosphere-react';
import type { ReactNode } from 'react';

import { NOTICE_TONES, type NoticeTone } from './notices';

/**
 * One thing said to the person using the hub, in one of four tones: something went wrong, something
 * is worth their attention, something worked, something is worth knowing.
 *
 * The **fifth** component of the closed list (plan §8.3), and the first added since M1 closed with
 * the four it predicted. Carmine asked for it after running the demo: an editor that saved said
 * nothing, and an action that went nowhere said nothing either — which is how a section was lost
 * while copying a page across by hand, without anybody noticing.
 *
 * ⚠️ It is not `ProblemAlert`, and does not replace it. That one draws what the **server refused**,
 * field by field, out of a `ProblemDetails`; this one is a sentence somebody wrote, in any of four
 * tones, anywhere. Whether the two ever become one is a change that touches every screen of the
 * back office, and Carmine chose not to make it now.
 *
 * The tones, and the confirmation that says the same thing in the corner of the screen and then
 * goes, are in `notices.ts` beside this file — the two halves read one table, so they cannot drift.
 */

export function Notice({
  tone,
  title,
  description,
  className = '',
}: {
  tone: NoticeTone;
  /** The sentence. Already in the language on screen: this never translates anything. */
  title: string;
  description?: ReactNode;
  className?: string;
}) {
  const { variant, className: toned, Icon, role } = NOTICE_TONES[tone];

  return (
    <AlertRoot variant={variant} role={role} className={`${toned} ${className}`.trim()}>
      {/* A direct `svg` child, because that is what the variant positions: Atmosphere's own alert
          classes place `[&>svg]` in the corner and indent everything after it. */}
      <Icon aria-hidden className="size-4" />
      <AlertTitle>{title}</AlertTitle>
      {description === undefined ? null : <AlertDescription>{description}</AlertDescription>}
    </AlertRoot>
  );
}
