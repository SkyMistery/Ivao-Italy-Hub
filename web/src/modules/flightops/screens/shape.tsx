import { Button } from '@ivao/atmosphere-react';
import { useQuery, type UseQueryOptions } from '@tanstack/react-query';
import { useNavigate, useParams } from '@tanstack/react-router';
import { useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { SchemaForm, type ChoiceOption } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import {
  DataList,
  col,
  listSearchSchema,
  type ColumnSpec,
  type ListSearch,
  type Page,
} from '../../../shared/list';
import { ConfirmDialog, Notice, PageShell } from '../../../shared/ui';
import {
  callsignRuleQuery,
  callsignRuleToFormValues,
  callsignRulesQuery,
  constraintQuery,
  constraintToFormValues,
  constraintsQuery,
  emptyCallsignRule,
  emptyConstraint,
  emptyHub,
  emptyRotation,
  hubQuery,
  hubToFormValues,
  hubsQuery,
  parametersToFormValues,
  rotationQuery,
  rotationToFormValues,
  rotationsQuery,
  subtoursQuery,
  tourLegsQuery,
  tourQuery,
  tourToFormValues,
  useDeleteShapeRow,
  useSaveCallsignRule,
  useSaveConstraint,
  useSaveHub,
  useSaveRotation,
  useSaveTour,
  type CallsignRuleListDto,
  type HubDto,
  type RotationListDto,
  type TourConstraintListDto,
  type TourDetailDto,
  type TourListDto,
} from '../api';
import {
  GOAL_PARAMETERS,
  WAKE_CATEGORIES,
  callsignRuleSchema,
  constraintKindSchema,
  hubSchema,
  openGoalKindSchema,
  openGoalSchema,
  rotationSchema,
  tourConstraintSchema,
  type OpenGoalKind,
  type TourConstraintKind,
  type TourEditorTab,
} from '../schemas';

import { NewButton } from './NewButton';
import { useStaff } from './hooks';

/**
 * The shape of a tour besides its legs (design M2 §1.3, §1.6, §2.6.1, §2.7, §8.3): the tabs of the tour's editor for its
 * hubs and rotations, its subtours, its callsign constraints, and the goal and constraints of an Open tour — generated
 * lists, each row opened in a generated form on a page of its own, as every back office screen of the hub (plan §16.6).
 * Only the legs are a table (T7a).
 */

const TOURS = '/staff/tours';

const hubColumns: readonly ColumnSpec<HubDto>[] = [col.text('icao'), col.number('sort')];

const rotationColumns: readonly ColumnSpec<RotationListDto>[] = [
  col.text('hubIcao'),
  col.number('sort'),
  col.number('size'),
  col.number('legs'),
];

const callsignColumns: readonly ColumnSpec<CallsignRuleListDto>[] = [
  col.badge('mode', 'flightops:callsigns'),
  col.badge('match', 'flightops:callsigns'),
  col.text('value'),
  col.number('legNumber'),
];

const constraintColumns: readonly ColumnSpec<TourConstraintListDto>[] = [
  col.badge('kind', 'flightops:constraints'),
  col.list('values'),
];

const subtourColumns: readonly ColumnSpec<TourListDto>[] = [
  col.localized('title'),
  col.badge('state', 'flightops:tours'),
  col.badge('kind', 'flightops:tours'),
  col.date('releaseAt'),
  col.date('closeAt'),
];

/** A list inside a tab: its page and search kept by the tab, not by the address, which belongs to the tour. */
function TabList<TRow extends { id: number }, TKey extends readonly unknown[]>({
  columns,
  query,
  labels,
  edit,
  create,
}: {
  columns: readonly ColumnSpec<TRow>[];
  query: (search: ListSearch) => UseQueryOptions<Page<TRow>, Error, Page<TRow>, TKey>;
  labels: string;
  edit: ((row: TRow) => string) | null;
  create: ReactNode;
}) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useStaff();
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({ pageSize: 100 }));

  return (
    <DataList
      columns={columns}
      query={query(search)}
      labels={labels}
      locale={i18n.language}
      defaultLocale={bootstrap.division.defaultLocale}
      timezone={bootstrap.division.timezone}
      search={search}
      onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
      {...(create === null ? {} : { toolbar: create })}
      actions={(row) =>
        edit === null ? null : (
          <Button asChild variant="ghost" size="sm">
            <RouterAnchor href={edit(row)}>{t('common.edit')}</RouterAnchor>
          </Button>
        )
      }
    />
  );
}

/** Hubs and rotations of a hub tour (design M2 §1.3). */
export function TourHubsTab({ tour, editable }: { tour: TourDetailDto; editable: boolean }) {
  const { t } = useTranslation();
  const base = `${TOURS}/${tour.id}`;

  return (
    <div className="flex flex-col gap-6">
      <section className="flex flex-col gap-2">
        <h3 className="text-lg font-semibold">{t('flightops:hubs.title')}</h3>
        <TabList
          columns={hubColumns}
          query={(search) => hubsQuery(tour.id, search)}
          labels="flightops:hubs"
          edit={editable ? (row) => `${base}/hubs/${row.id}` : null}
          create={
            editable ? <NewButton href={`${base}/hubs/new`} label={t('flightops:hubs.create')} /> : null
          }
        />
      </section>
      <section className="flex flex-col gap-2">
        <h3 className="text-lg font-semibold">{t('flightops:rotations.title')}</h3>
        <TabList
          columns={rotationColumns}
          query={(search) => rotationsQuery(tour.id, search)}
          labels="flightops:rotations"
          edit={editable ? (row) => `${base}/rotations/${row.id}` : null}
          create={
            editable ? (
              <NewButton href={`${base}/rotations/new`} label={t('flightops:rotations.create')} />
            ) : null
          }
        />
      </section>
    </div>
  );
}

/** The subtours of a container (design M2 §2.7): tours of their own, opened in the tour's own editor. */
export function TourSubtoursTab({ tour, editable }: { tour: TourDetailDto; editable: boolean }) {
  const { t } = useTranslation();

  return (
    <TabList
      columns={subtourColumns}
      query={(search) => subtoursQuery(tour.id, search)}
      labels="flightops:tours"
      edit={(row) => `${TOURS}/${row.id}`}
      create={
        editable ? (
          <NewButton href={`${TOURS}/new?parent=${tour.id}`} label={t('flightops:subtours.create')} />
        ) : null
      }
    />
  );
}

/** The callsign constraints of a tour and of its legs (design M2 §1.6). */
export function TourCallsignsTab({ tour, editable }: { tour: TourDetailDto; editable: boolean }) {
  const { t } = useTranslation();
  const base = `${TOURS}/${tour.id}`;

  return (
    <TabList
      columns={callsignColumns}
      query={(search) => callsignRulesQuery(tour.id, search)}
      labels="flightops:callsigns"
      edit={editable ? (row) => `${base}/callsigns/${row.id}` : null}
      create={
        editable ? <NewButton href={`${base}/callsigns/new`} label={t('flightops:callsigns.create')} /> : null
      }
    />
  );
}

/**
 * The goal of an Open tour, and its filters and sequence rules (design M2 §2.6.1, note 2026-09-22-il-tour-open): the goal
 * is saved with the tour, the constraints are rows of their own.
 */
export function TourOpenTab({ tour, editable }: { tour: TourDetailDto; editable: boolean }) {
  const { t } = useTranslation();
  const base = `${TOURS}/${tour.id}`;

  return (
    <div className="flex flex-col gap-6">
      <section className="flex flex-col gap-2">
        <h3 className="text-lg font-semibold">{t('flightops:open.goal')}</h3>
        <OpenGoalEditor tour={tour} editable={editable} />
      </section>
      <section className="flex flex-col gap-2">
        <h3 className="text-lg font-semibold">{t('flightops:constraints.title')}</h3>
        <p className="text-sm">{t('flightops:constraints.description')}</p>
        <TabList
          columns={constraintColumns}
          query={(search) => constraintsQuery(tour.id, search)}
          labels="flightops:constraints"
          edit={editable ? (row) => `${base}/constraints/${row.id}` : null}
          create={
            editable ? (
              <NewButton href={`${base}/constraints/new`} label={t('flightops:constraints.create')} />
            ) : null
          }
        />
      </section>
    </div>
  );
}

/**
 * The kind of a goal or of a constraint, and what it means: a form of one field applied as it is chosen, and the
 * sentence that says what the kind asks of a pilot. The parameters of the kind are the form below it.
 */
function KindPicker<TKind extends string>({
  schema,
  field,
  value,
  labels,
  explanation,
  onChange,
}: {
  schema: typeof openGoalKindSchema | typeof constraintKindSchema;
  field: 'openGoal' | 'kind';
  value: TKind;
  labels: string;
  explanation: string;
  onChange: ((kind: TKind) => void) | null;
}) {
  const { t } = useTranslation();
  const { bootstrap } = useStaff();

  return (
    <div className="flex flex-col gap-2">
      {onChange === null ? (
        <p className="font-semibold">{t(`${labels}.options.${field}.${value}`)}</p>
      ) : (
        <SchemaForm
          schema={schema as never}
          defaults={{ [field]: value }}
          locales={bootstrap.division.locales}
          labels={labels}
          onChange={(values: Record<string, unknown>) => onChange(values[field] as TKind)}
        />
      )}
      <p className="text-sm">{t(`${explanation}.${value}`)}</p>
    </div>
  );
}

/** The goal: its kind, then the form of its parameters, saved with the tour. Read only for who may not change it. */
function OpenGoalEditor({ tour, editable }: { tour: TourDetailDto; editable: boolean }) {
  const { t } = useTranslation();
  const { bootstrap } = useStaff();
  const locales = bootstrap.division.locales;
  const save = useSaveTour(tour.id);
  const [goal, setGoal] = useState<OpenGoalKind>(tour.openGoal ?? 'Distance');
  const [saved, setSaved] = useState(false);
  // The parameters stored belong to the kind stored: another kind starts empty.
  const stored = tour.openGoal === goal ? tour.openGoalParameters : {};

  if (!editable) {
    return tour.openGoal === null ? (
      <p className="text-sm">{t('flightops:open.noGoal')}</p>
    ) : (
      <KindPicker
        schema={openGoalKindSchema}
        field="openGoal"
        value={tour.openGoal}
        labels="flightops:tours"
        explanation="flightops:open.goals"
        onChange={null}
      />
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <KindPicker
        schema={openGoalKindSchema}
        field="openGoal"
        value={goal}
        labels="flightops:tours"
        explanation="flightops:open.goals"
        onChange={(next: OpenGoalKind) => {
          setGoal(next);
          setSaved(false);
        }}
      />
      {saved ? <Notice tone="success" title={t('flightops:open.goalSaved')} /> : null}
      <SchemaForm
        key={goal}
        schema={openGoalSchema(goal)}
        defaults={{ openGoalParameters: parametersToFormValues(GOAL_PARAMETERS[goal], stored) }}
        locales={locales}
        labels="flightops:tours"
        onSubmit={async (values) => {
          setSaved(false);
          await save.mutateAsync({
            values: tourToFormValues(tour, locales),
            goal: { openGoal: goal, parameters: values.openGoalParameters },
          });
          setSaved(true);
        }}
        submitLabel={t('flightops:open.saveGoal')}
      />
    </div>
  );
}

/**
 * The page a row of a tour's shape is edited on: the tour in the breadcrumb, the generated form, "delete" for a row that
 * exists, and back to the tab it came from.
 */
function ShapeFormPage({
  labels,
  tab,
  isNew,
  children,
  onDelete,
  deleting,
}: {
  labels: string;
  tab: TourEditorTab;
  isNew: boolean;
  children: (tour: TourDetailDto, back: () => void) => ReactNode;
  onDelete: (back: () => void) => void;
  deleting: boolean;
}) {
  const { t } = useTranslation();
  const read = useLocalized();
  const navigate = useNavigate();
  const tourId = Number(useParams({ strict: false }).id);
  const tour = useQuery(tourQuery(tourId)).data ?? null;

  if (tour === null) {
    return null;
  }

  const back = () => void navigate({ href: `${TOURS}/${tourId}?tab=${tab}` });
  const title = t(`${labels}.${isNew ? 'create' : 'edit'}`);

  return (
    <PageShell
      title={title}
      breadcrumb={[
        { label: t('flightops:nav.section') },
        { label: t('flightops:tours.title'), to: TOURS },
        { label: read(tour.title) || t('flightops:tours.edit'), to: `${TOURS}/${tourId}?tab=${tab}` },
        { label: title },
      ]}
      actions={
        isNew ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t(`${labels}.delete.title`)}
            description={t(`${labels}.delete.description`)}
            confirmText={t('common.delete')}
            disabled={deleting}
            onConfirm={() => onDelete(back)}
          />
        )
      }
    >
      {children(tour, back)}
    </PageShell>
  );
}

function useRowId(name: 'hubId' | 'rotationId' | 'ruleId' | 'constraintId'): number | null {
  // The routes of a module are registered from its manifest, so their parameters are not in the router's typed tree.
  const params: Readonly<Record<string, string | undefined>> = useParams({ strict: false });
  const raw = params[name] ?? 'new';
  return raw === 'new' ? null : Number(raw);
}

function Cancel({ href }: { href: string }) {
  const { t } = useTranslation();

  return (
    <Button asChild variant="ghost">
      <RouterAnchor href={href}>{t('common.cancel')}</RouterAnchor>
    </Button>
  );
}

export function HubForm() {
  const { t } = useTranslation();
  const { bootstrap } = useStaff();
  const id = useRowId('hubId');
  const tourId = Number(useParams({ strict: false }).id);
  const hub = useQuery({ ...hubQuery(id ?? 0), enabled: id !== null }).data ?? null;
  const hubs = useQuery(hubsQuery(tourId)).data?.items ?? [];
  const save = useSaveHub(id);
  const remove = useDeleteShapeRow('hubs', tourId);

  if (id !== null && hub === null) {
    return null;
  }

  return (
    <ShapeFormPage
      labels="flightops:hubs"
      tab="hubs"
      isNew={id === null}
      deleting={remove.isPending}
      onDelete={(back) => remove.mutate(id ?? 0, { onSuccess: back })}
    >
      {(tour, back) => (
        <SchemaForm
          schema={hubSchema}
          defaults={hub === null ? emptyHub(tour.id, hubs.length + 1) : hubToFormValues(hub)}
          locales={bootstrap.division.locales}
          labels="flightops:hubs"
          onSubmit={async (values) => {
            await save.mutateAsync(values);
            back();
          }}
          submitLabel={t('common.save')}
          secondaryAction={<Cancel href={`${TOURS}/${tour.id}?tab=hubs`} />}
        />
      )}
    </ShapeFormPage>
  );
}

export function RotationForm() {
  const { t } = useTranslation();
  const { bootstrap } = useStaff();
  const id = useRowId('rotationId');
  const tourId = Number(useParams({ strict: false }).id);
  const rotation = useQuery({ ...rotationQuery(id ?? 0), enabled: id !== null }).data ?? null;
  const hubs: ChoiceOption[] = (useQuery(hubsQuery(tourId)).data?.items ?? []).map((hub) => ({
    value: String(hub.id),
    label: hub.icao,
  }));
  const save = useSaveRotation(id);
  const remove = useDeleteShapeRow('rotations', tourId);

  if (id !== null && rotation === null) {
    return null;
  }

  return (
    <ShapeFormPage
      labels="flightops:rotations"
      tab="hubs"
      isNew={id === null}
      deleting={remove.isPending}
      onDelete={(back) => remove.mutate(id ?? 0, { onSuccess: back })}
    >
      {(tour, back) => (
        <SchemaForm
          schema={rotationSchema(hubs)}
          defaults={
            // The order counts inside one hub, which a new rotation has not chosen yet: first, until somebody says otherwise.
            rotation === null ? emptyRotation(tour.id, 1) : rotationToFormValues(rotation)
          }
          locales={bootstrap.division.locales}
          labels="flightops:rotations"
          onSubmit={async (values) => {
            await save.mutateAsync(values);
            back();
          }}
          submitLabel={t('common.save')}
          secondaryAction={<Cancel href={`${TOURS}/${tour.id}?tab=hubs`} />}
        />
      )}
    </ShapeFormPage>
  );
}

export function CallsignRuleForm() {
  const { t } = useTranslation();
  const { bootstrap } = useStaff();
  const id = useRowId('ruleId');
  const tourId = Number(useParams({ strict: false }).id);
  const rule = useQuery({ ...callsignRuleQuery(id ?? 0), enabled: id !== null }).data ?? null;
  const tour = useQuery(tourQuery(tourId)).data;
  // A template has no legs; a tour offers its own, by number.
  const legs: ChoiceOption[] = (
    useQuery({ ...tourLegsQuery(tourId), enabled: tour !== undefined && !tour.isTemplate }).data?.legs ?? []
  )
    .filter((leg) => leg.retiredAt === null)
    .map((leg) => ({
      value: String(leg.id),
      label: `${t('flightops:legs.row', { number: leg.number })} · ${leg.departureIcao} → ${leg.arrivalIcao}`,
    }));
  const save = useSaveCallsignRule(id);
  const remove = useDeleteShapeRow('callsign-rules', tourId);

  if (id !== null && rule === null) {
    return null;
  }

  return (
    <ShapeFormPage
      labels="flightops:callsigns"
      tab="callsigns"
      isNew={id === null}
      deleting={remove.isPending}
      onDelete={(back) => remove.mutate(id ?? 0, { onSuccess: back })}
    >
      {(current, back) => (
        <SchemaForm
          schema={callsignRuleSchema(legs)}
          defaults={rule === null ? emptyCallsignRule(current.id) : callsignRuleToFormValues(rule)}
          locales={bootstrap.division.locales}
          labels="flightops:callsigns"
          onSubmit={async (values) => {
            await save.mutateAsync(values);
            back();
          }}
          submitLabel={t('common.save')}
          secondaryAction={<Cancel href={`${TOURS}/${current.id}?tab=callsigns`} />}
        />
      )}
    </ShapeFormPage>
  );
}

/**
 * A filter or a sequence rule of an Open tour: its kind chosen first, then the form of its parameters (note
 * 2026-09-22-il-tour-open). The kind of a row that exists does not change: another kind is another row.
 */
export function TourConstraintForm() {
  const { t } = useTranslation();
  const { bootstrap } = useStaff();
  const id = useRowId('constraintId');
  const tourId = Number(useParams({ strict: false }).id);
  const constraint = useQuery({ ...constraintQuery(id ?? 0), enabled: id !== null }).data ?? null;
  const [kind, setKind] = useState<TourConstraintKind>('DepartureOrArrivalIn');
  const save = useSaveConstraint(id);
  const remove = useDeleteShapeRow('tour-constraints', tourId);
  const categories: ChoiceOption[] = WAKE_CATEGORIES.map((value) => ({
    value,
    label: t(`flightops:constraints.options.parameters.categories.${value}`),
  }));

  if (id !== null && constraint === null) {
    return null;
  }

  const chosen = constraint?.kind ?? kind;

  return (
    <ShapeFormPage
      labels="flightops:constraints"
      tab="open"
      isNew={id === null}
      deleting={remove.isPending}
      onDelete={(back) => remove.mutate(id ?? 0, { onSuccess: back })}
    >
      {(tour, back) => (
        <div className="flex flex-col gap-6">
          <KindPicker
            schema={constraintKindSchema}
            field="kind"
            value={chosen}
            labels="flightops:constraints"
            explanation="flightops:constraints.explain"
            onChange={constraint === null ? setKind : null}
          />
          <SchemaForm
            key={chosen}
            schema={tourConstraintSchema(chosen, categories)}
            defaults={
              constraint === null ? emptyConstraint(tour.id, chosen) : constraintToFormValues(constraint)
            }
            locales={bootstrap.division.locales}
            labels="flightops:constraints"
            onSubmit={async (values) => {
              await save.mutateAsync(values);
              back();
            }}
            submitLabel={t('common.save')}
            secondaryAction={<Cancel href={`${TOURS}/${tour.id}?tab=open`} />}
          />
        </div>
      )}
    </ShapeFormPage>
  );
}
