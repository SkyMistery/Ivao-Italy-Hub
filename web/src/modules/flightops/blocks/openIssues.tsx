import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import type { BlockComponentProps } from '../../../shared/modules';

/**
 * `flightops.openIssues` (design M2 §8.2; note 2026-09-23-contestazioni-chiarimenti-segnalazioni §3.4): what waits for the tours'
 * staff besides the queue — the issues on the legs still open, the disputes and the clarifications nobody has answered — three
 * numbers, each a link. Always live and with no property; its other half is `OpenIssuesProvider`, which counts for the reader.
 */

/** What `OpenIssuesProvider` answers with: the department is the contacts queue most of the threads are in. */
export interface OpenIssuesData {
  legIssues?: number;
  disputes?: number;
  clarifications?: number;
  department?: string | null;
}

export function OpenIssuesBlock({ data }: BlockComponentProps) {
  const { t } = useTranslation();
  const counts = data as OpenIssuesData | null | undefined;

  if (counts?.legIssues === undefined) {
    return <p className="text-muted-foreground text-sm">{t('common.loading')}</p>;
  }

  const contacts =
    counts.department === null || counts.department === undefined
      ? null
      : `/staff/${counts.department}/contacts`;
  const lines = [
    { key: 'legIssues', count: counts.legIssues, href: '/staff/tours/issues' },
    { key: 'disputes', count: counts.disputes ?? 0, href: '/staff/tours/review?disputed=true' },
    { key: 'clarifications', count: counts.clarifications ?? 0, href: contacts },
  ].filter((line) => line.count > 0);

  if (lines.length === 0) {
    return <p className="text-muted-foreground text-sm">{t('flightops:blocks.openIssues.empty')}</p>;
  }

  return (
    <ul className="flex flex-col divide-y">
      {lines.map((line) => (
        <li key={line.key} className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1 py-3">
          {line.href === null ? (
            <span className="font-semibold">
              {t(`flightops:blocks.openIssues.${line.key}`, { count: line.count })}
            </span>
          ) : (
            <RouterAnchor href={line.href} className="font-semibold underline">
              {t(`flightops:blocks.openIssues.${line.key}`, { count: line.count })}
            </RouterAnchor>
          )}
        </li>
      ))}
    </ul>
  );
}
