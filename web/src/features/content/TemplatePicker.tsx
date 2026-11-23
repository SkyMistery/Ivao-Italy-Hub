import { Button, Input, Label, Select } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { LayoutTemplate } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import type { Department } from '../../shared/api/bootstrap';
import {
  NO_CHOICE,
  ProblemAlert,
  cannotProposeASlug,
  choiceItem,
  describeProblem,
  slugify,
} from '../../shared/forms';
import { useLocalized } from '../../shared/i18n/useLocalized';
import { bootstrapQuery } from '../me/queries';

import { useCreateFromTemplate } from './mutations';
import { parentChoices } from './pageTree';
import { pageTreeQuery, templatesQuery, type ContentKind } from './queries';
import { CONTENT_SLUG_MAX_LENGTH } from './schema';

/**
 * "New from template". The copy is made by the server — new identifiers for every section and
 * block, the keys only a template may carry left behind — so this is a template, a title and a
 * button, and no client side copying at all (design M0 §5.6).
 *
 * The address is born from the title, as in the form of a row (design 13 §4.1; the maintainer's answer on #554, point 3):
 * the title of the new row is asked here, in the division's main language, and its slug comes from it. The field of the
 * slug is drawn to whoever may approve, who writes it as elsewhere, and to anybody whose title gives no slug (point 1).
 * An approver may leave the title empty: the row then keeps the template's.
 *
 * A division with no templates seeded sees nothing here rather than an empty select: there is
 * nothing to choose, and saying so with an empty control would only invite a click.
 */
export function TemplatePicker({
  department,
  kind,
  mayBeAtTheTop,
  maySetSlug,
  onCreated,
}: {
  department: Department;
  /** Which list this picker sits on: a news list offers the templates of a news item, and no other. */
  kind: ContentKind;
  /**
   * Whether the page may be left at the top of the site (note 2026-09-13-contenuti-centralizzati,
   * 3.7). When it may not, the page it makes has to be put under one, and the button waits for it.
   */
  mayBeAtTheTop: boolean;
  /** Whether this member writes the slug: whoever holds `Content.Approve` (design 13 §4.1). */
  maySetSlug: boolean;
  onCreated: (id: number) => void;
}) {
  const { t, i18n } = useTranslation();
  const read = useLocalized();

  const [templateId, setTemplateId] = useState<string>('');
  const [title, setTitle] = useState('');
  // What was typed in the field of the slug; null while it follows the title, as `slugFrom` does in a form.
  const [typedSlug, setTypedSlug] = useState<string | null>(null);
  const [parentId, setParentId] = useState<string>('');

  const templates = useQuery(templatesQuery(kind));
  // Where the new page goes, for a page; a news item and a document have an address of their kind.
  const isPage = kind === 'Page';
  const tree = useQuery({ ...pageTreeQuery(), enabled: isPage });
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const parents = parentChoices(
    tree.data ?? [],
    bootstrap?.registries.screens ?? [],
    read,
    (key) => t(key),
    t('content.tree.noPagesUnder'),
  );
  const create = useCreateFromTemplate();

  const defaultLocale = bootstrap?.division.defaultLocale;
  const slug = (typedSlug ?? slugify(title, CONTENT_SLUG_MAX_LENGTH)).trim();

  // Opened by a title that gives no slug, and open from then on, as in the editor.
  const [slugOpened, setSlugOpened] = useState(false);
  if (!slugOpened && cannotProposeASlug(title, defaultLocale)) {
    setSlugOpened(true);
  }
  const slugDrawn = maySetSlug || slugOpened;

  const items = (templates.data?.items ?? []).map((template) => ({
    value: String(template.id),
    label: read(template.title) || template.slug,
  }));

  if (items.length === 0) {
    return null;
  }

  const submit = () => {
    create.mutate(
      {
        templateId: Number(templateId),
        ownerDepartment: department,
        slug,
        parentId: isPage && parentId !== '' ? Number(parentId) : null,
        // In the division's main language, the one the address is published in; the others are written in the editor.
        title: title.trim() === '' || defaultLocale === undefined ? null : { [defaultLocale]: title.trim() },
      },
      { onSuccess: (content) => onCreated(content.id) },
    );
  };

  return (
    <div className="border-border flex flex-col gap-3 rounded-lg border p-4">
      <div className="flex items-center gap-2">
        <LayoutTemplate aria-hidden className="size-4" />
        <span className="font-medium">{t('content.newFromTemplate')}</span>
      </div>

      {/* The server's reason — an address already taken, one the site keeps for itself — and the generic sentence only when
          it gave none (issue #332, item 12). */}
      <ProblemAlert
        summary={
          create.isError
            ? (describeProblem(create.error, t, i18n.language) ?? t('content.templateRefused'))
            : null
        }
      />

      <div className="flex flex-wrap items-end gap-3">
        <div className="flex min-w-56 flex-col gap-1">
          <Label htmlFor="templateId">{t('content.fields.template')}</Label>
          <Select
            id="templateId"
            {...(templateId === '' ? {} : { value: templateId })}
            onValueChange={setTemplateId}
            placeholder={t('content.chooseTemplate')}
            items={items}
          />
        </div>

        {isPage ? (
          <div className="flex min-w-56 flex-col gap-1">
            <Label htmlFor="newParent">{t('content.fields.parentId')}</Label>
            <Select
              id="newParent"
              {...(parentId === '' ? {} : { value: parentId })}
              onValueChange={(chosen) => setParentId(chosen === NO_CHOICE ? '' : chosen)}
              placeholder={mayBeAtTheTop ? t('content.options.parentId.none') : t('content.chooseParent')}
              items={[
                ...(mayBeAtTheTop ? [{ value: NO_CHOICE, label: t('content.options.parentId.none') }] : []),
                ...parents.map(choiceItem),
              ]}
            />
          </div>
        ) : null}

        <div className="flex min-w-56 flex-col gap-1">
          <Label htmlFor="newTitle">{t('content.fields.title')}</Label>
          <Input id="newTitle" value={title} onChange={(event) => setTitle(event.target.value)} />
        </div>

        {slugDrawn ? (
          <div className="flex min-w-56 flex-col gap-1">
            <Label htmlFor="newSlug">{t('content.fields.slug')}</Label>
            <Input
              id="newSlug"
              value={typedSlug ?? slug}
              onChange={(event) => setTypedSlug(event.target.value)}
              placeholder={t('content.slugPlaceholder')}
            />
          </div>
        ) : null}

        <Button
          type="button"
          disabled={
            templateId === '' ||
            slug === '' ||
            (isPage && !mayBeAtTheTop && parentId === '') ||
            create.isPending
          }
          isLoading={create.isPending}
          onClick={submit}
        >
          {t('content.create')}
        </Button>
      </div>
    </div>
  );
}
