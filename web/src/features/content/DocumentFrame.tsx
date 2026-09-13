import { Button } from '@ivao/atmosphere-react';
import { Link } from '@tanstack/react-router';
import { Printer } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { useLocalized } from '../../shared/i18n/useLocalized';
import { useMoment } from '../../shared/i18n/useMoment';
import { Notice } from '../../shared/ui';

import { dayInstant, dayOf } from './documentDays';
import type { PublicContentDto } from './queries';

/**
 * What the public screen draws around the body of a document (G14): a strip of facts under the
 * title, a notice when the document is not — or not yet — in force, and a footer that says which
 * edition this is. Three pieces of the **screen**, and none of the renderer: the renderer draws
 * sections and blocks, and a document is a row with columns around the same body (design M1 §3.1).
 *
 * Until 13 September 2026 the strip also carried a type, two positions, an airport and a FIR, and
 * the footer an AIRAC cycle: that half left with vIPI (note `2026-09-13-staccarsi-da-vipi`).
 */

/** The facts under the title: in force from. Nothing at all when the document says nothing. */
export function DocumentStrip({ content }: { content: PublicContentDto }) {
  const { t } = useTranslation();
  const moment = useMoment();

  const facts: Array<{ key: string; label: string; value: string }> = [];

  const push = (key: string, value: string | null) => {
    if (value !== null && value !== '') {
      facts.push({ key, label: t(`documents.public.${key}`), value });
    }
  };

  push(
    'effectiveOn',
    content.effectiveOn === null ? null : moment(dayInstant(content.effectiveOn), { time: false }),
  );

  if (facts.length === 0) {
    return null;
  }

  return (
    <dl className="border-border grid grid-cols-2 gap-x-6 gap-y-2 rounded-lg border p-4 text-sm sm:grid-cols-3">
      {facts.map((fact) => (
        <div key={fact.key} className="flex flex-col">
          <dt className="text-muted-foreground text-xs uppercase tracking-wide">{fact.label}</dt>
          <dd className="font-medium">{fact.value}</dd>
        </div>
      ))}
    </dl>
  );
}

/**
 * The notice on top when the document is not the one to follow: retired, with the way on when
 * something replaced it; or not yet in force, when its date is still ahead. A retired document
 * stays readable on purpose — somebody arriving from an old link needs the successor, not a 404.
 */
export function DocumentNotice({ content, now = new Date() }: { content: PublicContentDto; now?: Date }) {
  const { t } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();

  if (content.retiredAt !== null) {
    const since = moment(dayInstant(content.retiredAt), { time: false });
    const successor =
      content.supersededBySlug === null ? null : (
        <Link to="/documents/$slug" params={{ slug: content.supersededBySlug }} className="underline">
          {read(content.supersededByTitle) || content.supersededBySlug}
        </Link>
      );

    return (
      <Notice
        tone="warning"
        title={t('documents.public.retired', { date: since })}
        {...(successor === null
          ? {}
          : {
              description: (
                <span>
                  {t('documents.public.supersededBy')} {successor}
                </span>
              ),
            })}
      />
    );
  }

  // Days compared as days: the document comes into force on a date, in no time zone in particular.
  if (content.effectiveOn !== null && dayOf(content.effectiveOn) > dayOf(now.toISOString())) {
    return (
      <Notice
        tone="info"
        title={t('documents.public.notYetInForce', {
          date: moment(dayInstant(content.effectiveOn), { time: false }),
        })}
      />
    );
  }

  return null;
}

/** The edition: version, date, who — and the way to paper. */
export function DocumentFooter({ content }: { content: PublicContentDto }) {
  const { t } = useTranslation();
  const moment = useMoment();

  return (
    <footer className="border-border text-muted-foreground flex flex-wrap items-center justify-between gap-3 border-t pt-4 text-sm">
      <dl className="flex flex-wrap gap-x-6 gap-y-1">
        <div className="flex gap-1">
          <dt>{t('documents.public.version')}</dt>
          <dd className="text-foreground font-medium tabular-nums">{content.version}</dd>
        </div>
        <div className="flex gap-1">
          <dt>{t('documents.public.publishedOn')}</dt>
          <dd className="text-foreground font-medium">{moment(content.publishedAt, { time: false })}</dd>
        </div>
        {content.publishedByName === null ? null : (
          <div className="flex gap-1">
            <dt>{t('documents.public.publishedBy')}</dt>
            <dd className="text-foreground font-medium">{content.publishedByName}</dd>
          </div>
        )}
      </dl>
      {/* Hidden on the paper it makes: a button to print, printed, is a picture of a button. */}
      <Button
        type="button"
        variant="outline"
        size="sm"
        className="print:hidden"
        onClick={() => window.print()}
      >
        <Printer aria-hidden className="mr-2 size-4" />
        {t('documents.public.print')}
      </Button>
    </footer>
  );
}
