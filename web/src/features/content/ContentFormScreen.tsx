import { useQuery, useQueryClient } from '@tanstack/react-query';
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
import { publishProblemsKey, publishProblemsQuery, type ContentDetailDto } from './queries';
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
  note,
  startsAsTemplate = false,
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
  /**
   * One line under the title, when the screen has something to say about this row. The templates
   * screen uses it to say how many rows were made from this one, which is the sentence that stops a
   * careless edit — a template with eleven pages behind it is not one to reorganise casually.
   */
  note?: string;
  /** Passed through: a row created here is a template. See `ContentEditor.startsAsTemplate`. */
  startsAsTemplate?: boolean;
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

  const queryClient = useQueryClient();

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

  // What still stands between this row and the public, answered by the server running the very same
  // checks publication runs. There is nothing to ask about a row that does not exist yet.
  const problems = useQuery({ ...publishProblemsQuery(Number(id)), enabled: !isNew });

  // Asked again after anything that could have changed the answer. Not awaited: the screen has
  // already been told what happened, and a list that arrives a moment later is a list arriving.
  const askAgain = () => {
    if (!isNew) {
      void queryClient.invalidateQueries({ queryKey: publishProblemsKey(Number(id)) });
    }
  };

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
      {...(note === undefined ? {} : { note })}
      breadcrumb={[
        { label: department },
        { label: t(`${config.titles}.title`), to: breadcrumbTo },
        { label: title },
      ]}
    >
      <ContentEditor
        content={content}
        kind={config.kind}
        startsAsTemplate={startsAsTemplate}
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
        publishProblems={problems.data}
        onSave={async (values: ContentFormValues, body) => {
          if (isNew) {
            const created = await create.mutateAsync({ values, body });
            await onCreated(created.id);
            notice({ tone: 'success', title: t('content.editor.saved') });
            return created;
          }

          const saved = await update.mutateAsync({ values, body });
          notice({ tone: 'success', title: t('content.editor.saved') });
          askAgain();
          return saved;
        }}
        onPublish={
          isNew
            ? null
            : () =>
                publish.mutate(null, {
                  onSuccess: () => {
                    notice({ tone: 'success', title: t('content.editor.published') });
                    askAgain();
                  },
                  // The reason stays in `PublishProblems`, which names the block and the language.
                  // This only says that the click was answered, and answered no: the list of
                  // reasons is above the form and may well be off the screen.
                  onError: () => {
                    notice({ tone: 'error', title: t('content.editor.publishRefused') });
                    // The reason is the list above the form, and it is the same list: ask for it
                    // again rather than reading the refusal, so there is one answer and not two.
                    askAgain();
                  },
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
