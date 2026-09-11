import { useQuery } from '@tanstack/react-query';
import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { ContentFormScreen } from '../../features/content/ContentFormScreen';
import { CONTENT_KINDS } from '../../features/content/kinds';
import { contentQuery, madeFromTemplateQuery, type ContentDetailDto } from '../../features/content/queries';
import { deptParam } from '../../shared/api/department';

/**
 * One template, in the same editor a page is written in — because a template **is** a row of
 * `cms_contents`, and a second editor for it would be the thing plan §9.3 exists to prevent.
 *
 * What this file adds is the two things the editor cannot know: that a row created here is a
 * template, and how many rows were made from this one.
 */
export const Route = createFileRoute('/_staff/staff/$dept/templates/$id')({
  // Which kind the new template is for, chosen on the list before coming here. Only `new` reads it:
  // an existing row says what it is. `Page` is the fallback, so a hand typed address still works.
  validateSearch: z.object({
    kind: z.enum(['Page', 'News', 'Document']).default('Page'),
  }),
  loader: async ({ context, params }): Promise<ContentDetailDto | null> =>
    params.id === 'new' ? null : context.queryClient.ensureQueryData(contentQuery(Number(params.id))),
  component: TemplateForm,
});

function TemplateForm() {
  const { t } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { dept, id } = Route.useParams();
  const { kind } = Route.useSearch();
  const navigate = useNavigate();

  const isNew = id === 'new';

  // ⚠️ The query and not `Route.useLoaderData()`, for the reason every other detail screen says: a
  // loader runs on navigation and never again, so after one save the screen would still hold the
  // `rowVersion` it opened with and the second save would be answered 409.
  const row = useQuery({ ...contentQuery(Number(id)), enabled: !isNew }).data ?? null;

  // How many rows were made from this one. Asked only of a template that exists, and read as the
  // total of a page of one — no endpoint of its own, and no column on the list either, because a
  // column would be one request per row.
  const made = useQuery({ ...madeFromTemplateQuery(Number(id)), enabled: !isNew });
  const count = made.data?.total;

  return (
    <ContentFormScreen
      // The kind decides which fields the form draws; the wording of the screen comes from the
      // templates namespace, because this is not the page list under another name.
      config={{ ...CONTENT_KINDS[row?.kind ?? kind], titles: 'templates' }}
      bootstrap={bootstrap}
      department={dept}
      id={id}
      content={row}
      startsAsTemplate
      {...(count === undefined ? {} : { note: t('templates.madeFromIt', { count }) })}
      breadcrumbTo={`/staff/${deptParam.format(dept)}/templates`}
      onCreated={async (created) => {
        await navigate({ to: '/staff/$dept/templates/$id', params: { dept, id: String(created) } });
      }}
      onFinished={() => void navigate({ to: '/staff/$dept/templates', params: { dept } })}
    />
  );
}
