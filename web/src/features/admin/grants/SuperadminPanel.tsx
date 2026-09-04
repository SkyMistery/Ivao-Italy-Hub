import { Badge, H3, Subtle } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { SchemaForm } from '../../../shared/forms';
import { ConfirmDialog, SectionHeader } from '../../../shared/ui';

import { useAddSuperadmin, useRemoveSuperadmin } from './mutations';
import { superadminsQuery } from './queries';

/**
 * Who may bypass every policy, and the two gestures that change that list.
 *
 * Only a super administrator ever sees it: the permission catalogue has nothing above
 * `Permissions.Manage` and deliberately does not, because a permission able to hand out the bypass
 * would make the bypass ordinary. The server answers 403 to anybody else, so this is convenience
 * and not security.
 *
 * The one field is still a generated form. A single input is exactly where somebody would write
 * "just this once" and hand roll one, and then the next screen has two of them.
 */
const addSchema = z.object({ vid: z.number().int() });

type AddValues = z.output<typeof addSchema>;

export function SuperadminPanel({ locales }: { locales: readonly string[] }) {
  const { t } = useTranslation();
  const { data: vids, isPending } = useQuery(superadminsQuery());

  const add = useAddSuperadmin();
  const remove = useRemoveSuperadmin();

  return (
    <section className="flex flex-col gap-4">
      <SectionHeader title={t('superadmins.title')} description={t('superadmins.description')} />

      {isPending ? (
        <Subtle>{t('common.loading')}</Subtle>
      ) : (
        <ul className="flex flex-wrap items-center gap-2">
          {(vids ?? []).map((vid) => (
            <li key={vid} className="flex items-center gap-1">
              <Badge variant="leaked" text={String(vid)} />
              <ConfirmDialog
                triggerText={t('common.delete')}
                title={t('superadmins.remove.title')}
                description={t('superadmins.remove.description', { vid })}
                confirmText={t('common.delete')}
                disabled={remove.isPending}
                onConfirm={() => remove.mutate(vid)}
              />
            </li>
          ))}
        </ul>
      )}

      <H3>{t('superadmins.add')}</H3>
      <SchemaForm
        schema={addSchema}
        defaults={{ vid: 0 }}
        locales={locales}
        labels="superadmins"
        onSubmit={async (values: AddValues) => {
          await add.mutateAsync(values.vid);
        }}
        submitLabel={t('superadmins.add')}
      />
    </section>
  );
}
