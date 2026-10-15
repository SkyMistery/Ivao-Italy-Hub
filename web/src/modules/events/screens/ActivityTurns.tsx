import { Button, H3, Subtle } from '@ivao/atmosphere-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { describeProblem } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { Notice } from '../../../shared/ui';
import {
  useGiveSeatBack,
  useTakeSeat,
  type MyRegistrationDto,
  type MySeatDto,
  type PublicActivityDto,
} from '../api';

/**
 * The parallel activities of an event in person on its page (design M4 §4-bis.2, §7.1, E17), inside the section of the registration: each
 * activity with its turns, in UTC as every hour of an event, and the seats still free in each — to everybody, never who took them. A member
 * registered takes a seat in a turn while the event takes registrations, and gives it back until it starts; whether a turn is still free,
 * one of the activity's, and not over another of theirs is the server's to say, above the list.
 */
export function ActivitiesList({
  activities,
  registration,
}: {
  activities: readonly PublicActivityDto[];
  /** The reader's registration to the event, when they have one: what lets them take a seat. */
  registration: MyRegistrationDto | undefined;
}) {
  const { t, i18n } = useTranslation();
  const take = useTakeSeat();
  const giveBack = useGiveSeatBack();
  const refusal = describeProblem(take.error ?? giveBack.error, t, i18n.language);

  if (activities.length === 0) {
    return null;
  }

  return (
    <div className="flex flex-col gap-4" aria-label={t('events:public.activities.title')} role="group">
      <H3>{t('events:public.activities.title')}</H3>
      {registration === undefined ? (
        <Subtle className="text-sm">{t('events:public.activities.registerFirst')}</Subtle>
      ) : null}
      {refusal === null ? null : <Notice tone="error" title={refusal} />}
      {activities.map((activity) => (
        <Activity
          key={activity.id}
          activity={activity}
          registration={registration}
          pending={take.isPending || giveBack.isPending}
          onTake={(startsAtUtc) => {
            giveBack.reset();
            take.mutate({ activityId: activity.id, startsAtUtc });
          }}
          onGiveBack={(seat) => {
            take.reset();
            giveBack.mutate(seat.id);
          }}
        />
      ))}
    </div>
  );
}

function Activity({
  activity,
  registration,
  pending,
  onTake,
  onGiveBack,
}: {
  activity: PublicActivityDto;
  registration: MyRegistrationDto | undefined;
  pending: boolean;
  onTake: (startsAtUtc: string) => void;
  onGiveBack: (seat: MySeatDto) => void;
}) {
  const { t } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const name = read(activity.name);
  const place = activity.place === null ? '' : read(activity.place);
  // A seat names its turn by the instant it starts: the same instant whichever way the server wrote it.
  const mine = (start: string) =>
    registration?.seats.find(
      (seat) => seat.activityId === activity.id && Date.parse(seat.startsAtUtc) === Date.parse(start),
    );

  return (
    <div className="flex flex-col gap-2">
      <p className="font-medium">
        {name}
        {place === '' ? null : <span className="text-muted-foreground font-normal"> · {place}</span>}
      </p>
      <Subtle className="text-sm">
        {t('events:public.activities.seatsEvery', {
          seats: activity.seatsPerSlot,
          minutes: activity.slotMinutes,
        })}
      </Subtle>
      <div className="overflow-x-auto">
        <table className="w-full text-sm" aria-label={name}>
          <thead>
            <tr className="text-muted-foreground border-b text-left">
              <th className="py-2 pr-3 font-medium">{t('events:public.activities.turn')}</th>
              <th className="py-2 pr-3 font-medium">{t('events:public.activities.free')}</th>
              {registration === undefined ? null : <th className="py-2 font-medium" />}
            </tr>
          </thead>
          <tbody>
            {activity.turns.map((turn) => {
              const seat = mine(turn.startsAtUtc);
              return (
                <tr key={turn.startsAtUtc} className="border-b align-middle last:border-0">
                  <td className="py-2 pr-3">
                    {t('events:public.activities.hours', {
                      from: moment(turn.startsAtUtc),
                      to: moment(turn.endsAtUtc, { date: false }),
                    })}
                  </td>
                  <td className="py-2 pr-3">
                    {seat !== undefined
                      ? t('events:public.activities.yours')
                      : turn.free === 0
                        ? t('events:public.activities.full')
                        : t('events:public.activities.seatsFree', { seats: turn.free })}
                  </td>
                  {registration === undefined ? null : (
                    <td className="py-2 text-right">
                      {seat !== undefined ? (
                        registration.withdrawable ? (
                          <Button
                            size="sm"
                            variant="secondary"
                            disabled={pending}
                            onClick={() => onGiveBack(seat)}
                          >
                            {t('events:public.activities.giveBack')}
                          </Button>
                        ) : null
                      ) : registration.changeable && turn.free > 0 ? (
                        <Button size="sm" disabled={pending} onClick={() => onTake(turn.startsAtUtc)}>
                          {t('events:public.activities.take')}
                        </Button>
                      ) : null}
                    </td>
                  )}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

/**
 * A member's seats under their registration (§4-bis.2), one line each — the activity, its place, the turn in UTC —, on `/events/mine`;
 * with «give back» while the event takes withdrawals.
 */
export function RegistrationSeats({ registration }: { registration: MyRegistrationDto }) {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const giveBack = useGiveSeatBack();
  const [asked, setAsked] = useState<number | null>(null);
  const refusal = describeProblem(giveBack.error, t, i18n.language);

  if (registration.seats.length === 0) {
    return null;
  }

  return (
    <div className="flex flex-col gap-1">
      <p className="text-sm font-medium">{t('events:mine.seats')}</p>
      {refusal === null ? null : <Notice tone="error" title={refusal} />}
      <ul className="flex flex-col gap-1 text-sm">
        {registration.seats.map((seat) => (
          <li key={seat.id} className="flex flex-wrap items-center gap-2">
            <span>
              {read(seat.activity)}
              {seat.place === null ? null : ` · ${read(seat.place)}`}
              {' — '}
              {t('events:public.activities.hours', {
                from: moment(seat.startsAtUtc),
                to: moment(seat.endsAtUtc, { date: false }),
              })}
            </span>
            {registration.withdrawable ? (
              <Button
                size="sm"
                variant="ghost"
                disabled={giveBack.isPending && asked === seat.id}
                onClick={() => {
                  setAsked(seat.id);
                  giveBack.mutate(seat.id);
                }}
              >
                {t('events:public.activities.giveBack')}
              </Button>
            ) : null}
          </li>
        ))}
      </ul>
    </div>
  );
}
