import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { writableDepartments } from '../../../shared/api/bootstrap';
import { SchemaForm, type ChoiceOption } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { DataList, col, listSearchSchema, type ColumnSpec } from '../../../shared/list';
import { Notice, PageShell } from '../../../shared/ui';
import {
  tourQuery,
  toursListQuery,
  useSaveTourAsTemplate,
  useTourFromTemplate,
  type TourListDto,
} from '../api';
import { TOURS_EDIT, TOURS_MANAGE_TEMPLATES } from '../permissions';
import { tourFromTemplateSchema, tourSaveAsTemplateSchema } from '../schemas';

import { NewButton } from './NewButton';
import { useListSearch, useStaff } from './hooks';

/**
 * The tours in the back office (design M2 §8.3): the list of every tour, past, present and future, and the list of
 * templates; "new from a template" and "save as template" (§1.10). The editor of one tour, in steps, is `tourEditor.tsx`
 * (design 11 §5).
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
