import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { ApiError } from '../../../shared/api/problem';
import { SchemaForm } from '../../../shared/forms';
import { DataList, col, listSearchSchema, type ColumnSpec } from '../../../shared/list';
import { Notice, PageShell } from '../../../shared/ui';
import { settingsQuery, toursListQuery, useSaveSettings, type TourListDto } from '../api';
import { settingsSchema, settingsToFormValues } from '../schemas';

import { useStaff } from './hooks';

/** The refusal of switching the daily limit off while some tour has none of its own (design M2 §3.7). */
const TOURS_NEED_DAILY_LIMIT = 'flightops:errors.toursNeedDailyLimit';

const inTheWayColumns: readonly ColumnSpec<TourListDto>[] = [
  col.localized('title'),
  col.badge('state', 'flightops:tours'),
  col.date('releaseAt'),
  col.date('closeAt'),
];

/**
 * The settings of the tours (design M2 §1.11): what the department changes without a release — the
 * daily limit, the windows, the two numbers of the estimated time. Kept by the core's settings of a
 * module; the form is generated, the ranges are the server's.
 * <br />When the division's limit cannot be switched off, the tours in the way are listed under the form, asked of
 * the list of tours with the server's own rule.
 */
export function FlightOpsSettingsPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useStaff();
  const settings = useQuery(settingsQuery()).data;
  const save = useSaveSettings();

  const blockedByTours =
    save.error instanceof ApiError &&
    (save.error.problem?.errors?.['dailyLegLimit'] ?? []).includes(TOURS_NEED_DAILY_LIMIT);

  return (
    <PageShell
      title={t('flightops:settings.title')}
      description={t('flightops:settings.description')}
      breadcrumb={[{ label: t('flightops:nav.section') }, { label: t('flightops:settings.title') }]}
    >
      {settings === undefined ? null : (
        <div className="flex flex-col gap-6">
          {save.isSuccess ? <Notice tone="success" title={t('flightops:settings.saved')} /> : null}
          <SchemaForm
            schema={settingsSchema}
            defaults={settingsToFormValues(settings)}
            locales={bootstrap.division.locales}
            labels="flightops:settings"
            onSubmit={async (values) => {
              await save.mutateAsync(values);
            }}
            submitLabel={t('common.save')}
          />
          {blockedByTours ? (
            <>
              <Notice tone="warning" title={t('flightops:settings.toursInTheWay')} />
              <DataList
                columns={inTheWayColumns}
                query={toursListQuery(listSearchSchema.parse({ pageSize: 100 }), {
                  needsOwnDailyLimit: true,
                })}
                labels="flightops:tours"
                locale={i18n.language}
                defaultLocale={bootstrap.division.defaultLocale}
                timezone={bootstrap.division.timezone}
                search={listSearchSchema.parse({ pageSize: 100 })}
                onSearchChange={() => undefined}
              />
            </>
          ) : null}
        </div>
      )}
    </PageShell>
  );
}
