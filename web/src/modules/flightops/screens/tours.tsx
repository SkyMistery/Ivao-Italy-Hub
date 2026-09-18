import { Button, Tabs } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useBlocker, useNavigate, useParams, useSearch } from '@tanstack/react-router';
import { useEffect, useRef, useState } from 'react';
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
import { DataList, col, listSearchSchema, type ColumnSpec } from '../../../shared/list';
import { ConfirmDialog, Notice, PageShell } from '../../../shared/ui';
import {
  emptyTour,
  tourQuery,
  tourReadyProblemsQuery,
  tourToFormValues,
  toursListQuery,
  useDeleteTour,
  useSaveTour,
  useSaveTourAsTemplate,
  useTourFromTemplate,
  useTourStatus,
  type TourDetailDto,
  type TourListDto,
  type TourReadyProblemsDto,
} from '../api';
import { TOURS_DELETE, TOURS_EDIT, TOURS_MANAGE_TEMPLATES } from '../permissions';
import {
  tourEditorSearchSchema,
  tourFromTemplateSchema,
  tourSaveAsTemplateSchema,
  tourSchema,
  type TourEditorTab,
} from '../schemas';

import { NewButton } from './NewButton';
import { keepingCurrent, useListSearch, useStaff, useTypeSuggestions } from './hooks';

/**
 * The tours in the back office (design M2 §8.3): the list of every tour, past, present and future, and the list of
 * templates; the settings of one tour, as a generated form; the bar of what happens to it without the form — ready,
 * back to draft, hidden, shown, deleted, saved as a template (§1.2.1, §1.2.2, §1.10). A tour that exists has tabs: the
 * settings, and the briefing written with the editor of the content (T6b); the legs and the rules are the tabs T7 and
 * T9 add.
 */

export const TOURS = '/staff/tours';
export const TEMPLATES = '/staff/tours/templates';

const tourColumns: readonly ColumnSpec<TourListDto>[] = [
  col.localized('title'),
  col.badge('state', 'flightops:tours'),
  col.badge('kind', 'flightops:tours', { sortable: true }),
  col.date('releaseAt', { sortable: true }),
  col.date('closeAt', { sortable: true }),
  col.boolean('isHidden'),
  col.date('updatedAt', { sortable: true }),
];

const templateColumns: readonly ColumnSpec<TourListDto>[] = [
  col.localized('title'),
  col.badge('kind', 'flightops:tours', { sortable: true }),
  col.date('updatedAt', { sortable: true }),
];

function TourList({ isTemplate }: { isTemplate: boolean }) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useStaff();
  const { search, onSearchChange } = useListSearch();

  const labels = isTemplate ? 'flightops:tourTemplates' : 'flightops:tours';
  const editable =
    writableDepartments(bootstrap, isTemplate ? TOURS_MANAGE_TEMPLATES : TOURS_EDIT).length > 0;
  const create = editable ? (
    <div className="flex flex-wrap gap-2">
      <NewButton
        href={isTemplate ? `${TOURS}/new?template=true` : `${TOURS}/new`}
        label={t(`${labels}.create`)}
      />
      {isTemplate ? null : (
        <NewButton
          href={`${TOURS}/from-template`}
          label={t('flightops:tours.fromTemplate')}
          variant="outline"
        />
      )}
    </div>
  ) : null;

  return (
    <PageShell
      title={t(`${labels}.title`)}
      description={t(`${labels}.description`)}
      breadcrumb={[{ label: t('flightops:nav.section') }, { label: t(`${labels}.title`) }]}
      actions={create ?? undefined}
    >
      <DataList
        columns={isTemplate ? templateColumns : tourColumns}
        query={toursListQuery(search, { isTemplate })}
        labels="flightops:tours"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={onSearchChange}
        actions={(row) => (
          <Button asChild variant="ghost" size="sm">
            <RouterAnchor href={`${TOURS}/${row.id}`}>{t('common.edit')}</RouterAnchor>
          </Button>
        )}
        {...(create === null ? {} : { emptyAction: create })}
      />
    </PageShell>
  );
}

export function ToursPage() {
  return <TourList isTemplate={false} />;
}

export function TourTemplatesPage() {
  return <TourList isTemplate />;
}

/**
 * What stands between a draft and "ready", before anybody presses it: the server's answer to the very checks the
 * action runs, one line per field, the missing languages named.
 */
function ReadyProblems({
  problems,
  briefing,
}: {
  problems: TourReadyProblemsDto | undefined;
  briefing: Body;
}) {
  const { t, i18n } = useTranslation();
  // A path inside the briefing — `briefing.sections[0].blocks[1].props.text` — read against the briefing as saved, the
  // way the editor of the content reads its own (`publishProblems.tsx`): the checks ran on the saved row too.
  const describe = useBodyPathDescription(briefing);
  const entries = Object.entries(problems?.errors ?? {});

  if (entries.length === 0) {
    return null;
  }

  return (
    <Notice
      tone="warning"
      title={t('flightops:tours.readyProblems')}
      description={
        <ul className="list-disc pl-5">
          {entries.map(([field, keys]) => {
            const missing = problems?.localized[field] ?? [];
            const label = field.startsWith('briefing.')
              ? `${t('flightops:tours.fields.briefing')} › ${describe(field)}`
              : t(`flightops:tours.fields.${field.split(/[.[]/)[0] ?? field}`);
            const sentence =
              missing.length > 0
                ? t('errors.localized.missingIn', { locales: languageNames([...missing], i18n.language) })
                : keys.map((key) => t(key)).join(' ');

            return (
              <li key={field}>
                {label}: {sentence}
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
  const refusal = describeProblem(status.error ?? remove.error, t, i18n.language);

  return (
    <div className="flex flex-col items-end gap-2">
      <div className="flex flex-wrap justify-end gap-2">
        {tour.isTemplate ? null : tour.status === 'Draft' ? (
          <Button disabled={status.isPending} onClick={() => status.mutate('Ready')}>
            {t('flightops:tours.actions.ready')}
          </Button>
        ) : released ? null : (
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

  // On the way out — another tab, another screen, the browser's tab closed — a briefing not saved is asked about, in
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

export function TourEditor() {
  const { t } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useStaff();
  const navigate = useNavigate();
  const id = String(useParams({ strict: false }).id ?? 'new');
  const search = tourEditorSearchSchema.parse(useSearch({ strict: false }));
  const isNew = id === 'new';
  const locales = bootstrap.division.locales;
  const [saved, setSaved] = useState(false);
  const tab: TourEditorTab = search.tab ?? 'settings';

  const tour = useQuery({ ...tourQuery(Number(id)), enabled: !isNew }).data ?? null;
  const isTemplate = tour?.isTemplate ?? search.template === true;
  const problems = useQuery({
    ...tourReadyProblemsQuery(Number(id)),
    enabled: tour !== null && !tour.isTemplate && tour.status === 'Draft',
  }).data;
  const awards: ChoiceOption[] = (useQuery(activeAwardsQuery()).data?.items ?? []).map((award) => ({
    value: String(award.id),
    label: read(award.name),
  }));
  const save = useSaveTour(isNew ? null : Number(id));
  const { suggestions, onSuggestSearch } = useTypeSuggestions();

  const department =
    tour?.ownerDepartment ??
    writableDepartments(bootstrap, isTemplate ? TOURS_MANAGE_TEMPLATES : TOURS_EDIT)[0];
  const list = isTemplate ? TEMPLATES : TOURS;
  const labels = isTemplate ? 'flightops:tourTemplates' : 'flightops:tours';

  if ((!isNew && tour === null) || department === undefined) {
    return null;
  }

  const title = isNew ? t(`${labels}.create`) : read(tour?.title ?? {}) || t(`${labels}.edit`);

  const settings = (
    <SchemaForm
      // A new key when the row changes underneath, so the form reloads what the actions wrote.
      key={tour?.rowVersion ?? 'new'}
      schema={tourSchema({
        types: keepingCurrent(suggestions, tour?.referenceAircraftIcao ? [tour.referenceAircraftIcao] : []),
        awards,
        isTemplate,
        kindLocked: tour?.isPublic ?? false,
      })}
      defaults={tour === null ? emptyTour(department, locales, isTemplate) : tourToFormValues(tour, locales)}
      locales={locales}
      labels="flightops:tours"
      // Banner and photo come from the library, where the department that prepares them uploaded them.
      mediaLibrary={mediaPickerQuery(department)}
      division={{
        defaultLocale: bootstrap.division.defaultLocale,
        timezone: bootstrap.division.timezone,
      }}
      onSuggestSearch={onSuggestSearch}
      onSubmit={async (values) => {
        setSaved(false);
        const result = await save.mutateAsync({ values });
        if (isNew) {
          void navigate({ href: `${TOURS}/${result.id}` });
        } else {
          setSaved(true);
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

  return (
    <PageShell
      title={title}
      {...(tour === null || tour.isTemplate
        ? {}
        : { description: t(`flightops:tours.options.state.${tour.state}`) })}
      breadcrumb={[
        { label: t('flightops:nav.section') },
        { label: t(`${labels}.title`), to: list },
        { label: title },
      ]}
      actions={tour === null ? undefined : <TourActions tour={tour} />}
    >
      <div className="flex flex-col gap-6">
        {saved && tab === 'settings' ? <Notice tone="success" title={t('flightops:tours.saved')} /> : null}
        <ReadyProblems problems={problems} briefing={readBody(tour?.briefing)} />
        {tour === null ? (
          settings
        ) : (
          // The open tab is in the address, so a reload or a link lands on it; a briefing not saved is asked about
          // before the tab changes (`TourBriefing`). Only the open tab is mounted.
          <Tabs
            // ⚠️ `w-full` is not decoration: Atmosphere pins `Tabs` to `w-[400px]` (HANDOFF §13).
            className="w-full"
            value={tab}
            onValueChange={(next) =>
              void navigate({
                search: ((previous: Record<string, unknown>) => ({ ...previous, tab: next })) as never,
                to: '.',
              })
            }
            tabs={{
              settings: {
                trigger: t('flightops:tours.tabs.settings'),
                content: <div className="pt-4">{settings}</div>,
              },
              briefing: {
                trigger: t('flightops:tours.tabs.briefing'),
                content: (
                  <div className="pt-4">
                    <TourBriefing tour={tour} />
                  </div>
                ),
              },
            }}
          />
        )}
      </div>
    </PageShell>
  );
}

export function TourFromTemplatePage() {
  const { t } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useStaff();
  const navigate = useNavigate();
  const create = useTourFromTemplate();
  const locales = bootstrap.division.locales;

  const page = useQuery(toursListQuery(listSearchSchema.parse({ pageSize: 100 }), { isTemplate: true })).data;
  const templates: ChoiceOption[] = (page?.items ?? []).map((template) => ({
    value: String(template.id),
    label: read(template.title),
  }));
  const title = t('flightops:tours.fromTemplate');

  return (
    <PageShell
      title={title}
      description={t('flightops:tourFromTemplate.description')}
      breadcrumb={[
        { label: t('flightops:nav.section') },
        { label: t('flightops:tours.title'), to: TOURS },
        { label: title },
      ]}
    >
      {page === undefined ? null : templates.length === 0 ? (
        <Notice tone="info" title={t('flightops:tourFromTemplate.none')} />
      ) : (
        <SchemaForm
          schema={tourFromTemplateSchema(templates)}
          defaults={{
            templateId: templates[0]?.value ?? '',
            title: Object.fromEntries(locales.map((locale) => [locale, ''])),
            slug: '',
          }}
          locales={locales}
          labels="flightops:tourFromTemplate"
          division={{
            defaultLocale: bootstrap.division.defaultLocale,
            timezone: bootstrap.division.timezone,
          }}
          onSubmit={async (values) => {
            const tour = await create.mutateAsync(values);
            void navigate({ href: `${TOURS}/${tour.id}` });
          }}
          submitLabel={t('flightops:tourFromTemplate.submit')}
          secondaryAction={
            <Button asChild variant="ghost">
              <RouterAnchor href={TOURS}>{t('common.cancel')}</RouterAnchor>
            </Button>
          }
        />
      )}
    </PageShell>
  );
}

export function TourSaveAsTemplatePage() {
  const { t } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useStaff();
  const navigate = useNavigate();
  const id = Number(useParams({ strict: false }).id);
  const locales = bootstrap.division.locales;

  const tour = useQuery(tourQuery(id)).data;
  const saveAs = useSaveTourAsTemplate(id);

  if (tour === undefined) {
    return null;
  }

  const title = t('flightops:tours.actions.saveAsTemplate');

  return (
    <PageShell
      title={title}
      description={t('flightops:tourSaveAsTemplate.description')}
      breadcrumb={[
        { label: t('flightops:nav.section') },
        { label: t('flightops:tours.title'), to: TOURS },
        { label: read(tour.title), to: `${TOURS}/${tour.id}` },
        { label: title },
      ]}
    >
      <SchemaForm
        schema={tourSaveAsTemplateSchema}
        defaults={{ title: Object.fromEntries(locales.map((locale) => [locale, tour.title[locale] ?? ''])) }}
        locales={locales}
        labels="flightops:tourSaveAsTemplate"
        onSubmit={async (values) => {
          const template = await saveAs.mutateAsync(values);
          void navigate({ href: `${TOURS}/${template.id}` });
        }}
        submitLabel={t('common.save')}
        secondaryAction={
          <Button asChild variant="ghost">
            <RouterAnchor href={`${TOURS}/${tour.id}`}>{t('common.cancel')}</RouterAnchor>
          </Button>
        }
      />
    </PageShell>
  );
}
