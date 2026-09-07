import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { holdsPermission, type Bootstrap, type Department } from '../../shared/api/bootstrap';
import type { ChoiceOption } from '../../shared/forms';
import { useLocalized } from '../../shared/i18n/useLocalized';
import { PageShell, useNotice } from '../../shared/ui';
import { categoriesOfKindQuery } from '../categories/queries';
import { mediaPickerQuery } from '../media/queries';

import { ContentEditor } from './ContentEditor';
import type { ContentKindConfig } from './kinds';
import { useCreateContent, useDeleteContent, usePublishContent, useUpdateContent } from './mutations';
import type { ContentDetailDto } from './queries';
import type { ContentFormValues } from './schema';
import { MANAGE_TEMPLATES } from './templateRules';

/**
 * One row of content in the editor, whichever kind it is. `new` is a row that does not exist yet:
 * it is created by the first save, which is also when it gets an address of its own — until then
 * there is nothing to publish and nothing to delete, and the editor says so by being handed nothing
 * to call.
 *
 * Pages, news and documents are this screen three times over. What the kind decides is which three
 * of the five kind-specific fields the form draws, and that is decided by the schema and not by a
 * branch here (design M1 §3.2).
 *
 * The navigation is handed in for the same reason it is in the list screen: a route path is a
 * literal the router checks at build time and belongs in the file that owns the route.
 */
export function ContentFormScreen({
  config,
  bootstrap,
  department,
  id,
  content,
  breadcrumbTo,
  onCreated,
  onFinished,
}: {
  config: ContentKindConfig;
  bootstrap: Bootstrap;
  department: Department;
  /** The id in the address: a number as text, or `new`. */
  id: string;
  content: ContentDetailDto | null;
  /** Where the breadcrumb goes back to; the route knows the address, this screen does not. */
  breadcrumbTo: string;
  onCreated: (id: number) => Promise<void>;
  onFinished: () => void;
}) {
  const { t } = useTranslation();
  const read = useLocalized();

  // What tells the editor that the click did something. Asked for by Carmine after the demo: a
  // save that worked said nothing, and neither did an action that went nowhere — which is how a
  // section was lost while copying a page across by hand.
  const notice = useNotice();

  const isNew = id === 'new';
  const locales = bootstrap.division.locales;

  const create = useCreateContent();
  const update = useUpdateContent(Number(id));
  const remove = useDeleteContent();
  const publish = usePublishContent(Number(id));

  // The shelves this department has for this kind. A page has none, so nothing is asked for one:
  // the select only exists on the kinds whose schema declares it.
  const vocabulary = useQuery({
    ...categoriesOfKindQuery(department, config.kind),
    enabled: config.kind !== 'Page',
  });

  const categories: ChoiceOption[] = (vocabulary.data?.items ?? []).map((category) => ({
    value: category.key,
    // Resolved here, in the language on screen: the generator draws the label it is handed and
    // never translates a value of its own (`shared/forms/schema.ts`).
    label: read(category.label) || category.key,
  }));

  const title = isNew ? t(`${config.titles}.create`) : t(`${config.titles}.edit`);

  return (
    <PageShell
      title={title}
      breadcrumb={[
        { label: department },
        { label: t(`${config.titles}.title`), to: breadcrumbTo },
        { label: title },
      ]}
    >
      <ContentEditor
        content={content}
        kind={config.kind}
        categories={categories}
        department={department}
        locales={locales}
        division={{
          defaultLocale: bootstrap.division.defaultLocale,
          timezone: bootstrap.division.timezone,
        }}
        // The library of this department: a row picks its pictures out of its own files.
        mediaLibrary={mediaPickerQuery(department)}
        // Asked of the template's department and not of this page's: a page of one department can
        // be made from the template of another (design M1 §9.4).
        canManageTemplates={(owner) => holdsPermission(bootstrap, MANAGE_TEMPLATES, owner)}
        busy={create.isPending || update.isPending || publish.isPending || remove.isPending}
        publishError={publish.error}
        onSave={async (values: ContentFormValues, body) => {
          if (isNew) {
            const created = await create.mutateAsync({ values, body });
            await onCreated(created.id);
            notice({ tone: 'success', title: t('content.editor.saved') });
            return created;
          }

          const saved = await update.mutateAsync({ values, body });
          notice({ tone: 'success', title: t('content.editor.saved') });
          return saved;
        }}
        onPublish={
          isNew
            ? null
            : () =>
                publish.mutate(null, {
                  onSuccess: () => notice({ tone: 'success', title: t('content.editor.published') }),
                  // The reason stays in `PublishProblems`, which names the block and the language.
                  // This only says that the click was answered, and answered no: the list of
                  // reasons is above the form and may well be off the screen.
                  onError: () => notice({ tone: 'error', title: t('content.editor.publishRefused') }),
                })
        }
        onDelete={
          isNew
            ? null
            : () =>
                remove.mutate(Number(id), {
                  onSuccess: () => {
                    notice({ tone: 'success', title: t('content.editor.deleted') });
                    onFinished();
                  },
                })
        }
      />
    </PageShell>
  );
}
