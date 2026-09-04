import { Button, Input, Label, Select } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { LayoutTemplate } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import type { Department } from '../../shared/api/bootstrap';
import { ProblemAlert } from '../../shared/forms';
import { useLocalized } from '../../shared/i18n/useLocalized';

import { useCreateFromTemplate } from './mutations';
import { templatesQuery } from './queries';

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
  onCreated,
}: {
  department: Department;
  onCreated: (id: number) => void;
}) {
  const { t } = useTranslation();
  const read = useLocalized();

  const [templateId, setTemplateId] = useState<string>('');
  const [slug, setSlug] = useState('');

  const templates = useQuery(templatesQuery());
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
      { templateId: Number(templateId), ownerDepartment: department, slug: slug.trim() },
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

        <div className="flex min-w-56 flex-col gap-1">
          <Label htmlFor="newSlug">{t('content.fields.slug')}</Label>
          <Input
            id="newSlug"
            value={slug}
            onChange={(event) => setSlug(event.target.value)}
            placeholder="my-new-page"
          />
        </div>

        <Button
          type="button"
          disabled={templateId === '' || slug.trim() === '' || create.isPending}
          isLoading={create.isPending}
          onClick={submit}
        >
          {t('content.create')}
        </Button>
      </div>
    </div>
  );
}
