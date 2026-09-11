import { Button, Select } from '@ivao/atmosphere-react';
import { Link, createFileRoute } from '@tanstack/react-router';
import { Plus } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { TEMPLATE_COLUMNS } from '../../features/content/kinds';
import { templateListQuery } from '../../features/content/queries';
import { DataList, listSearchSchema } from '../../shared/list';
import { PageShell } from '../../shared/ui';

/**
 * The templates of one department. Until now they had no screen at all: they were kept out of the
 * content list on purpose, offered by the picker only to make a page from, and the only way to open
 * one was to type its address. So a coordinator could be told, in design M1 §9.4, that templates
 * were theirs to manage, and find nowhere to manage them.
 *
 * Recipe 2 (design M0 §7.3), like every other list. What is different is one column — the kind —
 * and one control, because a new template has to be told which kind it is for before it exists.
 */
export const Route = createFileRoute('/_staff/staff/$dept/templates/')({
  validateSearch: listSearchSchema,
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps, params }) =>
    context.queryClient.ensureQueryData(templateListQuery(params.dept, deps)),
  component: TemplatesPage,
});

/**
 * The kinds a template can be for. Not `Dashboard`: a department has exactly one home and it is
 * seeded from the template the installation was born with, so a second one would be a template
 * nothing could be made from (design M1 §14).
 */
const KINDS = ['Page', 'News', 'Document'] as const;

/** The three of the four, as a type: the search parameter of the editor accepts exactly these. */
type TemplateKind = (typeof KINDS)[number];

function TemplatesPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { dept } = Route.useParams();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  // Which kind the next one is for. It sits beside the button rather than inside the editor because
  // the kind decides which fields the form draws, and a form that redrew itself under the hands of
  // somebody filling it in would be worse than choosing first.
  const [kind, setKind] = useState<TemplateKind>('Page');

  const division = bootstrap.division;

  const create = (
    <div className="flex items-end gap-2">
      {/* ⚠️ A real label and not an `aria-label`: Atmosphere's `Select` drops the attribute, so a
          control labelled that way has no accessible name at all (`docs/UI-GUIDELINES.md`). Invisible
          because the button beside it already says what this chooses. */}
      <label htmlFor="templateKind" className="sr-only">
        {t('content.fields.kind')}
      </label>
      <Select
        id="templateKind"
        value={kind}
        onValueChange={(value) => setKind(value as TemplateKind)}
        items={KINDS.map((value) => ({ value, label: t(`content.options.kind.${value}`) }))}
      />
      <Button asChild>
        <Link to="/staff/$dept/templates/$id" params={{ dept, id: 'new' }} search={{ kind }}>
          <Plus aria-hidden className="mr-2 size-4" />
          {t('templates.create')}
        </Link>
      </Button>
    </div>
  );

  return (
    <PageShell
      title={t('templates.title')}
      description={t('templates.description')}
      breadcrumb={[{ label: dept }, { label: t('templates.title') }]}
      actions={create}
    >
      <DataList
        columns={TEMPLATE_COLUMNS}
        query={templateListQuery(dept, search)}
        labels="content"
        locale={i18n.language}
        defaultLocale={division.defaultLocale}
        timezone={division.timezone}
        search={search}
        onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
        actions={(row) => (
          <Button asChild variant="ghost" size="sm">
            <Link to="/staff/$dept/templates/$id" params={{ dept, id: String(row.id) }}>
              {t('common.edit')}
            </Link>
          </Button>
        )}
        emptyAction={create}
      />
    </PageShell>
  );
}
