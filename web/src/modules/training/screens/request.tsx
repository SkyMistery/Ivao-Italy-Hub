import { Button, H1, H2, Label, Lead, RadioGroupItem, RadioGroupRoot, Subtle } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useSearch } from '@tanstack/react-router';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import type { ApiError } from '../../../shared/api/problem';
import { SchemaForm, describeProblem } from '../../../shared/forms';
import { ConfirmDialog, Notice, RatingBadge, useNotice } from '../../../shared/ui';
import {
  mineQuery,
  useRequestTraining,
  type MyTrainingDto,
  type MyTrainingPathDto,
  type TraineeTrainingDto,
  type TrainingRatingDto,
} from '../api';
import {
  EMPTY_REQUEST,
  requestFromFormValues,
  requestSchema,
  type RatingKind,
  type RequestFormValues,
  type RequestSearch,
} from '../schemas';

import { RefusalDetailText, TheoryExamLink } from './parts';
import { MINE, chosenPath, formatHours, isTheoryRefusal, refusalDetail, splitRefusal } from './trainee';

/**
 * The request of a training (design M3 §2.2, §4.1; note 2026-09-25-il-teorico-lo-dichiara-il-trainee), `/training/request`,
 * for a signed in member: who they are, read only and never their address; the ladder; the one training the hub proposes on
 * it; a position among the ones offered, when the rating is trained on one; and two texts in their own words.
 *
 * «Request training» asks the question on the theory first (R.2). A «no» is sent as well, and the hub records it as a request
 * it refused by itself: the screen says so, and no mail goes. Every rule is the server's: this page offers what
 * `GET /api/training/mine` says may be asked, and a refusal lands where it belongs — on its field, or above the form for the
 * request as a whole, whose details come from the same answer the page reads again.
 */
export function RequestPage() {
  const { t, i18n } = useTranslation();
  const search: RequestSearch = useSearch({ strict: false });
  const mine = useQuery(mineQuery());

  if (mine.isPending) {
    return (
      <p className="text-muted-foreground mx-auto w-full max-w-3xl px-4 py-10 text-sm">
        {t('common.loading')}
      </p>
    );
  }

  if (mine.data === undefined) {
    return (
      <div className="mx-auto w-full max-w-3xl px-4 py-10">
        <Notice tone="error" title={describeProblem(mine.error, t, i18n.language) ?? t('errors.unknown')} />
      </div>
    );
  }

  return <RequestScreen mine={mine.data} wanted={search.kind} />;
}

function RequestScreen({ mine, wanted }: { mine: MyTrainingDto; wanted: RatingKind | undefined }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const path = chosenPath(mine, wanted);

  // A request the hub refused because the theory is not passed: the screen says so in place of the form, until the trainee
  // goes back to the form or to the other ladder.
  const [declined, setDeclined] = useState<TraineeTrainingDto | null>(null);

  const choose = (kind: RatingKind) => {
    setDeclined(null);
    void navigate({ to: '.', search: { kind } as never, replace: true });
  };

  return (
    <article className="mx-auto flex w-full max-w-3xl flex-col gap-8 px-4 py-10">
      <header className="flex flex-col gap-2">
        <RouterAnchor href={MINE} className="text-sm underline">
          {t('training:mine.title')}
        </RouterAnchor>
        <H1>{t('training:request.title')}</H1>
        <Lead>{t('training:request.lead')}</Lead>
      </header>

      <Trainee mine={mine} path={path} />

      {path === null ? null : (
        <section className="flex flex-col gap-4">
          <H2>{t('training:request.what')}</H2>
          <PathChoice paths={mine.paths} value={path.kind} onChange={choose} />
          {declined !== null && declined.kind === path.kind ? (
            <Declined mine={mine} training={declined} onBack={() => setDeclined(null)} />
          ) : path.refusal !== null || path.next === null ? (
            <Refused path={path} />
          ) : (
            // Keyed on the ladder only: a rating the server moved on keeps what the trainee wrote, and the refusal that says so.
            <RequestForm key={path.kind} mine={mine} path={path} next={path.next} onDeclined={setDeclined} />
          )}
        </section>
      )}
    </article>
  );
}

/** Who asks, as the network gave them at their last sign in: read only, and never their address (§0.5). */
function Trainee({ mine, path }: { mine: MyTrainingDto; path: MyTrainingPathDto | null }) {
  const { t, i18n } = useTranslation();
  const kind = path === null ? '' : t(`training:kinds.${path.kind}`);

  return (
    <section className="flex flex-col gap-3">
      <H2>{t('training:request.you')}</H2>
      <dl className="grid grid-cols-[max-content_1fr] items-center gap-x-6 gap-y-2">
        <dt className="text-muted-foreground text-sm">{t('training:request.fields.vid')}</dt>
        <dd className="tabular-nums">{mine.vid}</dd>
        <dt className="text-muted-foreground text-sm">{t('training:request.fields.name')}</dt>
        <dd>{mine.name}</dd>
        {path === null ? null : (
          <>
            <dt className="text-muted-foreground text-sm">{t('training:request.fields.rating', { kind })}</dt>
            <dd>
              {path.ratingShortName === null ? (
                t('training:unknown')
              ) : (
                <RatingBadge kind={path.kind} shortName={path.ratingShortName} />
              )}
            </dd>
            <dt className="text-muted-foreground text-sm">{t('training:request.fields.hours', { kind })}</dt>
            <dd className="tabular-nums">
              {formatHours(path.hours, i18n.language) ?? t('training:unknown')}
            </dd>
          </>
        )}
      </dl>
      <Subtle>{t('training:request.youHint')}</Subtle>
    </section>
  );
}

/** The ladders, ATC and pilot, each with the training it would be: two paths of their own (§2.2, d4). */
function PathChoice({
  paths,
  value,
  onChange,
}: {
  paths: readonly MyTrainingPathDto[];
  value: RatingKind;
  onChange: (kind: RatingKind) => void;
}) {
  const { t } = useTranslation();

  return (
    <RadioGroupRoot
      aria-label={t('training:request.what')}
      value={value}
      onValueChange={(kind) => onChange(kind as RatingKind)}
      className="grid gap-3 sm:grid-cols-2"
    >
      {paths.map((path) => {
        const id = `training-path-${path.kind}`;

        return (
          <div key={path.kind} className="flex items-start gap-3 rounded-md border p-3">
            <RadioGroupItem id={id} value={path.kind} className="mt-1" />
            <Label htmlFor={id} className="flex flex-1 cursor-pointer flex-col gap-1 font-normal">
              <span className="font-semibold">{t(`training:kinds.${path.kind}`)}</span>
              <span className="text-muted-foreground text-sm">
                {path.next === null
                  ? t('training:request.nothingNext')
                  : t('training:request.next', { rating: path.next.shortName })}
              </span>
            </Label>
          </div>
        );
      })}
    </RadioGroupRoot>
  );
}

/** Why nothing can be asked for on this ladder now, and what the answer says beside it. */
function Refused({ path }: { path: MyTrainingPathDto }) {
  const { t } = useTranslation();
  const detail = refusalDetail(path);

  return (
    <Notice
      tone="warning"
      title={t(path.refusal ?? 'training:errors.requestNothingToAsk')}
      {...(detail === null ? {} : { description: <RefusalDetailText detail={detail} mineLink /> })}
    />
  );
}

/** The id of the form, so that the answer to the question on the theory sends it from the dialog. */
const REQUEST_FORM = 'training-request';

/** The fields the form draws: a refusal of any other is about the request as a whole, and the page says it above the form. */
const FORM_FIELDS = Object.keys(EMPTY_REQUEST);

function RequestForm({
  mine,
  path,
  next,
  onDeclined,
}: {
  mine: MyTrainingDto;
  path: MyTrainingPathDto;
  next: TrainingRatingDto;
  onDeclined: (training: TraineeTrainingDto) => void;
}) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const notice = useNotice();
  const send = useRequestTraining();
  const [answer, setAnswer] = useState<boolean | null>(null);
  const [problem, setProblem] = useState<ApiError | null>(null);

  // The positions offered follow the answer the page reads: a closed suggestion, as the server accepts no other.
  const schema = useMemo(
    () =>
      requestSchema(
        path.asksPosition
          ? path.positions.map((position) => ({
              value: position.callsign,
              label: `${position.callsign} — ${position.name}`,
            }))
          : null,
      ),
    [path],
  );

  const submit = async (values: RequestFormValues) => {
    setProblem(null);

    let training: TraineeTrainingDto;
    try {
      training = await send.mutateAsync(requestFromFormValues(values, next, mine.asksTheory ? answer : null));
    } catch (error) {
      const { form, page } = splitRefusal(error, FORM_FIELDS);
      setProblem(page);
      if (form !== null) {
        throw form;
      }
      return;
    }

    if (isTheoryRefusal(training)) {
      onDeclined(training);
      return;
    }

    notice({ tone: 'success', title: t('training:request.sent', { rating: next.shortName }) });
    await navigate({ href: MINE });
  };

  // The answer sends the form: it is the question «request training» asks (R.2), and the form is what is sent with it.
  const sendWithAnswer = () => {
    const form = document.getElementById(REQUEST_FORM);
    if (form instanceof HTMLFormElement) {
      form.requestSubmit();
    }
  };

  const cancel = (
    <Button asChild variant="ghost">
      <RouterAnchor href={MINE}>{t('common.cancel')}</RouterAnchor>
    </Button>
  );

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-3">
        <p className="flex flex-wrap items-center gap-2">
          <span>{t('training:request.proposed')}</span>
          <RatingBadge kind={path.kind} shortName={next.shortName} />
          <span className="font-semibold">{t(next.nameKey)}</span>
        </p>
        {path.isMockExam ? <Notice tone="info" title={t('training:mockExam')} /> : null}
      </div>

      {problem === null ? null : (
        <Notice tone="error" title={describeProblem(problem, t, i18n.language) ?? t('errors.unknown')} />
      )}

      <SchemaForm
        id={REQUEST_FORM}
        schema={schema}
        defaults={EMPTY_REQUEST}
        locales={[]}
        labels="training:request"
        onSubmit={submit}
        submitLabel={t('training:request.send')}
        secondaryAction={cancel}
        actionsElsewhere={mine.asksTheory}
      />

      {mine.asksTheory ? (
        <div className="flex flex-wrap items-center gap-3">
          <ConfirmDialog
            triggerText={t('training:request.send')}
            triggerVariant="secondary"
            title={t('training:request.theory.question', { rating: next.shortName })}
            description={t('training:request.theory.description')}
            confirmText={t('training:request.theory.confirm')}
            confirmVariant="primary"
            disabled={send.isPending}
            confirmDisabled={answer === null}
            // Asked afresh every time: the answer is the trainee's word for this request.
            onOpenChange={(open) => {
              if (open) {
                setAnswer(null);
              }
            }}
            onConfirm={sendWithAnswer}
          >
            <TheoryAnswer value={answer} onChange={setAnswer} />
            {mine.theoryExamUrl === null ? null : <TheoryExamLink url={mine.theoryExamUrl} />}
          </ConfirmDialog>
          {cancel}
        </div>
      ) : null}
    </div>
  );
}

/** The answer on the theory (R.2): yes or no, and neither until the trainee chooses. */
function TheoryAnswer({ value, onChange }: { value: boolean | null; onChange: (passed: boolean) => void }) {
  const { t } = useTranslation();

  return (
    <RadioGroupRoot
      aria-label={t('training:request.theory.answer')}
      value={value === null ? '' : value ? 'yes' : 'no'}
      onValueChange={(chosen) => onChange(chosen === 'yes')}
      className="flex flex-col gap-2"
    >
      {(['yes', 'no'] as const).map((option) => {
        const id = `training-theory-${option}`;

        return (
          <div key={option} className="flex items-center gap-2">
            <RadioGroupItem id={id} value={option} />
            <Label htmlFor={id} className="cursor-pointer font-normal">
              {t(`training:request.theory.${option}`)}
            </Label>
          </div>
        );
      })}
    </RadioGroupRoot>
  );
}

/** The hub's own refusal (R.2): the theory is not passed. Recorded and sent to nobody: this screen is where it is read. */
function Declined({
  mine,
  training,
  onBack,
}: {
  mine: MyTrainingDto;
  training: TraineeTrainingDto;
  onBack: () => void;
}) {
  const { t } = useTranslation();

  return (
    <div className="flex flex-col gap-4">
      <Notice
        tone="warning"
        title={t('training:request.declined.title', { rating: training.ratingShortName ?? '' })}
        description={
          <span className="flex flex-col gap-2">
            <span>{t('training:request.declined.description')}</span>
            {mine.theoryExamUrl === null ? null : <TheoryExamLink url={mine.theoryExamUrl} />}
          </span>
        }
      />
      <div className="flex flex-wrap items-center gap-3">
        <Button type="button" variant="secondary" onClick={onBack}>
          {t('training:request.declined.back')}
        </Button>
        <Button asChild variant="ghost">
          <RouterAnchor href={MINE}>{t('training:mine.title')}</RouterAnchor>
        </Button>
      </div>
    </div>
  );
}
