import { H2, Subtle } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { bootstrapQuery } from '../../../features/me/queries';
import { loginHref } from '../../../shared/api/client';
import { SchemaForm, describeProblem } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { ConfirmDialog, Notice } from '../../../shared/ui';
import {
  myAvailabilityQuery,
  useGiveAvailability,
  useWithdrawAvailability,
  type MyAvailabilityDto,
  type PublicEventAtcDto,
  type PublicEventDto,
} from '../api';
import { availabilitySchema, emptyAvailability, type AvailabilityFormValues } from '../schemas';

/**
 * The ATC of an event with a roster on its page (design M4 §4.2, §7.1, E11a), under `#atc`: the positions it opens, to everybody, and a
 * member's availability. A visitor is asked to sign in while availabilities are open. A member without one gives their windows and a note
 * while they are open and their ATC rating is enough — below it, the page says which rating the event asks —; one who gave it reads it,
 * changes it until the close, and withdraws it until then. What the server would refuse it says field by field; whether the event still
 * takes availabilities is the server's answer in the page itself (`atc.open`).
 */
export function AvailabilitySection({ event, signedIn }: { event: PublicEventDto; signedIn: boolean }) {
  const { t } = useTranslation();
  const moment = useMoment();
  const atc = event.atc;
  // Read only for an event with a roster, by a member: no other page asks.
  const mine = useQuery({ ...myAvailabilityQuery(), enabled: signedIn && atc !== null });

  if (atc === null) {
    return null;
  }

  const availability = mine.data?.find((row) => row.eventId === event.id);

  return (
    <section id="atc" className="flex flex-col gap-4" aria-label={t('events:public.atc.title')}>
      <H2>{t('events:public.atc.title')}</H2>
      <Positions atc={atc} />
      {atc.open ? (
        <Subtle className="text-sm">
          {t('events:public.atc.closesAt', { date: moment(atc.closesAtUtc) })}
        </Subtle>
      ) : null}
      {!signedIn ? (
        atc.open ? (
          <p className="text-sm">
            <a href={loginHref(`${window.location.pathname}${window.location.search}`)} className="underline">
              {t('events:public.atc.signIn')}
            </a>
          </p>
        ) : (
          <Subtle className="text-sm">{t('events:public.atc.closed')}</Subtle>
        )
      ) : mine.data === undefined ? null : availability !== undefined ? (
        <Given event={event} availability={availability} />
      ) : !atc.open ? (
        <Subtle className="text-sm">{t('events:public.atc.closed')}</Subtle>
      ) : atc.ratedEnough === false ? (
        <Subtle className="text-sm">
          {t('events:public.atc.ratingTooLow', { rating: atc.minimumRating ?? '' })}
        </Subtle>
      ) : (
        <AvailabilityForm event={event} availability={undefined} />
      )}
    </section>
  );
}

/** The positions the event opens, by the hour they open: the callsign, the name the hub knows, and the hours in UTC. */
function Positions({ atc }: { atc: PublicEventAtcDto }) {
  const { t } = useTranslation();
  const moment = useMoment();

  if (atc.positions.length === 0) {
    return <Subtle className="text-sm">{t('events:public.atc.noPositions')}</Subtle>;
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm" aria-label={t('events:public.atc.positions')}>
        <thead>
          <tr className="text-muted-foreground border-b text-left">
            <th className="py-2 pr-3 font-medium">{t('events:public.atc.position')}</th>
            <th className="py-2 pr-3 font-medium">{t('events:public.atc.from')}</th>
            <th className="py-2 font-medium">{t('events:public.atc.to')}</th>
          </tr>
        </thead>
        <tbody>
          {atc.positions.map((position) => (
            <tr key={position.id} className="border-b align-top last:border-0">
              <td className="py-2 pr-3">
                <span className="font-mono">{position.callsign}</span>
                {position.name === null ? null : (
                  <span className="text-muted-foreground"> {position.name}</span>
                )}
              </td>
              <td className="py-2 pr-3">{moment(position.fromUtc)}</td>
              <td className="py-2">{moment(position.toUtc)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** A member who gave their availability: that they did, their windows — to change until the close —, and «withdraw» until then. */
function Given({ event, availability }: { event: PublicEventDto; availability: MyAvailabilityDto }) {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const withdraw = useWithdrawAvailability();
  const refusal = describeProblem(withdraw.error, t, i18n.language);

  return (
    <div className="flex flex-col gap-4">
      <Notice tone="success" title={t('events:public.atc.given')} />
      {availability.changeable ? (
        <AvailabilityForm event={event} availability={availability} />
      ) : (
        <div className="flex flex-col gap-1 text-sm">
          <span className="font-medium">{t('events:public.atc.yourWindows')}</span>
          <ul className="flex flex-col gap-1">
            {availability.windows.map((window) => (
              <li key={window.fromUtc}>
                {moment(window.fromUtc)} – {moment(window.toUtc)}
              </li>
            ))}
          </ul>
          {availability.note === null ? null : <p>{availability.note}</p>}
        </div>
      )}
      {refusal === null ? null : <Notice tone="error" title={refusal} />}
      {availability.withdrawable ? (
        <div>
          <ConfirmDialog
            triggerText={t('events:mine.withdraw')}
            triggerVariant="secondary"
            title={t('events:public.atc.withdrawTitle', { title: read(event.title) || event.slug })}
            description={t('events:public.atc.withdrawDescription')}
            confirmText={t('events:mine.withdraw')}
            confirmVariant="destructive"
            disabled={withdraw.isPending}
            onConfirm={() => withdraw.mutate(event.id)}
          />
        </div>
      ) : null}
    </div>
  );
}

/**
 * The windows and the note: «give your availability» the first time — one window, the whole event, to narrow —, «save your availability»
 * after. A refusal lands on the field of its window; one about the whole availability — closed meanwhile — above the form.
 */
function AvailabilityForm({
  event,
  availability,
}: {
  event: PublicEventDto;
  availability: MyAvailabilityDto | undefined;
}) {
  const { t } = useTranslation();
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const give = useGiveAvailability(event.id);
  const [saved, setSaved] = useState(false);

  // A time of a window is in UTC, and the form says what it is in the division's time next to it: drawn once the division is known.
  if (bootstrap === undefined) {
    return null;
  }

  const defaults: AvailabilityFormValues =
    availability === undefined
      ? emptyAvailability(event.startsAtUtc, event.endsAtUtc)
      : {
          windows: availability.windows.map((window) => ({ fromUtc: window.fromUtc, toUtc: window.toUtc })),
          note: availability.note ?? '',
        };

  return (
    <div className="flex flex-col gap-4">
      {saved ? <Notice tone="success" title={t('events:public.atc.saved')} /> : null}
      <SchemaForm<AvailabilityFormValues>
        schema={availabilitySchema}
        defaults={defaults}
        locales={[]}
        labels="events:public.atc"
        division={{ defaultLocale: bootstrap.division.defaultLocale, timezone: bootstrap.division.timezone }}
        onSubmit={async (values) => {
          setSaved(false);
          await give.mutateAsync(values);
          setSaved(availability !== undefined);
        }}
        submitLabel={t(availability === undefined ? 'events:public.atc.give' : 'events:public.atc.save')}
      />
    </div>
  );
}
