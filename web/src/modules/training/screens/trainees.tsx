import {
  AccordionContent,
  AccordionItem,
  AccordionRoot,
  AccordionTrigger,
  Badge,
  Button,
  H2,
  H3,
  Subtle,
} from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams, useRouteContext } from '@tanstack/react-router';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { SchemaForm } from '../../../shared/forms';
import { useMoment } from '../../../shared/i18n/useMoment';
import { NotFound, Notice, PageShell, RatingBadge, personName } from '../../../shared/ui';
import {
  banStatus,
  shownState,
  traineePathQuery,
  type MyTrainingPathDto,
  type StaffTrainingDto,
  type TraineeBanDto,
  type TraineePathDto,
} from '../api';
import { traineeLookupSchema, type RatingKind, type TraineeLookupValues } from '../schemas';

import { LiftBan } from './bans';
import { ReportBoxes, ReportView, SessionList, StateBadge, WhenText } from './parts';
import {
  BANS,
  BAN_COLOURS,
  TRAINEES,
  banFormHref,
  ladderSays,
  traineeHref,
  trainingsByLadder,
  type LadderSays,
} from './path';
import { publishedBy } from './report';
import { formatHours, readyForExam } from './trainee';
import { staffTrainingHref } from './trainings';

/**
 * A trainee's path as the staff reads it (design M3 §4.2), over the server of A10a, like the pilot's page of the tours (design M2
 * §8.7): asked by VID; where they stand on each ladder — their rating and hours, what they may ask for next or why not, a mock exam
 * agreed, «ready for the exam», the waiting, a ban —; their bans, with «lift the ban»; and every training of theirs by ladder and
 * rating, each opening on its trainer, its session, its report and its sessions. «Ban» for whoever the server says may.
 *
 * Every training is the staff's page of it (`StaffTrainings.PageAsync`), so a trainer reading their own path reads nothing reserved
 * (note le-note-riservate-e-il-trainee), and the page says so as the page of a training does.
 *
 * What the reader reads of the path is the server's answer: a head of a FIR reads the trainings of their FIR, and neither the
 * ladders nor the bans, which the training department reads (A11b) — the server sends none, and the page says so.
 */

/** «Trainees»: a VID, and the trainee's path. */
export function TraineeLookupPage() {
  const { t } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const navigate = useNavigate();

  return (
    <PageShell
      title={t('training:trainees.title')}
      description={t('training:trainees.description')}
      breadcrumb={[{ label: t('training:nav.section') }, { label: t('training:trainees.title') }]}
    >
      <SchemaForm<TraineeLookupValues>
        schema={traineeLookupSchema}
        defaults={{}}
        locales={bootstrap.division.locales}
        labels="training:trainees"
        onSubmit={async (values) => {
          const href = values.vid === undefined ? null : traineeHref(values.vid);
          if (href !== null) {
            await navigate({ href });
          }
        }}
        submitLabel={t('training:trainees.open')}
      />
    </PageShell>
  );
}

export function TraineePathPage() {
  const { t } = useTranslation();
  // `$id`, like every other screen of the module: it is the trainee's VID. A pseudonym — a person whose data was erased, a negative
  // number — has no path (A12b), and is not asked for.
  const { id = '' } = useParams({ strict: false });
  const known = /^\d+$/.test(id);
  const path = useQuery({ ...traineePathQuery(Number(id)), enabled: known });

  if (known && path.isPending) {
    return <p className="text-muted-foreground text-sm">{t('common.loading')}</p>;
  }

  return path.data === undefined || path.data === null ? (
    <NotFound />
  ) : (
    <TraineePathScreen path={path.data} />
  );
}

function TraineePathScreen({ path }: { path: TraineePathDto }) {
  const { t } = useTranslation();
  const vid = path.trainee.vid;
  // A trainer reading their own path: what is reserved is on none of its trainings (note le-note-riservate-e-il-trainee).
  const reserved = path.trainings.some((training) => training.reservedLeftOut);
  // A head of a FIR (A11b): the trainings of their FIR, and not what the training department reads of the trainee.
  const firOnly = path.ladders === null || path.bans === null;

  return (
    <PageShell
      title={personName(path.trainee, t)}
      description={t('training:trainees.pathDescription')}
      breadcrumb={[
        { label: t('training:nav.section') },
        { label: t('training:trainees.title'), to: TRAINEES },
        { label: String(vid) },
      ]}
      actions={
        path.canBan ? (
          <Button asChild variant="outline">
            <RouterAnchor href={banFormHref(vid)}>{t('training:trainees.ban')}</RouterAnchor>
          </Button>
        ) : undefined
      }
    >
      <div className="flex flex-col gap-8">
        {reserved ? <Notice tone="info" title={t('training:staff.reserved')} /> : null}

        {firOnly ? <Notice tone="info" title={t('training:trainees.firOnly')} /> : null}

        {path.ladders === null ? null : (
          <Section title={t('training:trainees.sections.ladders')}>
            <div className="grid gap-4 sm:grid-cols-2">
              {path.ladders.map((ladder) => (
                <LadderCard key={ladder.kind} ladder={ladder} trainings={path.trainings} />
              ))}
            </div>
          </Section>
        )}

        {path.bans === null ? null : (
          <Section title={t('training:trainees.sections.bans')}>
            <BanLines bans={path.bans} canLift={path.canBan} />
            <p className="text-sm">
              <RouterAnchor href={`${BANS}?vid=${String(vid)}`} className="underline">
                {t('training:trainees.allBans')}
              </RouterAnchor>
            </p>
          </Section>
        )}

        <Section title={t('training:trainees.sections.trainings')}>
          <TrainingsByLadder
            trainings={path.trainings}
            empty={firOnly ? t('training:trainees.noTrainingsOnFir') : t('training:trainees.noTrainings')}
          />
        </Section>
      </div>
    </PageShell>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-3">
      <H2 className="text-lg">{title}</H2>
      {children}
    </section>
  );
}

/**
 * One ladder (§2.2, §2.8): the trainee's rating and hours on it, what they may ask for now or the first rule that refuses — the
 * answer their own page gives, said of them —, and what the last report marked them ready for.
 */
function LadderCard({
  ladder,
  trainings,
}: {
  ladder: MyTrainingPathDto;
  trainings: readonly StaffTrainingDto[];
}) {
  const { t, i18n } = useTranslation();

  return (
    <section className="bg-card text-card-foreground border-border flex flex-col gap-3 rounded-lg border p-4">
      <H3>{t(`training:kinds.${ladder.kind}`)}</H3>
      <dl className="grid grid-cols-[max-content_1fr] items-center gap-x-4 gap-y-1 text-sm">
        <dt className="text-muted-foreground">{t('training:trainees.rating')}</dt>
        <dd>
          {ladder.ratingShortName === null ? (
            t('training:unknown')
          ) : (
            <RatingBadge kind={ladder.kind} shortName={ladder.ratingShortName} />
          )}
        </dd>
        <dt className="text-muted-foreground">{t('training:trainees.hours')}</dt>
        <dd className="tabular-nums">{formatHours(ladder.hours, i18n.language) ?? t('training:unknown')}</dd>
      </dl>

      <div className="flex flex-col gap-1 text-sm">
        <LadderText says={ladderSays(ladder)} kind={ladder.kind} />
      </div>

      {ladder.next !== null && readyForExam(ladder, trainings) ? (
        <p className="text-sm font-semibold">
          {t('training:trainees.readyForExamOn', { rating: ladder.next.shortName })}
        </p>
      ) : null}
    </section>
  );
}

/** What a ladder says of the trainee, in the staff's words; the times are UTC, as the sentences say. */
function LadderText({ says, kind }: { says: LadderSays; kind: RatingKind }) {
  const { t, i18n } = useTranslation();
  const moment = useMoment();

  switch (says.kind) {
    case 'canAsk':
      return (
        <>
          <p className="flex flex-wrap items-center gap-2">
            <span>{t('training:trainees.standing.canAsk')}</span>
            <RatingBadge kind={kind} shortName={says.next.shortName} />
          </p>
          {says.isMockExam ? <p>{t('training:trainees.standing.mockExam')}</p> : null}
        </>
      );
    case 'banned':
      return (
        <p>
          {says.until === null
            ? t('training:trainees.standing.bannedForever')
            : t('training:trainees.standing.bannedUntil', { date: moment(says.until) })}
        </p>
      );
    case 'open':
      return (
        <p>
          {t('training:trainees.standing.open')}
          {says.trainingId === null ? null : (
            <>
              {' '}
              <RouterAnchor href={staffTrainingHref(says.trainingId)} className="underline">
                {t('training:trainees.standing.openLink')}
              </RouterAnchor>
            </>
          )}
        </p>
      );
    case 'waiting':
      return (
        <p>
          {says.until === null
            ? t('training:trainees.standing.waiting')
            : t('training:trainees.standing.waitingUntil', { date: moment(says.until) })}
        </p>
      );
    case 'hours': {
      const hours = formatHours(says.hours, i18n.language);
      const minimum = says.minimum === null ? '—' : String(says.minimum);
      return (
        <p>
          {hours === null
            ? t('training:trainees.standing.hoursUnknown', { minimum })
            : t('training:trainees.standing.hours', { minimum, hours })}
        </p>
      );
    }
    case 'noPosition':
      return <p>{t('training:trainees.standing.noPosition')}</p>;
    case 'nothingToAsk':
      return <p>{t('training:trainees.standing.nothingToAsk')}</p>;
    case 'other':
      return <p>{t(says.refusal)}</p>;
  }
}

/**
 * The trainee's bans (§2.9), newest first as the server orders them: how each stands, since when and until when, why, who gave it
 * and who lifted it — «lift the ban» on one that holds, for whoever may ban this member.
 */
function BanLines({ bans, canLift }: { bans: readonly TraineeBanDto[]; canLift: boolean }) {
  const { t } = useTranslation();
  const moment = useMoment();

  if (bans.length === 0) {
    return <p className="text-muted-foreground text-sm">{t('training:trainees.noBans')}</p>;
  }

  return (
    <ul className="flex flex-col gap-3">
      {bans.map((ban) => {
        const status = banStatus(ban);
        const who = [
          ban.givenBy === null
            ? null
            : t('training:trainees.banGivenBy', { name: personName(ban.givenBy, t) }),
          ban.liftedBy === null || ban.liftedAt === null
            ? null
            : t('training:trainees.banLiftedBy', {
                name: personName(ban.liftedBy, t),
                date: moment(ban.liftedAt),
              }),
        ].filter((part) => part !== null);

        return (
          <li
            key={ban.id}
            className="border-border flex flex-col gap-2 rounded-md border p-3 text-sm sm:flex-row sm:items-start sm:justify-between"
          >
            <div className="flex min-w-0 flex-col gap-1">
              <span className="flex flex-wrap items-center gap-2">
                <Badge
                  variant="flat"
                  color={BAN_COLOURS[status]}
                  text={t(`training:bans.options.status.${status}`)}
                />
                <span className="tabular-nums">
                  {ban.endsAt === null
                    ? t('training:trainees.banForever', { from: moment(ban.createdAt) })
                    : t('training:trainees.banUntil', {
                        from: moment(ban.createdAt),
                        until: moment(ban.endsAt),
                      })}
                </span>
              </span>
              <p className="break-words whitespace-pre-line">
                {t('training:trainees.banReason', { reason: ban.reason })}
              </p>
              {who.length === 0 ? null : <Subtle>{who.join(' · ')}</Subtle>}
            </div>
            {canLift && ban.holds ? (
              <div className="shrink-0">
                <LiftBan ban={ban} />
              </div>
            ) : null}
          </li>
        );
      })}
    </ul>
  );
}

/**
 * Every training of the trainee the reader reads, by ladder and rating (§4.2), each a line that opens on what the staff reads of it;
 * `empty` when there is none — none at all, or none on the FIR of a head of a FIR.
 */
function TrainingsByLadder({ trainings, empty }: { trainings: readonly StaffTrainingDto[]; empty: string }) {
  const { t } = useTranslation();
  const ladders = trainingsByLadder(trainings);

  if (ladders.length === 0) {
    return <p className="text-muted-foreground text-sm">{empty}</p>;
  }

  return (
    <div className="flex flex-col gap-6">
      {ladders.map((ladder) => (
        <div key={ladder.kind} className="flex flex-col gap-4">
          <H3>{t(`training:kinds.${ladder.kind}`)}</H3>
          {ladder.ratings.map((group) => (
            <div key={group.rating} className="flex flex-col gap-2">
              {group.ratingShortName === null ? null : (
                <div>
                  <RatingBadge kind={ladder.kind} shortName={group.ratingShortName} />
                </div>
              )}
              <AccordionRoot type="multiple" className="w-full">
                {group.trainings.map((training) => (
                  <TrainingItem key={training.id} training={training} />
                ))}
              </AccordionRoot>
            </div>
          ))}
        </div>
      ))}
    </div>
  );
}

/**
 * One training on the path: its state — held from the day after its session —, its position, a mock exam, when it was asked for;
 * opened, its trainer, its session, its report as published — or the trainer's boxes —, its sessions that are over, and its page.
 */
function TrainingItem({ training }: { training: StaffTrainingDto }) {
  const { t } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const moment = useMoment();
  const published = training.state === 'Completed' ? publishedBy(training.sessions) : null;
  const state = t(`training:states.${shownState(training)}`);
  const mockExam = training.isMockExam ? t('training:mine.mockExamBadge') : null;
  const asked = t('training:trainees.requestedAt', {
    date: moment(training.requestedAt, { time: false }),
  });

  return (
    <AccordionItem value={String(training.id)}>
      {/* Its name said in words with their separators: the pieces of the line are drawn apart, and read out glued without it. */}
      <AccordionTrigger
        aria-label={[state, training.position, mockExam, asked].filter((part) => part !== null).join(' · ')}
      >
        <span className="flex flex-wrap items-center gap-2 text-left">
          <StateBadge state={shownState(training)} />
          {training.position === null ? null : <span className="font-mono text-sm">{training.position}</span>}
          {mockExam === null ? null : <Badge variant="flat" color="purple" text={mockExam} />}
          <span className="text-muted-foreground text-sm font-normal">{asked}</span>
        </span>
      </AccordionTrigger>
      <AccordionContent>
        <div className="flex flex-col gap-4 pt-2">
          <p className="text-sm">
            {training.trainer === null
              ? t('training:staff.trainer.none')
              : t('training:trainees.trainer', { name: personName(training.trainer, t) })}
          </p>
          {training.scheduledStartUtc === null ? null : (
            <WhenText
              startsAtUtc={training.scheduledStartUtc}
              timezone={bootstrap.division.timezone}
              emphasis
            />
          )}
          {training.state === 'Completed' ? (
            <div className="flex flex-col gap-3">
              {published === null ? null : (
                <p className="text-sm">
                  {t('training:report.publishedBy', {
                    name: personName(published.by, t),
                    date: moment(published.at, { time: false }),
                  })}
                </p>
              )}
              <ReportView report={training} audience="staff" />
            </div>
          ) : (
            <ReportBoxes training={training} />
          )}
          {training.sessions.length === 0 ? null : (
            <SessionList sessions={training.sessions} timezone={bootstrap.division.timezone} />
          )}
          <div>
            <Button asChild size="sm" variant="secondary">
              <RouterAnchor href={staffTrainingHref(training.id)}>
                {t('training:trainees.openTraining')}
              </RouterAnchor>
            </Button>
          </div>
        </div>
      </AccordionContent>
    </AccordionItem>
  );
}
