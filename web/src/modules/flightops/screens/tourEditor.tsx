import { Button, Tabs } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useBlocker, useNavigate, useParams, useSearch } from '@tanstack/react-router';
import { CircleAlert } from 'lucide-react';
import { useEffect, useRef, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { readBody, type Body } from '../../../blocks';
import { activeAwardsQuery } from '../../../features/awards/queries';
import { BodyEditor } from '../../../features/content/BodyEditor';
import { useBodyPathDescription } from '../../../features/content/bodyPath';
import { mediaPickerQuery } from '../../../features/media/queries';
import { holdsPermissionAnywhere, writableDepartments } from '../../../shared/api/bootstrap';
import { SchemaForm, describeProblem, languageNames, type ChoiceOption } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { listSearchSchema } from '../../../shared/list';
import { ConfirmDialog, Notice, PageShell } from '../../../shared/ui';
import {
  emptyTour,
  groupsListQuery,
  tourQuery,
  tourRatingsQuery,
  tourReadyProblemsQuery,
  tourToFormValues,
  useDeleteTour,
  useSaveTour,
  useTourStatus,
  type TourDetailDto,
  type TourReadyProblemsDto,
} from '../api';
import { TOURS_DELETE, TOURS_EDIT, TOURS_MANAGE_RULES, TOURS_MANAGE_TEMPLATES } from '../permissions';
import {
  SUBTOUR_KINDS,
  TOUR_KINDS,
  tourEditorSearchSchema,
  tourKindSchema,
  tourSchema,
  type TourFormPart,
  type TourStep,
} from '../schemas';

import { LegGrid } from './LegGrid';
import { keepingCurrent, useStaff, useTypeSuggestions } from './hooks';
import { TourRulesTab } from './rules';
import { TourCallsignsTab, TourConstraints, TourHubsTab, TourOpenGoal, TourSubtoursTab } from './shape';
import { neighbours, openStep, routeParts, stepOfProblem, stepsToFix, tourSteps } from './tourSteps';
import { TEMPLATES, TOURS } from './tours';

/**
 * The editor of a tour (design 11 §5, which replaces design M2 §8.3): the steps a tour is written in, in their order —
 * its kind; its name, dates and pictures; its route; its rules; its briefing; the check of what stands between it and
 * "ready". A new tour walks them: the kind is chosen first and travels in the address (`?kind=`), and the tour exists as a
 * draft from the second step, which saves it and goes on to the next. One that exists has the same steps as tabs, each
 * marked when the server says something in it stands between the tour and "ready" (`ready-problems`). The generated form
 * (`SchemaForm`) and the grid of the legs (`LegGrid`) are where they were: only where they stand has changed.
 */

type TourKind = (typeof TOUR_KINDS)[number];

/** Who may write what on this tour: its own permission, the rules', and a template's on top of both (design M2 §1.10). */
function useWrites(tour: TourDetailDto | null) {
  const { bootstrap } = useStaff();
  const holds = (permission: string) =>
    tour !== null && writableDepartments(bootstrap, permission).includes(tour.ownerDepartment);
  const template = tour === null || !tour.isTemplate || holds(TOURS_MANAGE_TEMPLATES);

  return {
    tour: holds(TOURS_EDIT),
    // A template's goal, constraints and callsigns are changed with the template's own permission too.
    shape: holds(TOURS_EDIT) && template,
    rules: holds(TOURS_EDIT) && holds(TOURS_MANAGE_RULES) && template,
  };
}

/** A step's name in its tab: its number in front (design 11 §6), and a mark when it has something to fix. */
function StepTrigger({ number, step, toFix }: { number: number; step: TourStep; toFix: boolean }) {
  const { t } = useTranslation();

  return (
    <span className="inline-flex items-center gap-1.5">
      <span className="tabular-nums">{number}.</span>
      {t(`flightops:tours.steps.${step}`)}
      {toFix ? (
        <>
          <CircleAlert aria-hidden className="size-4 text-orange-500" />
          <span className="sr-only">{t('flightops:tours.steps.toFix')}</span>
        </>
      ) : null}
    </span>
  );
}

/** The problems of "ready" in one list: the server's answer, as `[field, keys]`, with the missing languages. */
type Problems = readonly (readonly [string, readonly string[]])[];

/**
 * What stands between a draft and "ready", before anybody presses it: the server's answer to the very checks the action
 * runs, one line per field, the missing languages named — and, at the check, the step each one is fixed in.
 */
function ReadyProblems({
  problems,
  localized,
  briefing,
  linkTo,
}: {
  problems: Problems;
  localized: TourReadyProblemsDto['localized'];
  briefing: Body;
  linkTo?: (field: string) => { step: TourStep; href: string };
}) {
  const { t, i18n } = useTranslation();
  // A path inside the briefing — `briefing.sections[0].blocks[1].props.text` — read against the briefing as saved, the
  // way the editor of the content reads its own (`publishProblems.tsx`): the checks ran on the saved row too.
  const describe = useBodyPathDescription(briefing);

  if (problems.length === 0) {
    return null;
  }

  return (
    <Notice
      tone="warning"
      title={t('flightops:tours.readyProblems')}
      description={
        <ul className="list-disc pl-5">
          {problems.map(([field, keys]) => {
            const missing = localized[field] ?? [];
            // `legs.3` is the leg numbered 3, `legs` the legs as a whole (T7a); `hubs.LIRF` a hub, `rotations.LIRF.2` the
            // second rotation of that hub (T7b).
            const leg = /^legs\.(\d+)$/.exec(field);
            const hub = /^hubs\.([A-Z0-9]+)$/.exec(field);
            const rotation = /^rotations\.([A-Z0-9]+)\.(\d+)$/.exec(field);
            const label = field.startsWith('briefing.')
              ? `${t('flightops:tours.fields.briefing')} › ${describe(field)}`
              : leg !== null
                ? t('flightops:legs.row', { number: Number(leg[1]) })
                : hub !== null
                  ? t('flightops:hubs.row', { icao: hub[1] })
                  : rotation !== null
                    ? t('flightops:rotations.row', { hub: rotation[1], number: Number(rotation[2]) })
                    : t(`flightops:tours.fields.${field.split(/[.[]/)[0] ?? field}`);
            const sentence =
              missing.length > 0
                ? t('errors.localized.missingIn', { locales: languageNames([...missing], i18n.language) })
                : keys.map((key) => t(key)).join(' ');
            const link = linkTo?.(field);

            return (
              <li key={field}>
                {label}: {sentence}
                {link === undefined ? null : (
                  <>
                    {' '}
                    <RouterAnchor href={link.href} className="underline">
                      {t('flightops:tours.steps.goTo', { step: t(`flightops:tours.steps.${link.step}`) })}
                    </RouterAnchor>
                  </>
                )}
              </li>
            );
          })}
        </ul>
      }
    />
  );
}

/** The bar of a tour that exists: what happens to it without its form. */
function TourActions({ tour }: { tour: TourDetailDto }) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useStaff();
  const navigate = useNavigate();
  const status = useTourStatus(tour.id);
  const remove = useDeleteTour();

  const released = tour.releaseAt !== null && new Date(tour.releaseAt) <= new Date();
  // Deleting asks for Tours.Delete on every row, and a template for its own permission too: the server asks both.
  const mayDelete =
    holdsPermissionAnywhere(bootstrap, TOURS_DELETE) &&
    (!tour.isTemplate || holdsPermissionAnywhere(bootstrap, TOURS_MANAGE_TEMPLATES));
  const moment = useMoment();
  const refusal = describeProblem(status.error ?? remove.error, t, i18n.language);

  // An archived tour is read, never changed (design M2 §10): the server refuses every action, so none is offered.
  if (tour.purgedAt !== null) {
    return (
      <Notice
        tone="info"
        title={t('flightops:tours.purged', { date: moment(tour.purgedAt, { time: false }) })}
      />
    );
  }

  return (
    <div className="flex flex-col items-end gap-2">
      <div className="flex flex-wrap justify-end gap-2">
        {/* "Mark ready" is the last step's, after the check of what it needs (design 11 §5): not here too. */}
        {tour.isTemplate || tour.status === 'Draft' || released ? null : (
          <Button variant="outline" disabled={status.isPending} onClick={() => status.mutate('Draft')}>
            {t('flightops:tours.actions.draft')}
          </Button>
        )}
        {tour.isTemplate ? null : (
          <Button
            variant="outline"
            disabled={status.isPending}
            onClick={() => status.mutate(tour.isHidden ? 'Show' : 'Hide')}
          >
            {t(tour.isHidden ? 'flightops:tours.actions.show' : 'flightops:tours.actions.hide')}
          </Button>
        )}
        {tour.isTemplate || !holdsPermissionAnywhere(bootstrap, TOURS_MANAGE_TEMPLATES) ? null : (
          <Button asChild variant="outline">
            <RouterAnchor href={`${TOURS}/${tour.id}/save-as-template`}>
              {t('flightops:tours.actions.saveAsTemplate')}
            </RouterAnchor>
          </Button>
        )}
        {mayDelete ? (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('flightops:tours.delete.title')}
            description={t('flightops:tours.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() =>
              remove.mutate(tour.id, {
                onSuccess: () => void navigate({ href: tour.isTemplate ? TEMPLATES : TOURS }),
              })
            }
          />
        ) : null}
      </div>
      {refusal === null ? null : <Notice tone="error" title={refusal} />}
    </div>
  );
}

/**
 * The briefing of a tour (design M2 §1.2, §8.3): the editor of the content's body, saved with the tour's own `PUT` —
 * which validates the envelope, puts the text in search and declares the pictures as uses of files that expire with
 * the tour. Saved by a press, not by itself: a tour's other fields travel in the same request, and a save made under
 * somebody's fingers would be one of the tour as well.
 */
function TourBriefing({ tour }: { tour: TourDetailDto }) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useStaff();
  const locales = bootstrap.division.locales;
  const save = useSaveTour(tour.id);

  const [initial] = useState(() => readBody(tour.briefing));
  const [body, setBody] = useState(initial);
  // What was last stored, as one string: the briefing is dirty while the one on screen says something else.
  const [stored, setStored] = useState(() => JSON.stringify(initial));
  const [saved, setSaved] = useState(false);
  const dirty = JSON.stringify(body) !== stored;

  // On the way out — another step, another screen, the browser's tab closed — a briefing not saved is asked about, in
  // the words the editor of the content uses.
  const leaving = useRef(dirty);
  useEffect(() => {
    leaving.current = dirty;
  });
  useBlocker({
    shouldBlockFn: () => leaving.current && !window.confirm(t('content.editor.autosave.leave')),
    enableBeforeUnload: () => leaving.current,
  });

  const refusal = describeProblem(save.error, t, i18n.language);

  return (
    <BodyEditor
      initial={initial}
      onChange={(next) => {
        setBody(next);
        setSaved(false);
      }}
      locales={locales}
      division={{ defaultLocale: bootstrap.division.defaultLocale, timezone: bootstrap.division.timezone }}
      // The pictures come from the library, like the banner and the photo: the department that prepares them uploads
      // them there (design §1.14).
      mediaLibrary={mediaPickerQuery(tour.ownerDepartment)}
      // ⚠️ No block that asks for a permission belongs in a briefing: the only one that does, the interactive block,
      // runs in a frame the server builds for a row of the content alone (`/embed/{content}/…`).
      holds={() => false}
      // A tour is not published through the content's publication, the only thing that captures a data block: here
      // every one stays live, and the editor does not offer "frozen" (Carmine, 18 September 2026).
      captures={false}
      toolbar={(tools) => (
        <div className="flex flex-wrap items-center gap-2">
          <Button
            type="button"
            disabled={save.isPending || !dirty}
            onClick={() => {
              const sent = body;
              save.mutate(
                // The tour as it stands, with this briefing: the row's version is the latest the cache has.
                { values: tourToFormValues(tour, locales), briefing: sent },
                {
                  onSuccess: () => {
                    setStored(JSON.stringify(sent));
                    setSaved(true);
                  },
                },
              );
            }}
          >
            {t('flightops:tours.briefing.save')}
          </Button>
          {tools}
        </div>
      )}
      header={
        refusal !== null ? (
          <Notice tone="error" title={refusal} />
        ) : saved && !dirty ? (
          <Notice tone="success" title={t('flightops:tours.briefing.saved')} />
        ) : dirty ? (
          <p role="status" className="text-muted-foreground text-sm">
            {t('flightops:tours.briefing.unsaved')}
          </p>
        ) : null
      }
    />
  );
}

/** Every kind a tour of this place may be, each with the sentence that says how it is flown; the chosen one stands out. */
function KindSentences({ kinds, chosen }: { kinds: readonly TourKind[]; chosen: TourKind }) {
  const { t } = useTranslation();

  return (
    <dl className="grid gap-x-4 gap-y-2 text-sm sm:grid-cols-[minmax(10rem,auto)_1fr]">
      {kinds.map((kind) => (
        <div key={kind} className="contents">
          <dt className={kind === chosen ? 'font-semibold' : 'text-muted-foreground'}>
            {t(`flightops:tours.options.kind.${kind}`)}
          </dt>
          <dd className={kind === chosen ? '' : 'text-muted-foreground'}>
            {t(`flightops:tours.kindHelp.${kind}`)}
          </dd>
        </div>
      ))}
    </dl>
  );
}

/**
 * The first step: the kind. On a new tour it is chosen and carried to the next step in the address — nothing is saved
 * yet; on one that exists it is saved with the tour, until the public sees it and it no longer changes (§1.2.1).
 */
function KindStep({
  tour,
  initial,
  isSubtour,
  editable,
  onChosen,
  onSaved,
}: {
  tour: TourDetailDto | null;
  initial: TourKind;
  isSubtour: boolean;
  editable: boolean;
  onChosen: (kind: TourKind) => void;
  /** Said by the screen, above the step: the step is drawn again with the row the save answers. */
  onSaved: () => void;
}) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useStaff();
  const locales = bootstrap.division.locales;
  const save = useSaveTour(tour?.id ?? null);
  const [chosen, setChosen] = useState<TourKind>(initial);
  const kinds: readonly TourKind[] = isSubtour ? SUBTOUR_KINDS : TOUR_KINDS;
  const locked = tour?.isPublic ?? false;
  const refusal = describeProblem(save.error, t, i18n.language);

  return (
    <div className="flex flex-col gap-4">
      <p className="text-sm">{t('flightops:tours.steps.kindLead')}</p>
      {locked || !editable ? (
        <p className="font-semibold">{t(`flightops:tours.options.kind.${initial}`)}</p>
      ) : (
        <SchemaForm
          schema={tourKindSchema(isSubtour)}
          defaults={{ kind: initial }}
          locales={locales}
          labels="flightops:tours"
          onChange={(values) => setChosen(values.kind)}
        />
      )}
      {locked ? <Notice tone="info" title={t('flightops:tours.steps.kindLocked')} /> : null}
      <KindSentences kinds={kinds} chosen={locked ? initial : chosen} />
      {refusal === null ? null : <Notice tone="error" title={refusal} />}
      {locked || !editable ? null : tour === null ? (
        <div>
          <Button type="button" onClick={() => onChosen(chosen)}>
            {t('flightops:tours.steps.next')}
          </Button>
        </div>
      ) : (
        <div>
          <Button
            type="button"
            disabled={save.isPending || chosen === tour.kind}
            onClick={() =>
              save.mutate(
                { values: { ...tourToFormValues(tour, locales), kind: chosen } },
                { onSuccess: onSaved },
              )
            }
          >
            {t('common.save')}
          </Button>
        </div>
      )}
    </div>
  );
}

/** A section of a step, with its heading. */
function StepSection({ title, children }: { title?: string; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-2">
      {title === undefined ? null : <h3 className="text-lg font-semibold">{title}</h3>}
      {children}
    </section>
  );
}

/** The route of a tour, by its kind: the hubs and their rotations, the legs, the subtours, or the goal of an Open tour. */
function RouteStep({ tour }: { tour: TourDetailDto }) {
  const { t } = useTranslation();
  const writes = useWrites(tour);
  const parts = routeParts(tour);

  if (parts.length === 0) {
    return <Notice tone="info" title={t('flightops:tours.steps.noRoute')} />;
  }

  return (
    <div className="flex flex-col gap-8">
      {parts.map((part) =>
        part === 'hubs' ? (
          <TourHubsTab key={part} tour={tour} editable={writes.tour} />
        ) : part === 'legs' ? (
          <StepSection key={part} title={t('flightops:tours.steps.legs')}>
            <LegGrid tour={tour} editable={writes.tour} />
          </StepSection>
        ) : part === 'subtours' ? (
          <StepSection key={part} title={t('flightops:tours.steps.subtours')}>
            <TourSubtoursTab tour={tour} editable={writes.tour} />
          </StepSection>
        ) : (
          <TourOpenGoal key={part} tour={tour} editable={writes.shape} />
        ),
      )}
    </div>
  );
}

/** The rules of a tour: how it is flown and the rules it is validated with, its callsign constraints, an Open one's filters. */
function RulesStep({ tour, conditions }: { tour: TourDetailDto; conditions: ReactNode }) {
  const { t } = useTranslation();
  const writes = useWrites(tour);

  return (
    <div className="flex flex-col gap-8">
      <TourRulesTab tour={tour} conditions={conditions} editable={writes.rules} />
      <StepSection title={t('flightops:tours.steps.callsigns')}>
        <TourCallsignsTab tour={tour} editable={writes.shape} />
      </StepSection>
      {tour.kind === 'Open' ? <TourConstraints tour={tour} editable={writes.shape} /> : null}
    </div>
  );
}

/**
 * The last step: every problem of "ready", each with the step it is fixed in, and "Mark ready". A tour already ready says
 * so, with the day its page goes public.
 */
function CheckStep({
  tour,
  problems,
  localized,
  linkTo,
}: {
  tour: TourDetailDto;
  problems: Problems | undefined;
  localized: TourReadyProblemsDto['localized'];
  linkTo: (field: string) => { step: TourStep; href: string };
}) {
  const { t, i18n } = useTranslation();
  const moment = useMoment();
  const status = useTourStatus(tour.id);
  const writes = useWrites(tour);
  const refusal = describeProblem(status.error, t, i18n.language);

  if (tour.status !== 'Draft') {
    return (
      <Notice
        tone="success"
        title={
          tour.releaseAt === null
            ? t('flightops:tours.steps.isReadyNoDate')
            : t('flightops:tours.steps.isReady', { date: moment(tour.releaseAt, { time: false }) })
        }
      />
    );
  }

  if (problems === undefined) {
    return null;
  }

  return (
    <div className="flex flex-col gap-4">
      {problems.length === 0 ? (
        <Notice tone="success" title={t('flightops:tours.steps.nothingMissing')} />
      ) : (
        <ReadyProblems
          problems={problems}
          localized={localized}
          briefing={readBody(tour.briefing)}
          linkTo={linkTo}
        />
      )}
      {refusal === null ? null : <Notice tone="error" title={refusal} />}
      {writes.tour && tour.purgedAt === null ? (
        <div>
          <Button disabled={status.isPending} onClick={() => status.mutate('Ready')}>
            {t('flightops:tours.actions.ready')}
          </Button>
        </div>
      ) : null}
    </div>
  );
}

/** "Previous step" and "Next step", under every step of a tour that exists. */
function StepNavigation({
  previous,
  next,
  hrefOf,
}: {
  previous: TourStep | null;
  next: TourStep | null;
  hrefOf: (step: TourStep) => string;
}) {
  const { t } = useTranslation();

  if (previous === null && next === null) {
    return null;
  }

  return (
    <nav className="flex flex-wrap justify-between gap-2 border-t pt-4">
      {previous === null ? (
        <span />
      ) : (
        <Button asChild variant="outline">
          <RouterAnchor href={hrefOf(previous)}>
            ← {t('flightops:tours.steps.previous')}: {t(`flightops:tours.steps.${previous}`)}
          </RouterAnchor>
        </Button>
      )}
      {next === null ? null : (
        <Button asChild variant="outline">
          <RouterAnchor href={hrefOf(next)}>
            {t('flightops:tours.steps.next')}: {t(`flightops:tours.steps.${next}`)} →
          </RouterAnchor>
        </Button>
      )}
    </nav>
  );
}

export function TourEditor() {
  const { t } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useStaff();
  const navigate = useNavigate();
  const id = String(useParams({ strict: false }).id ?? 'new');
  const search = tourEditorSearchSchema.parse(useSearch({ strict: false }));
  const isNew = id === 'new';
  const locales = bootstrap.division.locales;
  const [savedAt, setSavedAt] = useState<TourStep | null>(null);

  const tour = useQuery({ ...tourQuery(Number(id)), enabled: !isNew }).data ?? null;
  const isTemplate = tour?.isTemplate ?? search.template === true;
  // A subtour: one that exists, or a new one opened from its container's route (`?parent=`).
  const parentId = tour?.parentTourId ?? (isNew ? search.parent : undefined) ?? null;
  const parent = useQuery({ ...tourQuery(parentId ?? 0), enabled: parentId !== null }).data ?? null;
  const ready = useQuery({
    ...tourReadyProblemsQuery(Number(id)),
    enabled: tour !== null && !tour.isTemplate && tour.status === 'Draft',
  }).data;
  const awards: ChoiceOption[] = (useQuery(activeAwardsQuery()).data?.items ?? []).map((award) => ({
    value: String(award.id),
    label: read(award.name),
  }));
  const groups: ChoiceOption[] = (
    useQuery(groupsListQuery(listSearchSchema.parse({ pageSize: 100 }))).data?.items ?? []
  ).map((group) => ({ value: String(group.id), label: read(group.name) }));
  // The rating as the staff say it, with the network's name for it from the core's words.
  const ratings: ChoiceOption[] = (useQuery(tourRatingsQuery()).data ?? []).map((rating) => ({
    value: String(rating.number),
    label: `${rating.shortName} — ${t(`ratings.Pilot.${rating.shortName}`)}`,
  }));
  const save = useSaveTour(isNew ? null : Number(id));
  const { suggestions, onSuggestSearch, suggestSearching } = useTypeSuggestions();
  const writes = useWrites(tour);

  const department =
    tour?.ownerDepartment ??
    writableDepartments(bootstrap, isTemplate ? TOURS_MANAGE_TEMPLATES : TOURS_EDIT)[0];
  const list = isTemplate ? TEMPLATES : TOURS;
  const labels = isTemplate ? 'flightops:tourTemplates' : 'flightops:tours';

  if ((!isNew && tour === null) || department === undefined) {
    return null;
  }

  // A new subtour is never a container: a kind in the address it may not have is not taken.
  const askedKind =
    search.kind !== undefined && (parentId === null || search.kind !== 'Container') ? search.kind : undefined;
  const kind: TourKind = tour?.kind ?? askedKind ?? 'Sequential';
  const steps = tourSteps({ kind, isTemplate });
  const step = openStep({ asked: search.tab, steps, isNew, kindChosen: askedKind !== undefined });
  const problems: Problems = Object.entries(ready?.errors ?? {});
  const localized = ready?.localized ?? {};
  const toFix = stepsToFix(
    problems.map(([field]) => field),
    steps,
  );
  const { previous, next } = neighbours(steps, step);

  // The address of a step: the same screen, the rest of the address kept.
  const base = isNew ? `${TOURS}/new` : `${TOURS}/${id}`;
  const hrefOf = (target: TourStep) => {
    const query = new URLSearchParams();
    if (isNew) {
      if (isTemplate) query.set('template', 'true');
      if (parentId !== null) query.set('parent', String(parentId));
      if (askedKind !== undefined) query.set('kind', askedKind);
    }
    query.set('tab', target);
    return `${base}?${query.toString()}`;
  };
  const goTo = (target: TourStep) => {
    setSavedAt(null);
    void navigate({ href: hrefOf(target) });
  };

  const title = isNew
    ? t(parentId === null ? `${labels}.create` : 'flightops:subtours.create')
    : read(tour?.title ?? {}) || t(`${labels}.edit`);

  // The identity and the rules of a tour are two forms of one payload (#369): each draws its part and carries the rest.
  const tourForm = (part: TourFormPart) => (
    <SchemaForm
      // A new key when the row changes underneath, so the form reloads what the actions wrote — and what the other
      // part saved.
      key={`${part}-${tour?.rowVersion ?? 'new'}`}
      schema={tourSchema({
        types: keepingCurrent(suggestions, [
          ...(tour?.referenceAircraftIcao ? [tour.referenceAircraftIcao] : []),
          ...(tour?.allowedAircraft.types ?? []),
        ]),
        awards,
        groups,
        ratings,
        isTemplate,
        isSubtour: parentId !== null,
        part,
        ...(tour === null ? {} : { kind: tour.kind }),
      })}
      defaults={
        tour === null
          ? { ...emptyTour(department, locales, isTemplate, parentId ?? undefined), kind }
          : tourToFormValues(tour, locales)
      }
      locales={locales}
      labels="flightops:tours"
      // Banner and photo come from the library, where the department that prepares them uploaded them.
      mediaLibrary={mediaPickerQuery(department)}
      division={{
        defaultLocale: bootstrap.division.defaultLocale,
        timezone: bootstrap.division.timezone,
      }}
      onSuggestSearch={onSuggestSearch}
      suggestSearching={suggestSearching}
      onSubmit={async (values) => {
        setSavedAt(null);
        const result = await save.mutateAsync({ values });
        if (isNew) {
          // The tour exists from here: the walk goes on at the step after its name (design 11 §5).
          const after = neighbours(tourSteps({ kind, isTemplate }), 'identity').next ?? 'identity';
          void navigate({ href: `${TOURS}/${result.id}?tab=${after}` });
        } else {
          setSavedAt(part);
        }
      }}
      submitLabel={t('common.save')}
      secondaryAction={
        <Button asChild variant="ghost">
          <RouterAnchor href={list}>{t('common.cancel')}</RouterAnchor>
        </Button>
      }
    />
  );

  // The problems of one step, at its top; at the check, all of them, each with the way to its step.
  const problemsOf = (at: TourStep) => problems.filter(([field]) => stepOfProblem(field, steps) === at);
  const linkTo = (field: string) => {
    const target = stepOfProblem(field, steps);
    return { step: target, href: hrefOf(target) };
  };

  const contentOf = (at: TourStep): ReactNode => {
    if (at === 'kind') {
      return (
        <KindStep
          key={tour?.rowVersion ?? 'new'}
          tour={tour}
          initial={kind}
          isSubtour={parentId !== null}
          editable={tour === null || writes.tour}
          onChosen={(chosen) => {
            const query = new URLSearchParams();
            if (isTemplate) query.set('template', 'true');
            if (parentId !== null) query.set('parent', String(parentId));
            query.set('kind', chosen);
            query.set('tab', 'identity');
            void navigate({ href: `${TOURS}/new?${query.toString()}` });
          }}
          onSaved={() => setSavedAt('kind')}
        />
      );
    }

    if (at === 'identity') {
      return tourForm('identity');
    }

    // Every step after the name needs the tour saved: the draft is born at step 2.
    if (tour === null) {
      return <Notice tone="info" title={t('flightops:tours.steps.notYet')} />;
    }

    switch (at) {
      case 'route':
        return <RouteStep tour={tour} />;
      case 'rules':
        return <RulesStep tour={tour} conditions={tourForm('rules')} />;
      case 'briefing':
        return <TourBriefing tour={tour} />;
      case 'check':
        return (
          <CheckStep
            tour={tour}
            problems={ready === undefined ? undefined : problems}
            localized={localized}
            linkTo={linkTo}
          />
        );
    }
  };

  return (
    <PageShell
      title={title}
      {...(tour === null || tour.isTemplate
        ? {}
        : { description: t(`flightops:tours.options.state.${tour.state}`) })}
      breadcrumb={[
        { label: t('flightops:nav.section') },
        { label: t(`${labels}.title`), to: list },
        // A subtour is reached from its container's route, and goes back there.
        ...(parent === null
          ? []
          : [
              {
                label: read(parent.title) || t('flightops:tours.edit'),
                to: `${TOURS}/${parent.id}?tab=route`,
              },
            ]),
        { label: title },
      ]}
      actions={tour === null ? undefined : <TourActions tour={tour} />}
    >
      {/* The open step is in the address, so a reload or a link lands on it; a briefing not saved is asked about
          before the step changes (`TourBriefing`). Only the open step is mounted. */}
      <Tabs
        // ⚠️ `w-full` is not decoration: Atmosphere pins `Tabs` to `w-[400px]` (HANDOFF §13).
        className="w-full"
        value={step}
        onValueChange={(target) => goTo(target as TourStep)}
        tabs={Object.fromEntries(
          steps.map((at, index) => [
            at,
            {
              trigger: <StepTrigger number={index + 1} step={at} toFix={toFix.has(at)} />,
              content: (
                <div className="flex flex-col gap-6 pt-4">
                  {savedAt === at ? <Notice tone="success" title={t('flightops:tours.saved')} /> : null}
                  {at === 'check' ? null : (
                    <ReadyProblems
                      problems={problemsOf(at)}
                      localized={localized}
                      briefing={readBody(tour?.briefing)}
                    />
                  )}
                  {contentOf(at)}
                  {tour === null ? null : (
                    <StepNavigation
                      previous={at === step ? previous : null}
                      next={at === step ? next : null}
                      hrefOf={hrefOf}
                    />
                  )}
                </div>
              ),
            },
          ]),
        )}
      />
    </PageShell>
  );
}
