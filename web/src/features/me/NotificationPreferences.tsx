import { H4, Subtle, Switch } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { notificationPreferencesQuery, useSaveNotificationPreference } from './queries';

/**
 * The words of a kind of notification. A module's kind is named after the module — `flightops.pirepAccepted` —
 * and its words are in the module's namespace, under `notifications.{name}` (M2, T13); a kind of the core is
 * under `notifications.types.{type}` of the common one. The first key that exists wins.
 */
function labelKeys(type: string, part: 'title' | 'description'): string[] {
  const dot = type.indexOf('.');
  return [
    `${type.slice(0, dot)}:notifications.${type.slice(dot + 1)}.${part}`,
    `notifications.types.${type}.${part}`,
  ];
}

/**
 * Which notifications this member wants. One row per kind the server declares, the core's and the
 * enabled modules' (design M1 §5.2).
 *
 * It is not a `SchemaForm`, because it is not a form: there is nothing to fill in and nothing to
 * submit — a switch is the whole interaction, and it saves itself. The kinds come from the server
 * rather than from a list here, so the second kind of notification appears on this screen without
 * anybody touching it.
 */
export function NotificationPreferences() {
  const { t } = useTranslation();
  const { data: preferences } = useQuery(notificationPreferencesQuery);
  const save = useSaveNotificationPreference();

  if (!preferences || preferences.length === 0) {
    return null;
  }

  return (
    <section className="flex flex-col gap-3">
      <H4>{t('notifications.title')}</H4>

      {preferences.map((preference) => (
        <div key={preference.type} className="flex items-start justify-between gap-4">
          <div>
            <p className="text-sm font-medium">{t(labelKeys(preference.type, 'title'))}</p>
            <Subtle>{t(labelKeys(preference.type, 'description'))}</Subtle>
          </div>

          <Switch
            checked={preference.enabled}
            disabled={save.isPending}
            aria-label={t(labelKeys(preference.type, 'title'))}
            onCheckedChange={(enabled: boolean) => save.mutate({ type: preference.type, enabled })}
          />
        </div>
      ))}
    </section>
  );
}
