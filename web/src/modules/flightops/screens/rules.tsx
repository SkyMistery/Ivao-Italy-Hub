import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams, useSearch } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { writableDepartments } from '../../../shared/api/bootstrap';
import { SchemaForm, type ChoiceOption } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { DataList, col, listSearchSchema, type ColumnSpec } from '../../../shared/list';
import { ConfirmDialog, Notice, PageShell } from '../../../shared/ui';
import {
  allErrorsQuery,
  effectiveRulesQuery,
  parametersToFormValues,
  emptyError,
  emptyRule,
  errorQuery,
  errorToFormValues,
  errorsListQuery,
  generalRulesQuery,
  ruleQuery,
  ruleToFormValues,
  rulesListQuery,
  tourRulesQuery,
  toursListQuery,
  useCopyRules,
  useDeleteError,
  useDeleteRule,
  useSaveError,
  useSaveRule,
  type CopyRulesResultDto,
  type TourDetailDto,
  type TourErrorListDto,
  type TourRuleDto,
  type TourRuleListDto,
} from '../api';
import { TOURS_MANAGE_RULES } from '../permissions';
import {
  EQUIPMENT_LETTERS,
  TRANSPONDER_LETTERS,
  checkKeySchema,
  checkParameters,
  copyRulesSchema,
  ruleSchema,
  tourErrorSchema,
  tourRuleSearchSchema,
  type CheckChoice,
  type RuleFormValues,
} from '../schemas';

import { NewButton } from './NewButton';
import { useListSearch, useRowId, useStaff } from './hooks';
import { Cancel, KindPicker, ShapeFormPage, TabList } from './shape';

/**
 * Rules and errors (design M2 §1.7, §5): the general rules and the division's errors on screens of their own, and a
 * tour's rules in a tab of its editor — its rules in force, its own rules, "amend" next to each general one, and "copy the
 * rules of another tour". Lists and forms generated; the parameters of a rule are the form of the check chosen above it,
 * as the constraints of an Open tour are (T7c).
 */

const RULES = '/staff/tours/rules';
const ERRORS = '/staff/tours/errors';
const TOURS = '/staff/tours';

const ruleColumns: readonly ColumnSpec<TourRuleListDto>[] = [
  col.text('code', { sortable: true }),
  col.localized('title'),
  col.badge('checkKey', 'flightops:checks'),
  col.list('values'),
  col.number('errors'),
  col.boolean('active'),
];

const tourRuleColumns: readonly ColumnSpec<TourRuleListDto>[] = [
  col.text('code'),
  col.localized('title'),
  col.text('amendsCode'),
  col.list('values'),
  col.number('errors'),
  col.boolean('active'),
];

const errorColumns: readonly ColumnSpec<TourErrorListDto>[] = [
  col.localized('name'),
  col.badge('category', 'flightops:tourErrors', { sortable: true }),
  col.number('yearlyMax'),
  col.badge('checkKey', 'flightops:checks'),
  col.boolean('isPublic'),
  col.number('rules'),
  col.boolean('active'),
];

const LETTERS: ChoiceOption[] = EQUIPMENT_LETTERS.map((letter) => ({ value: letter, label: letter }));
const TRANSPONDER: ChoiceOption[] = TRANSPONDER_LETTERS.map((letter) => ({ value: letter, label: letter }));

/** Who may write rules and errors: `Tours.ManageRules` on some department. */
function useWritesRules() {
  const { bootstrap } = useStaff();
  return writableDepartments(bootstrap, TOURS_MANAGE_RULES).length > 0;
}

/** The errors a rule may be linked to, by name and weight; a retired one only while a rule still names it. */
function useErrorChoices(current: readonly number[]): ChoiceOption[] {
  const { t } = useTranslation();
  const read = useLocalized();
  const errors = useQuery(allErrorsQuery()).data?.items ?? [];

  return errors
    .filter((error) => error.active || current.includes(error.id))
    .map((error) => ({
      value: String(error.id),
      label: `${read(error.name)} · ${t(`flightops:tourErrors.options.category.${error.category}`)}`,
    }));
}

export function RulesPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useStaff();
  const { search, onSearchChange } = useListSearch();
  const writes = useWritesRules();
  const create = writes ? <NewButton href={`${RULES}/new`} label={t('flightops:rules.create')} /> : null;

  return (
    <PageShell
      title={t('flightops:rules.title')}
      description={t('flightops:rules.description')}
      breadcrumb={[{ label: t('flightops:nav.section') }, { label: t('flightops:rules.title') }]}
      actions={create ?? undefined}
    >
      <DataList
        columns={ruleColumns}
        query={rulesListQuery(search)}
        labels="flightops:rules"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={onSearchChange}
        actions={(row) =>
          writes ? (
            <Button asChild variant="ghost" size="sm">
              <RouterAnchor href={`${RULES}/${row.id}`}>{t('common.edit')}</RouterAnchor>
            </Button>
          ) : null
        }
        {...(create === null ? {} : { emptyAction: create })}
      />
    </PageShell>
  );
}

/**
 * The form of a rule, whoever's it is: the check picked first — not for an amendment, whose check is its rule's — then the
 * rule and the parameters of that check. An amendment says above the form what it amends and with which values.
 */
function RuleEditor({
  rule,
  defaults,
  amends,
  onSaved,
  cancelHref,
}: {
  rule: TourRuleDto | null;
  defaults: RuleFormValues;
  amends: TourRuleDto | null;
  onSaved: () => void;
  cancelHref: string;
}) {
  const { t } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useStaff();
  const [check, setCheck] = useState<CheckChoice>(defaults.checkKey);
  const save = useSaveRule(rule?.id ?? null);
  const errors = useErrorChoices(rule?.errorIds ?? []);
  const amending = defaults.amendsRuleId !== undefined;

  return (
    <div className="flex flex-col gap-6">
      {amending ? (
        <Notice
          tone="info"
          title={t('flightops:rules.amending', {
            code: amends?.code ?? '',
            title: amends === null ? '' : read(amends.title),
          })}
          description={t('flightops:rules.amendingHint')}
        />
      ) : (
        <KindPicker
          schema={checkKeySchema}
          field="checkKey"
          value={check}
          labels="flightops:checks"
          explanation="flightops:checks.explain"
          onChange={setCheck}
        />
      )}
      <SchemaForm
        key={check}
        schema={ruleSchema(check, { errors, letters: LETTERS, transponder: TRANSPONDER })}
        defaults={withCheck(defaults, check)}
        locales={bootstrap.division.locales}
        labels="flightops:rules"
        onSubmit={async (values) => {
          await save.mutateAsync({ ...values, checkKey: check });
          onSaved();
        }}
        submitLabel={t('common.save')}
        secondaryAction={<Cancel href={cancelHref} />}
      />
    </div>
  );
}

/** The defaults with another check: its parameters start empty, as a constraint of another kind does (T7c). */
function withCheck(defaults: RuleFormValues, check: CheckChoice): RuleFormValues {
  if (check === defaults.checkKey) {
    return defaults;
  }

  const rest: RuleFormValues = { ...defaults };
  delete rest.parameters;
  const shape = checkParameters(check);

  return {
    ...rest,
    checkKey: check,
    ...(Object.keys(shape).length === 0 ? {} : { parameters: parametersToFormValues(shape, {}) }),
  };
}

export function RuleForm() {
  const { t } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useStaff();
  const navigate = useNavigate();
  const id = String(useParams({ strict: false }).id ?? 'new');
  const isNew = id === 'new';
  const rule = useQuery({ ...ruleQuery(Number(id)), enabled: !isNew }).data ?? null;
  const remove = useDeleteRule();

  if (!isNew && rule === null) {
    return null;
  }

  const back = () => void navigate({ href: RULES });
  const title = rule === null ? t('flightops:rules.create') : `${rule.code} · ${read(rule.title)}`;

  return (
    <PageShell
      title={title}
      breadcrumb={[
        { label: t('flightops:nav.section') },
        { label: t('flightops:rules.title'), to: RULES },
        { label: title },
      ]}
      actions={
        isNew ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('flightops:rules.delete.title')}
            description={t('flightops:rules.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(Number(id), { onSuccess: back })}
          />
        )
      }
    >
      <RuleEditor
        rule={rule}
        defaults={
          rule === null
            ? emptyRule(bootstrap.division.locales)
            : ruleToFormValues(rule, bootstrap.division.locales)
        }
        amends={null}
        onSaved={back}
        cancelHref={RULES}
      />
    </PageShell>
  );
}

/** A rule of a tour, on the page of its tour: its own, or the amendment of a general one. */
export function TourRuleForm() {
  const { bootstrap } = useStaff();
  const id = useRowId('tourRuleId');
  const tourId = Number(useParams({ strict: false }).id);
  const { amends: amendsId } = tourRuleSearchSchema.parse(useSearch({ strict: false }));
  const rule = useQuery({ ...ruleQuery(id ?? 0), enabled: id !== null }).data ?? null;
  const amendedId = rule?.amendsRuleId ?? amendsId ?? null;
  const amends = useQuery({ ...ruleQuery(amendedId ?? 0), enabled: amendedId !== null }).data ?? null;
  const remove = useDeleteRule();
  const locales = bootstrap.division.locales;

  if ((id !== null && rule === null) || (amendedId !== null && amends === null)) {
    return null;
  }

  return (
    <ShapeFormPage
      labels="flightops:rules"
      tab="rules"
      isNew={id === null}
      deleting={remove.isPending}
      onDelete={(back) => remove.mutate(id ?? 0, { onSuccess: back })}
    >
      {(tour, back) => (
        <RuleEditor
          rule={rule}
          defaults={
            rule === null
              ? emptyRule(locales, { tourId: tour.id, ...(amends === null ? {} : { amends }) })
              : ruleToFormValues(rule, locales)
          }
          amends={amends}
          onSaved={back}
          cancelHref={`${TOURS}/${tourId}?tab=rules`}
        />
      )}
    </ShapeFormPage>
  );
}

/**
 * The rules of a tour (design M2 §5.1): what holds on it, its own rules, the general ones with "amend", and the copy from
 * another tour. Read only for who may not write rules.
 */
export function TourRulesTab({ tour, editable }: { tour: TourDetailDto; editable: boolean }) {
  const { t } = useTranslation();
  const base = `${TOURS}/${tour.id}`;
  const own = useQuery(tourRulesQuery(tour.id)).data?.items ?? [];
  const amended = new Map(
    own
      .filter((rule) => rule.amendsRuleId !== null && rule.active)
      .map((rule) => [rule.amendsRuleId, rule.id]),
  );

  return (
    <div className="flex flex-col gap-6">
      <section className="flex flex-col gap-2">
        <h3 className="text-lg font-semibold">{t('flightops:rules.effective')}</h3>
        <p className="text-sm">{t('flightops:rules.effectiveDescription')}</p>
        <EffectiveRules tourId={tour.id} />
      </section>
      <section className="flex flex-col gap-2">
        <h3 className="text-lg font-semibold">{t('flightops:rules.tourRules')}</h3>
        <TabList
          columns={tourRuleColumns}
          query={(search) => tourRulesQuery(tour.id, search)}
          labels="flightops:rules"
          edit={editable ? (row) => `${base}/rules/${row.id}` : null}
          create={
            editable ? <NewButton href={`${base}/rules/new`} label={t('flightops:rules.createOwn')} /> : null
          }
        />
      </section>
      <section className="flex flex-col gap-2">
        <h3 className="text-lg font-semibold">{t('flightops:rules.general')}</h3>
        <TabList
          columns={ruleColumns}
          query={() => generalRulesQuery()}
          labels="flightops:rules"
          edit={null}
          create={null}
          actions={(row) => {
            if (!editable || !row.active) {
              return null;
            }

            const amendment = amended.get(row.id);
            return (
              <Button asChild variant="ghost" size="sm">
                <RouterAnchor
                  href={
                    amendment === undefined
                      ? `${base}/rules/new?amends=${row.id}`
                      : `${base}/rules/${amendment}`
                  }
                >
                  {t(amendment === undefined ? 'flightops:rules.amend' : 'flightops:rules.editAmendment')}
                </RouterAnchor>
              </Button>
            );
          }}
        />
      </section>
      {editable ? (
        <section className="flex flex-col gap-2">
          <h3 className="text-lg font-semibold">{t('flightops:rules.copy.title')}</h3>
          <p className="text-sm">{t('flightops:rules.copy.description')}</p>
          <CopyRules tour={tour} />
        </section>
      ) : null}
    </div>
  );
}

/** The rules in force, as the server composes them: each with what it amends and the values of its parameters. */
function EffectiveRules({ tourId }: { tourId: number }) {
  const { t } = useTranslation();
  const read = useLocalized();
  const rules = useQuery(effectiveRulesQuery(tourId)).data;

  if (rules === undefined) {
    return null;
  }

  if (rules.length === 0) {
    return <p className="text-sm">{t('flightops:rules.noneInForce')}</p>;
  }

  return (
    <ul className="flex flex-col gap-1" data-testid="effective-rules">
      {rules.map((rule) => (
        <li key={rule.id} className="flex flex-wrap items-baseline gap-x-2 text-sm">
          <span className="font-semibold">{rule.code}</span>
          <span>{read(rule.title)}</span>
          {rule.amendsCode === null ? null : (
            <span className="text-xs">{t('flightops:rules.amends', { code: rule.amendsCode })}</span>
          )}
          {rule.values.length === 0 ? null : <span className="text-xs">{rule.values.join(' · ')}</span>}
        </li>
      ))}
    </ul>
  );
}

/** "Copy the rules of another tour": the tour chosen, and what the copy did. */
function CopyRules({ tour }: { tour: TourDetailDto }) {
  const { t } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useStaff();
  const copy = useCopyRules(tour.id);
  const [result, setResult] = useState<CopyRulesResultDto | null>(null);
  // Any tour but this one, templates and subtours included: a regulation is often written once and reused.
  const tours: ChoiceOption[] = [
    ...(useQuery(toursListQuery(listSearchSchema.parse({ pageSize: 100 }))).data?.items ?? []),
    ...(useQuery(toursListQuery(listSearchSchema.parse({ pageSize: 100 }), { isTemplate: true })).data
      ?.items ?? []),
  ]
    .filter((other) => other.id !== tour.id)
    .map((other) => ({ value: String(other.id), label: read(other.title) || `#${other.id}` }));

  return (
    <div className="flex flex-col gap-2">
      {result === null ? null : (
        <Notice
          tone="success"
          title={t('flightops:rules.copy.done', { count: result.copied })}
          {...(result.skipped.length === 0
            ? {}
            : { description: t('flightops:rules.copy.skipped', { codes: result.skipped.join(', ') }) })}
        />
      )}
      <SchemaForm
        schema={copyRulesSchema(tours)}
        defaults={{ sourceTourId: '' }}
        locales={bootstrap.division.locales}
        labels="flightops:rules.copy"
        onSubmit={async (values) => {
          setResult(null);
          setResult(await copy.mutateAsync(values));
        }}
        submitLabel={t('flightops:rules.copy.submit')}
      />
    </div>
  );
}

export function ErrorsPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useStaff();
  const { search, onSearchChange } = useListSearch();
  const writes = useWritesRules();
  const create = writes ? (
    <NewButton href={`${ERRORS}/new`} label={t('flightops:tourErrors.create')} />
  ) : null;

  return (
    <PageShell
      title={t('flightops:tourErrors.title')}
      description={t('flightops:tourErrors.description')}
      breadcrumb={[{ label: t('flightops:nav.section') }, { label: t('flightops:tourErrors.title') }]}
      actions={create ?? undefined}
    >
      <DataList
        columns={errorColumns}
        query={errorsListQuery(search)}
        labels="flightops:tourErrors"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={onSearchChange}
        actions={(row) =>
          writes ? (
            <Button asChild variant="ghost" size="sm">
              <RouterAnchor href={`${ERRORS}/${row.id}`}>{t('common.edit')}</RouterAnchor>
            </Button>
          ) : null
        }
        {...(create === null ? {} : { emptyAction: create })}
      />
    </PageShell>
  );
}

/** An error: the check whose failure suggests it picked first, then the error itself. */
export function ErrorForm() {
  const { t } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useStaff();
  const navigate = useNavigate();
  const id = String(useParams({ strict: false }).id ?? 'new');
  const isNew = id === 'new';
  const locales = bootstrap.division.locales;
  const error = useQuery({ ...errorQuery(Number(id)), enabled: !isNew }).data ?? null;
  const save = useSaveError(isNew ? null : Number(id));
  const remove = useDeleteError();
  const [check, setCheck] = useState<CheckChoice | null>(null);

  if (!isNew && error === null) {
    return null;
  }

  const back = () => void navigate({ href: ERRORS });
  const defaults = error === null ? emptyError(locales) : errorToFormValues(error, locales);
  const chosen = check ?? defaults.checkKey;
  const title = isNew
    ? t('flightops:tourErrors.create')
    : read(error?.name ?? {}) || t('flightops:tourErrors.edit');

  return (
    <PageShell
      title={title}
      breadcrumb={[
        { label: t('flightops:nav.section') },
        { label: t('flightops:tourErrors.title'), to: ERRORS },
        { label: title },
      ]}
      actions={
        isNew ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('flightops:tourErrors.delete.title')}
            description={t('flightops:tourErrors.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(Number(id), { onSuccess: back })}
          />
        )
      }
    >
      <div className="flex flex-col gap-6">
        <KindPicker
          schema={checkKeySchema}
          field="checkKey"
          value={chosen}
          labels="flightops:checks"
          explanation="flightops:checks.explain"
          onChange={setCheck}
        />
        <SchemaForm
          schema={tourErrorSchema}
          defaults={defaults}
          locales={locales}
          labels="flightops:tourErrors"
          onSubmit={async (values) => {
            await save.mutateAsync({ ...values, checkKey: chosen });
            back();
          }}
          submitLabel={t('common.save')}
          secondaryAction={<Cancel href={ERRORS} />}
        />
      </div>
    </PageShell>
  );
}
