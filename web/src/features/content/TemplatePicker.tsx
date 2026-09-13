import { Button, Input, Label, Select } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { LayoutTemplate } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import type { Department } from '../../shared/api/bootstrap';
import { NO_CHOICE, ProblemAlert } from '../../shared/forms';
import { useLocalized } from '../../shared/i18n/useLocalized';

import { useCreateFromTemplate } from './mutations';
import { pageTreeQuery, templatesQuery, type ContentKind } from './queries';

/**
 * "New from template". The copy is made by the server — new identifiers for every section and
 * block, the keys only a template may carry left behind — so this is a template, a slug and a
 * button, and no client side copying at all (design M0 §5.6).
 *
 * A division with no templates seeded sees nothing here rather than an empty select: there is
 * nothing to choose, and saying so with an empty control would only invite a click.
 */
export function TemplatePicker({
  department,
  kind,
  mayBeAtTheTop,
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
  onCreated: (id: number) => void;
}) {
  const { t } = useTranslation();
  const read = useLocalized();

  const [templateId, setTemplateId] = useState<string>('');
  const [slug, setSlug] = useState('');
  const [parentId, setParentId] = useState<string>('');

  const templates = useQuery(templatesQuery(kind));
  // Where the new page goes, for a page; a news item and a document have an address of their kind.
  const isPage = kind === 'Page';
  const tree = useQuery({ ...pageTreeQuery(), enabled: isPage });
  const parents = (tree.data ?? [])
    .filter((node) => node.depth < 3)
    .map((node) => ({ value: String(node.id), label: `/${node.path} — ${read(node.title) || node.path}` }));
  const create = useCreateFromTemplate();

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
        slug: slug.trim(),
        parentId: isPage && parentId !== '' ? Number(parentId) : null,
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

      <ProblemAlert summary={create.isError ? t('content.templateRefused') : null} />

      <div className="flex flex-wrap items-end gap-3">
        <div className="flex min-w-56 flex-col gap-1">
          <Label htmlFor="templateId">{t('content.fields.template')}</Label>
          <Select
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
                ...parents,
              ]}
            />
          </div>
        ) : null}

        <div className="flex min-w-56 flex-col gap-1">
          <Label htmlFor="newSlug">{t('content.fields.slug')}</Label>
          <Input
            id="newSlug"
            value={slug}
            onChange={(event) => setSlug(event.target.value)}
            placeholder={t('content.slugPlaceholder')}
          />
        </div>

        <Button
          type="button"
          disabled={
            templateId === '' ||
            slug.trim() === '' ||
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
