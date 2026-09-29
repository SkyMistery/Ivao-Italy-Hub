import { Button, H1, H2, Lead, Subtle } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useLocation, useParams } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { bootstrapQuery } from '../../../features/me/queries';
import { loginHref } from '../../../shared/api/client';
import { describeProblem } from '../../../shared/forms';
import { EmptyState, NotFound, Notice, RatingBadge } from '../../../shared/ui';
import { memberLabel, publicSessionQuery, upcomingSessionsQuery, type PublicSessionDto } from '../api';

import { StateBadge, WhenText } from './parts';
import { TRAINING, sessionHref, sessionTitle } from './site';
import { MINE, REQUEST } from './trainee';

/**
 * The public side of the training (design M3 §4.1; note il-training-in-pubblico): `/training`, with the sessions still to be held and
 * «Request training», and `/training/sessions/$id`, the page of one session, where every entry of the calendar points. What arrives is
 * what the reader may see, decided by the server: a visitor reads the position, the rating, the date and the time; a signed in reader
 * reads who too — the trainee and the trainer, by name and VID. The page decides nothing about it.
 */

export function TrainingPublicPage() {
  const { t, i18n } = useTranslation();
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const sessions = useQuery(upcomingSessionsQuery());
  const signedIn = bootstrap?.user !== null && bootstrap?.user !== undefined;

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-8 px-4 py-10">
      <header className="flex flex-col gap-3">
        <H1>{t('training:public.title')}</H1>
        <Lead>{t('training:public.lead')}</Lead>
        <div className="flex flex-wrap gap-2">
          {/* A visitor goes through the login and comes back to the request: it is a page of members. */}
          <Button asChild>
            <RouterAnchor href={REQUEST}>{t('training:request.send')}</RouterAnchor>
          </Button>
          {signedIn ? (
            <Button asChild variant="secondary">
              <RouterAnchor href={MINE}>{t('training:mine.title')}</RouterAnchor>
            </Button>
          ) : null}
        </div>
      </header>

      <section className="flex flex-col gap-4">
        <H2>{t('training:public.upcoming')}</H2>
        {sessions.isError ? (
          <Notice
            tone="error"
            title={describeProblem(sessions.error, t, i18n.language) ?? t('errors.unknown')}
          />
        ) : sessions.data === undefined || bootstrap === undefined ? (
          <p className="text-muted-foreground text-sm">{t('common.loading')}</p>
        ) : sessions.data.length === 0 ? (
          <EmptyState title={t('training:public.none')} />
        ) : (
          <UpcomingSessionList
            sessions={sessions.data}
            timezone={bootstrap.division.timezone}
            signedIn={signedIn}
          />
        )}
      </section>
    </div>
  );
}

/**
 * The sessions still to be held (§4.3), the soonest first, as `/training` and the block `training.upcomingSessions` draw them: each by
 * its rating and position — a link to its page —, when, in UTC and where the division lives, and who, when the server said it. A
 * visitor is told once, under the list, that signing in shows who.
 */
export function UpcomingSessionList({
  sessions,
  timezone,
  signedIn,
}: {
  sessions: readonly PublicSessionDto[];
  timezone: string;
  signedIn: boolean;
}) {
  const { t } = useTranslation();
  const location = useLocation();

  return (
    <div className="flex flex-col gap-3">
      {/* The lines between the sessions in the colour of the border: without one they take the colour of the text. */}
      <ul className="divide-border flex flex-col divide-y">
        {sessions.map((session) => (
          <li
            key={session.id}
            className="flex flex-col gap-2 py-3 sm:flex-row sm:items-start sm:justify-between"
          >
            <div className="flex min-w-0 flex-col gap-1">
              <div className="flex flex-wrap items-center gap-2">
                <RouterAnchor href={sessionHref(session.id)} className="font-semibold underline">
                  {sessionTitle(session)}
                </RouterAnchor>
                <span className="text-muted-foreground text-sm">{t(`training:kinds.${session.kind}`)}</span>
              </div>
              {session.trainee === null && session.trainer === null ? null : (
                <Subtle>
                  {t('training:public.people', {
                    trainee: session.trainee === null ? t('training:unknown') : memberLabel(session.trainee),
                    trainer: session.trainer === null ? t('training:unknown') : memberLabel(session.trainer),
                  })}
                </Subtle>
              )}
            </div>
            <WhenText startsAtUtc={session.startsAtUtc} timezone={timezone} emphasis />
          </li>
        ))}
      </ul>
      {signedIn ? null : (
        <p className="text-muted-foreground text-sm">
          <a href={loginHref(location.href)} className="underline">
            {t('training:public.signIn')}
          </a>
        </p>
      )}
    </div>
  );
}

export function PublicSessionPage() {
  const { t } = useTranslation();
  // A module route is not in the generated tree, so its parameters are not typed: the manifest declares the path and this reads
  // the one segment it has.
  const { id = '' } = useParams({ strict: false });
  const sessionId = Number(id);
  const valid = Number.isInteger(sessionId) && sessionId > 0;
  const session = useQuery({ ...publicSessionQuery(sessionId), enabled: valid });
  const { data: bootstrap } = useQuery(bootstrapQuery);

  if (valid && (session.isPending || bootstrap === undefined)) {
    return (
      <p className="text-muted-foreground mx-auto w-full max-w-3xl px-4 py-10 text-sm">
        {t('common.loading')}
      </p>
    );
  }

  if (session.data === undefined || session.data === null || bootstrap === undefined) {
    return <NotFound />;
  }

  return (
    <SessionScreen
      session={session.data}
      timezone={bootstrap.division.timezone}
      signedIn={bootstrap.user !== null}
    />
  );
}

/**
 * One session (§4.1): held or still to come, the ladder, then the position, the rating, the date and the time — and who, the trainee
 * and the trainer, when the server says it, which it does to a signed in reader only. A visitor is offered the login.
 */
function SessionScreen({
  session,
  timezone,
  signedIn,
}: {
  session: PublicSessionDto;
  timezone: string;
  signedIn: boolean;
}) {
  const { t } = useTranslation();
  const location = useLocation();

  return (
    <article className="mx-auto flex w-full max-w-3xl flex-col gap-6 px-4 py-10">
      <RouterAnchor href={TRAINING} className="text-sm underline">
        {t('training:public.back')}
      </RouterAnchor>

      <header className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <StateBadge state={session.held ? 'Held' : 'Scheduled'} />
          <span className="text-muted-foreground text-sm">{t(`training:kinds.${session.kind}`)}</span>
        </div>
        <H1>{t('training:public.session.title', { title: sessionTitle(session) })}</H1>
      </header>

      <dl className="grid grid-cols-[max-content_1fr] items-start gap-x-6 gap-y-3 text-sm">
        {session.position === null ? null : (
          <>
            <dt className="text-muted-foreground">{t('training:public.session.position')}</dt>
            <dd className="font-mono">{session.position}</dd>
          </>
        )}
        {session.ratingShortName === null ? null : (
          <>
            <dt className="text-muted-foreground">{t('training:public.session.rating')}</dt>
            <dd>
              <RatingBadge kind={session.kind} shortName={session.ratingShortName} />
            </dd>
          </>
        )}
        <dt className="text-muted-foreground">{t('training:public.session.when')}</dt>
        <dd>
          <WhenText startsAtUtc={session.startsAtUtc} timezone={timezone} emphasis />
        </dd>
        {session.trainee === null ? null : (
          <>
            <dt className="text-muted-foreground">{t('training:public.session.trainee')}</dt>
            <dd>{memberLabel(session.trainee)}</dd>
          </>
        )}
        {session.trainer === null ? null : (
          <>
            <dt className="text-muted-foreground">{t('training:public.session.trainer')}</dt>
            <dd>{memberLabel(session.trainer)}</dd>
          </>
        )}
      </dl>

      {signedIn ? null : (
        <p className="text-muted-foreground text-sm">
          <a href={loginHref(location.href)} className="underline">
            {t('training:public.session.signIn')}
          </a>
        </p>
      )}
    </article>
  );
}
