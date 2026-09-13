import { useQuery } from '@tanstack/react-query';
import { createFileRoute, redirect, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { contentEditorSearchSchema } from '../../features/content/contentSearch';
import { ContentFormScreen } from '../../features/content/ContentFormScreen';
import { CONTENT_KINDS } from '../../features/content/kinds';
import { contentQuery, madeFromTemplateQuery, type ContentDetailDto } from '../../features/content/queries';
import { MANAGE_TEMPLATES } from '../../features/content/templateRules';
import { writableDepartments } from '../../shared/api/bootstrap';

/** The permission a new row of an ordinary kind is written with. */
const CONTENT_EDIT = 'Content.Edit';

/**
 * One row of content in the editor — a page, a news item, a document or a template — and `new` for
 * one that does not exist yet. The screen is shared by all of them; what this file owns is the
 * address, which is the one thing that cannot be configuration (design M1 §3.2).
 *
 * An existing row says what it is and whose it is. A new one is told by the address: which kind,
 * whether it is a template, and in which department — chosen on the list before coming here, and
 * held to the departments this person may write in (note 2026-09-13-contenuti-centralizzati).
 */
export const Route = createFileRoute('/_staff/staff/content/$id')({
  validateSearch: contentEditorSearchSchema,
  beforeLoad: ({ context, params, search }) => {
    if (params.id !== 'new') {
      return;
    }

    const writable = writableDepartments(
      context.bootstrap,
      search.template === true ? MANAGE_TEMPLATES : CONTENT_EDIT,
    );
    if (search.department === undefined ? writable.length === 0 : !writable.includes(search.department)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  loader: async ({ context, params }): Promise<ContentDetailDto | null> =>
    params.id === 'new' ? null : context.queryClient.ensureQueryData(contentQuery(Number(params.id))),
  component: ContentForm,
});

function ContentForm() {
  const { t } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { id } = Route.useParams();
  const search = Route.useSearch();
  const navigate = useNavigate();

  const isNew = id === 'new';

  // The row as it stands now. ⚠️ Not `Route.useLoaderData()`: a loader runs on navigation and
  // never again, so after one save the screen still held the `rowVersion` from when the page
  // opened, and the second save was answered 409 — blaming somebody who does not exist. The loader
  // above is the *preload*; what the screen reads is the query it filled (design M0 §7.3).
  const row = useQuery({ ...contentQuery(Number(id)), enabled: !isNew }).data ?? null;

  const isTemplate = row?.isTemplate ?? search.template ?? false;

  // How many rows were made from a template. Asked only of a template that exists, and read as the
  // total of a page of one — no endpoint of its own, and no column on the list either.
  const made = useQuery({ ...madeFromTemplateQuery(Number(id)), enabled: !isNew && isTemplate });
  const count = made.data?.total;

  if (!isNew && row === null) {
    // The loader has already put it in the cache, so this is the compiler asking rather than a
    // state a reader reaches.
    return null;
  }

  const department =
    row?.ownerDepartment ??
    search.department ??
    writableDepartments(bootstrap, isTemplate ? MANAGE_TEMPLATES : CONTENT_EDIT)[0];

  if (department === undefined) {
    return null;
  }

  const kind = row?.kind ?? search.kind ?? 'Page';
  const config = isTemplate ? { ...CONTENT_KINDS[kind], titles: 'templates' } : CONTENT_KINDS[kind];

  // Where the list this row belongs to is: its kind, in its department. A department's home is not
  // on that list — it has its own address under the department — so it goes back to the pages.
  const listSearch = {
    kind: isTemplate ? ('Template' as const) : kind === 'Dashboard' ? ('Page' as const) : kind,
    department,
  };

  return (
    <ContentFormScreen
      config={config}
      bootstrap={bootstrap}
      department={department}
      id={id}
      content={row}
      startsAsTemplate={isTemplate}
      {...(count === undefined ? {} : { note: t('templates.madeFromIt', { count }) })}
      breadcrumbTo={`/staff/content?kind=${listSearch.kind}&department=${department}`}
      onCreated={async (created) => {
        await navigate({ to: '/staff/content/$id', params: { id: String(created) } });
      }}
      onFinished={() => void navigate({ to: '/staff/content', search: listSearch })}
    />
  );
}
