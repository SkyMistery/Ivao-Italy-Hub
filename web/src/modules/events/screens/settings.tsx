import { useQuery } from '@tanstack/react-query';
import { useRouteContext } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { SchemaForm } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { Notice, PageShell } from '../../../shared/ui';
import { settingsQuery, useSaveSettings } from '../api';
import { kindChoices, settingsSchema, settingsToFormValues } from '../schemas';

/**
 * The settings of the events (design M4 §1.12): what the events department changes without a release. Kept by the core's
 * settings of a module; the form is generated, and the rules are the server's.
 * <br />The kinds a preset is for are the division's words of the calendar, from the bootstrap: nothing here is typed that
 * could be mistyped.
 */
export function EventsSettingsPage() {
  const { t } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const settings = useQuery(settingsQuery()).data;
  const save = useSaveSettings();

  const calendar = bootstrap.calendarKinds.map((kind) => ({
    value: kind.key,
    label: read(kind.label) || kind.key,
  }));

  return (
    <PageShell
      title={t('events:settings.title')}
      description={t('events:settings.description')}
      breadcrumb={[{ label: t('events:nav.section') }, { label: t('events:settings.title') }]}
    >
      {settings === undefined ? null : (
        <div className="flex flex-col gap-6">
          {save.isSuccess ? <Notice tone="success" title={t('events:settings.saved')} /> : null}
          <SchemaForm
            schema={settingsSchema({ kinds: kindChoices(calendar, settings.kindPresets) })}
            defaults={settingsToFormValues(settings)}
            locales={bootstrap.division.locales}
            labels="events:settings"
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
