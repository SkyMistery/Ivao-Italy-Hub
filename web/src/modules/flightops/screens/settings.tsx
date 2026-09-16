import { useQuery } from '@tanstack/react-query';
import { useRouteContext } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { SchemaForm } from '../../../shared/forms';
import { Notice, PageShell } from '../../../shared/ui';
import { settingsQuery, useSaveSettings } from '../api';
import { settingsSchema, settingsToFormValues } from '../schemas';

/**
 * The settings of the tours (design M2 §1.11): what the department changes without a release — the
 * daily limit, the windows, the two numbers of the estimated time. Kept by the core's settings of a
 * module; the form is generated, the ranges are the server's.
 */
export function FlightOpsSettingsPage() {
  const { t } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const settings = useQuery(settingsQuery()).data;
  const save = useSaveSettings();

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
        </div>
      )}
    </PageShell>
  );
}
