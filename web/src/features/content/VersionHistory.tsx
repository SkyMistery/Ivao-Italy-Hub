import { Badge } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { useMoment } from '../../shared/i18n/useMoment';
import { personName } from '../../shared/ui';

import { contentVersionsQuery } from './queries';

/**
 * The versions of a row, the newest first (issue #352, note `2026-10-23-la-storia-delle-versioni`): its number, when
 * it went online, who published it — and who approved it, where the kind goes through approval —, and the line written
 * then. In the panel of the row's own properties, for whoever may read the row; a row never published draws nothing.
 *
 * A person whose data was erased is "Deleted person", through the core's `personName`.
 */
export function VersionHistory({ id, timezone }: { id: number; timezone: string }) {
  const { t } = useTranslation();
  const moment = useMoment();

  const versions = useQuery(contentVersionsQuery(id));

  if (versions.data === undefined || versions.data.length === 0) {
    return null;
  }

  return (
    <section aria-label={t('content.versions.title')} className="flex flex-col gap-2 text-sm">
      <h3 className="font-medium">{t('content.versions.title')}</h3>
      <ol className="flex flex-col gap-3">
        {versions.data.map((version) => (
          <li key={version.version} className="flex flex-col gap-0.5">
            <span className="flex flex-wrap items-center gap-2">
              <span className="font-medium">
                {t('content.versions.number', { version: version.version })}
              </span>
              <span className="text-muted-foreground">
                {moment(version.publishedAt, { timeZone: timezone })}
              </span>
              {version.online ? (
                <Badge variant="flat" color="green" text={t('content.versions.online')} />
              ) : null}
            </span>
            <span className="text-muted-foreground">
              {version.approvedBy === null
                ? t('content.versions.publishedBy', {
                    name: personName({ vid: version.publishedBy, name: version.publishedByName }, t),
                  })
                : t('content.versions.approvedBy', {
                    name: personName({ vid: version.approvedBy, name: version.approvedByName }, t),
                  })}
            </span>
            {version.changelog === null ? (
              <span className="text-muted-foreground italic">{t('content.versions.noLine')}</span>
            ) : (
              <span className="break-words">{version.changelog}</span>
            )}
          </li>
        ))}
      </ol>
    </section>
  );
}
