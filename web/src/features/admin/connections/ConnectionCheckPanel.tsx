import { Badge, Button, Subtle } from '@ivao/atmosphere-react';
import { useTranslation } from 'react-i18next';

import { useMoment } from '../../../shared/i18n/useMoment';
import { Notice, SectionHeader } from '../../../shared/ui';

import { useConnectionCheck, type ArchiveCheck, type HubDatabaseCheck } from './queries';

/**
 * The connection check of the super administrator (note `2026-10-16-la-prova-dei-collegamenti`): one button, and what the
 * hub found on its own database and on the ATC archive. The server answers the **kind** of a failure; the words are here,
 * in the core's language files, and the server never sends the connection string, the user, the host or its own message.
 */
export function ConnectionCheckPanel() {
  const { t } = useTranslation();
  const check = useConnectionCheck();

  return (
    <section className="flex flex-col gap-4">
      <div>
        <Button onClick={() => check.mutate()} disabled={check.isPending}>
          {check.isPending ? t('connections.running') : t('connections.run')}
        </Button>
      </div>

      {check.isError ? <Notice tone="error" title={t('connections.failed')} /> : null}

      {check.data === undefined ? null : (
        <div className="flex flex-col gap-6">
          <DatabaseResult result={check.data.database} />
          <ArchiveResult result={check.data.archive} />
        </div>
      )}
    </section>
  );
}

function DatabaseResult({ result }: { result: HubDatabaseCheck }) {
  const { t } = useTranslation();

  return (
    <div className="flex flex-col gap-2" data-testid="connection-database">
      <SectionHeader title={t('connections.database.title')} />
      <Outcome outcome={result.outcome} milliseconds={result.milliseconds} />
      {result.error === null ? null : <Notice tone="error" title={t(`connections.errors.${result.error}`)} />}
      {result.serverVersion === null ? null : (
        <Subtle>{t('connections.serverVersion', { version: result.serverVersion })}</Subtle>
      )}
      {result.migrations.length === 0 ? null : (
        <ul className="flex flex-col gap-1">
          {result.migrations.map((line) => (
            <li key={line.context} className="flex flex-wrap items-center gap-2">
              <span className="font-mono text-sm">{line.context}</span>
              {line.pending.length === 0 ? (
                <Badge variant="flat" text={t('connections.migrations.upToDate')} />
              ) : (
                <Badge
                  variant="filled"
                  text={t('connections.migrations.pending', {
                    count: line.pending.length,
                    names: line.pending.join(', '),
                  })}
                />
              )}
              {line.latest === null ? null : (
                <Subtle className="font-mono text-sm">
                  {t('connections.migrations.latest', { name: line.latest })}
                </Subtle>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function ArchiveResult({ result }: { result: ArchiveCheck }) {
  const { t } = useTranslation();
  const moment = useMoment();

  return (
    <div className="flex flex-col gap-2" data-testid="connection-archive">
      <SectionHeader title={t('connections.archive.title')} />
      <Outcome outcome={result.outcome} milliseconds={result.milliseconds} />
      {result.outcome === 'NotConfigured' ? <Subtle>{t('connections.archive.notConfigured')}</Subtle> : null}
      {result.error === null ? null : <Notice tone="error" title={t(`connections.errors.${result.error}`)} />}
      {result.outcome !== 'Ok' ? null : (
        <Subtle>
          {result.latestSessionStart === null
            ? t('connections.archive.empty')
            : t('connections.archive.latest', { at: moment(result.latestSessionStart) })}
        </Subtle>
      )}
    </div>
  );
}

/** Reached or not, and how long it took when something was opened. */
function Outcome({
  outcome,
  milliseconds,
}: {
  outcome: HubDatabaseCheck['outcome'];
  milliseconds: number | null;
}) {
  const { t } = useTranslation();

  return (
    <div className="flex items-center gap-2">
      <Badge variant={outcome === 'Ok' ? 'filled' : 'flat'} text={t(`connections.outcomes.${outcome}`)} />
      {milliseconds === null ? null : <Subtle>{t('connections.time', { ms: milliseconds })}</Subtle>}
    </div>
  );
}
