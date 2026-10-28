import { Button } from '@ivao/atmosphere-react';
import { Plus } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import type { Department } from '../../shared/api/bootstrap';
import { SchemaForm } from '../../shared/forms';
import { Dialog } from '../../shared/ui';

import { emptyCategory, useCreateCategory } from './mutations';
import type { ContentKind } from './queries';
import { newCollectionSchema } from './schema';

/**
 * "New collection" from the page of a news item or a document (#364): a collection is made where it
 * is needed, and the row being written is filed in it at once, rather than on a screen of its own
 * somebody has to know about. The same row and the same call as `/staff/<dept>/collections`, in a
 * dialog; only its name and its key are asked, since the kind is the page's and the rest has a
 * default.
 *
 * Mounted by the form generator under a field annotated `creatable` (`SchemaForm`, `createChoice`):
 * `onCreated` is what ticks the new key there.
 */
export function NewCollection({
  kind,
  department,
  locales,
  division,
  onCreated,
}: {
  kind: Extract<ContentKind, 'News' | 'Document'>;
  department: Department;
  locales: readonly string[];
  /** The division's default language, which the key is proposed from, and its time zone. */
  division: { defaultLocale: string; timezone: string };
  onCreated: (key: string) => void;
}) {
  const { t } = useTranslation();
  const create = useCreateCategory();
  const [open, setOpen] = useState(false);

  return (
    <Dialog
      open={open}
      onOpenChange={setOpen}
      trigger={
        <Button type="button" variant="ghost" size="sm" className="self-start">
          <Plus aria-hidden className="mr-2 size-4" />
          {t('categories.newHere.action')}
        </Button>
      }
      title={t('categories.create')}
      description={t('categories.newHere.description', { department: t(`departments.${department}`) })}
    >
      {/* Mounted only while open, so that every opening starts from an empty form. */}
      {open ? (
        <SchemaForm
          schema={newCollectionSchema}
          defaults={{ ...emptyCategory(department, locales), kind }}
          locales={locales}
          labels="categories"
          division={division}
          submitLabel={t('categories.newHere.submit')}
          onSubmit={async (values) => {
            const created = await create.mutateAsync(values);
            onCreated(created.key);
            setOpen(false);
          }}
        />
      ) : null}
    </Dialog>
  );
}
