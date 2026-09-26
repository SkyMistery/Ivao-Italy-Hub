import { useQuery } from '@tanstack/react-query';
import { useRouteContext } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { SchemaForm, type Suggestion } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { Notice, PageShell } from '../../../shared/ui';
import { positionsQuery, ratingsQuery, settingsQuery, useSaveSettings } from '../api';
import { settingsSchema, settingsToFormValues } from '../schemas';

import { ratingOptions } from './ratings';

/**
 * The settings of the training (design M3 §1.6): what the department changes without a release. Kept by the core's
 * settings of a module; the form is generated, and the rules are the server's.
 * <br />The ratings, the positions and the kinds of the calendar are chosen from what the hub knows — the first two asked
 * of the module, which asks the core; the kinds from the bootstrap — so nothing here is typed that could be mistyped.
 */
export function TrainingSettingsPage() {
  const { t } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const settings = useQuery(settingsQuery()).data;
  const ratings = useQuery(ratingsQuery()).data;
  const positions = useQuery(positionsQuery()).data;
  const save = useSaveSettings();

  const kinds = bootstrap.calendarKinds.map((kind) => ({
    value: kind.key,
    label: read(kind.label) || kind.key,
  }));

  return (
    <PageShell
      title={t('training:settings.title')}
      description={t('training:settings.description')}
      breadcrumb={[{ label: t('training:nav.section') }, { label: t('training:settings.title') }]}
    >
      {settings === undefined || ratings === undefined || positions === undefined ? null : (
        <div className="flex flex-col gap-6">
          {save.isSuccess ? <Notice tone="success" title={t('training:settings.saved')} /> : null}
          <SchemaForm
            schema={settingsSchema({
              ratings: ratingOptions(ratings, t),
              kinds,
              positions: [
                // A position the core no longer has stays on offer while it is on the list, so that it can be seen and taken out.
                ...settings.hiddenPositions
                  .filter((callsign) => !positions.some((position) => position.callsign === callsign))
                  .map((callsign): Suggestion => ({ value: callsign, label: callsign })),
                ...positions.map((position): Suggestion => ({
                  value: position.callsign,
                  label: `${position.callsign} — ${position.name}`,
                  group: position.ratingShortName,
                })),
              ],
            })}
            defaults={settingsToFormValues(
              settings,
              kinds.map((kind) => kind.value),
            )}
            locales={bootstrap.division.locales}
            labels="training:settings"
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
