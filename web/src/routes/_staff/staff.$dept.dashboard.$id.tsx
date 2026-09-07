import { useQuery } from '@tanstack/react-query';
import { createFileRoute, redirect, useNavigate } from '@tanstack/react-router';

import { ContentFormScreen } from '../../features/content/ContentFormScreen';
import { CONTENT_KINDS } from '../../features/content/kinds';
import { contentQuery, type ContentDetailDto } from '../../features/content/queries';
import { reachableDepartments } from '../../shared/api/bootstrap';
import { deptParam } from '../../shared/api/department';

/**
 * The dashboard of a department, in the editor. The screen is the one pages, news and documents
 * use — same editor, same renderer, same publication — and what this file owns is the address.
 *
 * There is no list beside it, and that is the difference from the other three kinds: a department
 * has exactly one dashboard, so it is reached from `/staff/<dept>` rather than from a list of one
 * row. ⚠️ It is a route of its own rather than a detour through `/staff/<dept>/content/<id>`,
 * because that screen carries `kind = Page` in its payload: opening a dashboard there and saving
 * would quietly turn it into a page.
 */
const CONFIG = CONTENT_KINDS.Dashboard;

export const Route = createFileRoute('/_staff/staff/$dept/dashboard/$id')({
  params: {
    parse: ({ dept, id }) => ({ dept: deptParam.parse(dept), id }),
    stringify: ({ dept, id }) => ({ dept: deptParam.format(dept), id }),
  },
  beforeLoad: ({ context, params }) => {
    if (!reachableDepartments(context.bootstrap).includes(params.dept)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  loader: async ({ context, params }): Promise<ContentDetailDto | null> =>
    params.id === 'new' ? null : context.queryClient.ensureQueryData(contentQuery(Number(params.id))),
  component: DashboardForm,
});

function DashboardForm() {
  const { bootstrap } = Route.useRouteContext();
  const { dept, id } = Route.useParams();
  const navigate = useNavigate();

  // The row as it stands now. ⚠️ Not `Route.useLoaderData()`: a loader runs on navigation and
  // never again, so after one save the screen still held the `rowVersion` from when the page
  // opened, and the second save was answered 409 — blaming somebody who does not exist. The loader
  // above is the *preload*; what the screen reads is the query it filled (design M0 §7.3).
  const row = useQuery({ ...contentQuery(Number(id)), enabled: id !== 'new' }).data ?? null;

  const backToDashboard = () => void navigate({ to: '/staff/$dept', params: { dept } });

  return (
    <ContentFormScreen
      config={CONFIG}
      bootstrap={bootstrap}
      department={dept}
      id={id}
      content={row}
      breadcrumbTo={`/staff/${deptParam.format(dept)}`}
      onCreated={async (created) => {
        await navigate({ to: '/staff/$dept/dashboard/$id', params: { dept, id: String(created) } });
      }}
      onFinished={backToDashboard}
    />
  );
}
