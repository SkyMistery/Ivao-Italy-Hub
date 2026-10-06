import { Badge, CardRoot, H4, Subtle } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { bootstrapQuery } from '../../../features/me/queries';
import { mediaFileUrl } from '../../../shared/api/mediaUrl';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { calendarKindColour } from '../../../shared/ui';

import { STATE_COLOURS, eventHref, spanText, type EventCard } from './cards';

/**
 * The cards of the events (design M4 §7.1, §7.3, E4): the banner, the state and the kind, the title, the summary, when, and the
 * airports. Written once and drawn twice — by `/events` and by the block `events.eventList` — because they are the same cards, and
 * a second copy is a second thing to keep in step. A card is the same for whoever is looking.
 */

export function EventCards({ cards }: { cards: readonly EventCard[] }) {
  const { t } = useTranslation();
  const read = useLocalized();
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const kinds = bootstrap?.calendarKinds ?? [];

  return (
    <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
      {cards.map((card) => {
        const summary = read(card.summary);

        return (
          <article key={card.id} className="h-full">
            <RouterAnchor href={eventHref(card.slug)} className="block h-full">
              <CardRoot className="flex h-full flex-col overflow-hidden">
                {card.bannerMediaId === null ? null : (
                  <img
                    src={mediaFileUrl(card.bannerMediaId)}
                    alt=""
                    className="bg-muted h-40 w-full object-cover"
                    loading="lazy"
                  />
                )}
                <div className="flex flex-col gap-2 p-5">
                  <span className="flex flex-wrap items-center gap-2">
                    <Badge
                      variant="flat"
                      color={STATE_COLOURS[card.state] ?? 'gray'}
                      text={t(`events:events.options.state.${card.state}`)}
                    />
                    <Badge
                      variant="flat"
                      color={calendarKindColour(card.kind, kinds)}
                      text={read(kinds.find((word) => word.key === card.kind)?.label) || card.kind}
                    />
                  </span>
                  <H4>{read(card.title)}</H4>
                  {summary === '' ? null : <p className="text-muted-foreground">{summary}</p>}
                  <EventWhen
                    startsAtUtc={card.startsAtUtc}
                    endsAtUtc={card.endsAtUtc}
                    timezone={bootstrap?.division.timezone}
                  />
                  <Subtle className="text-sm">
                    {card.wholeDivision ? t('events:public.wholeDivision') : card.airports.join(' · ')}
                  </Subtle>
                </div>
              </CardRoot>
            </RouterAnchor>
          </article>
        );
      })}
    </div>
  );
}

/**
 * When an event runs: in UTC, and under it where the division lives (docs/UI-GUIDELINES.md, «Times») — the hub is read by people
 * flying in one and organising in the other. The zone comes from the bootstrap; until it has come, UTC alone.
 */
export function EventWhen({
  startsAtUtc,
  endsAtUtc,
  timezone,
}: {
  startsAtUtc: string;
  endsAtUtc: string;
  timezone: string | undefined;
}) {
  const { t } = useTranslation();
  const moment = useMoment();

  return (
    <span className="flex flex-col leading-tight">
      <span className="font-semibold tabular-nums">
        {t('calendar.utc', { at: spanText(startsAtUtc, endsAtUtc, moment) })}
      </span>
      {timezone === undefined ? null : (
        <Subtle className="tabular-nums">
          {t('calendar.local', { at: spanText(startsAtUtc, endsAtUtc, moment, timezone) })}
        </Subtle>
      )}
    </span>
  );
}
