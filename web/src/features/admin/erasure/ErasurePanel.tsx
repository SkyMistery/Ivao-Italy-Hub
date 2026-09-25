import { Badge, Subtle } from '@ivao/atmosphere-react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { SchemaForm } from '../../../shared/forms';
import { ConfirmDialog, Notice, SectionHeader } from '../../../shared/ui';

import { erasurePreviewQuery, useErase, type ErasureLine } from './queries';

/**
 * Erasing a person's data (T20b, note `2026-09-25-la-cancellazione-dei-dati-di-una-persona`): a VID, what would go and what
 * would stay without the name, and one confirmation. Beside the list of super administrators, because it is the other
 * gesture only they have; the server answers 403 to anybody else, so this is convenience and not security.
 *
 * The lines are words the server names: the core's (`erasure.lines.*`) and each module's, in the module's namespace.
 */
const lookupSchema = z.object({ vid: z.number().int().positive() });

type LookupValues = z.output<typeof lookupSchema>;

export function ErasurePanel({ locales }: { locales: readonly string[] }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [vid, setVid] = useState<number | undefined>(undefined);
  const preview = useQuery({ ...erasurePreviewQuery(vid ?? 0), enabled: vid !== undefined });
  const erase = useErase();

  return (
    <section className="flex flex-col gap-4">
      <SectionHeader title={t('erasure.title')} description={t('erasure.description')} />

      <SchemaForm
        schema={lookupSchema}
        defaults={{ vid: 0 }}
        locales={locales}
        labels="erasure"
        onSubmit={async (values: LookupValues) => {
          // Fetched here rather than left to the query below, so a refusal lands on the field like any other.
          await queryClient.fetchQuery(erasurePreviewQuery(values.vid));
          erase.reset();
          setVid(values.vid);
        }}
        submitLabel={t('erasure.lookup')}
      />

      {erase.data === undefined ? null : (
        <Notice
          tone="success"
          title={t('erasure.done', { pseudonym: erase.data.pseudonym })}
          description={<Lines lines={erase.data.lines} />}
        />
      )}

      {vid === undefined || preview.data === undefined || erase.data !== undefined ? null : (
        <div className="flex flex-col gap-3">
          <Subtle>
            {preview.data.name === null || preview.data.name === undefined
              ? t('erasure.neverSignedIn', { vid })
              : t('erasure.person', { name: preview.data.name, vid })}
          </Subtle>
          <Lines lines={preview.data.lines} />
          {preview.data.isSuperadmin ? (
            <Notice tone="warning" title={t('erasure.superadmin')} />
          ) : (
            <div>
              <ConfirmDialog
                triggerText={t('erasure.confirm.trigger')}
                triggerVariant="secondary"
                title={t('erasure.confirm.title', { vid })}
                description={t('erasure.confirm.description')}
                confirmText={t('erasure.confirm.trigger')}
                confirmVariant="destructive"
                disabled={erase.isPending}
                onConfirm={() => erase.mutate(vid)}
              />
            </div>
          )}
        </div>
      )}
    </section>
  );
}

/** The lines with something in them: a count, what they are, and what becomes of them. */
function Lines({ lines }: { lines: readonly ErasureLine[] }) {
  const { t } = useTranslation();

  return (
    <ul className="flex flex-col gap-1">
      {lines
        .filter((line) => line.count > 0)
        .map((line) => (
          <li key={line.key} className="flex items-center gap-2">
            <span className="min-w-8 text-right tabular-nums">{line.count}</span>
            <span className="grow">{t(line.key)}</span>
            <Badge
              variant={line.outcome === 'Deleted' ? 'filled' : 'flat'}
              text={t(`erasure.outcomes.${line.outcome}`)}
            />
          </li>
        ))}
    </ul>
  );
}
