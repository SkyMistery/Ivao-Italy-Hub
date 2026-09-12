import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { Link, createFileRoute, redirect } from '@tanstack/react-router';
import { Pencil } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { ContentRenderer, EmbeddingContext, readBody, usePublishedEmbedding } from '../../blocks';
import { contentListQuery, publicContentQuery } from '../../features/content/queries';
import { holdsPermission, reachableDepartments } from '../../shared/api/bootstrap';
import { deptParam } from '../../shared/api/department';
import { listSearchSchema } from '../../shared/list';
import { PageShell } from '../../shared/ui';

/**
 * The home of a department: what somebody sees arriving at `/staff/<dept>` (design M1 §14, note
 * 2026-09-05-dashboard-di-dipartimento).
 *
 * It is **blocks and not tiles**, and that is the whole decision: a dashboard is a page the
 * department writes, and this hub already has a way of writing pages. The base and the tools are
 * given — one row per department, seeded from a template — and the arrangement is the department's,
 * in the editor it already uses. Nothing here knows what is on it.
 *
 * What it shows is the **published** version, like every other reader of content: the draft is what
 * the coordinator is working on, and their colleagues keep seeing the last thing that was
 * published. The row is `Visibility.Department`, so the query filter is what decides who may read
 * it — including the people a grant reached, which is what `HubClaims.BuildIdentity` was corrected
 * for in this phase.
 */
export const Route = createFileRoute('/_staff/staff/$dept/')({
  params: {
    parse: ({ dept }) => ({ dept: deptParam.parse(dept) }),
    stringify: ({ dept }) => ({ dept: deptParam.format(dept) }),
  },
  beforeLoad: ({ context, params }) => {
    if (!reachableDepartments(context.bootstrap).includes(params.dept)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  loader: ({ context, params }) =>
    // Not `ensureQueryData`: a department whose dashboard has never been published has no page to
    // read, and that is a state to draw rather than an error to throw.
    context.queryClient.ensureQueryData(dashboardQuery(params.dept)).catch(() => null),
  component: DepartmentDashboard,
});

/** What editing a dashboard needs; it is `Content.Edit`, because a dashboard is content. */
const CONTENT_EDIT = 'Content.Edit';

/** The dashboard of one department. Its slug is the department's own code, in lower case. */
function dashboardQuery(department: string) {
  return publicContentQuery('Dashboard', department.toLowerCase());
}

function DepartmentDashboard() {
  const { t } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { dept } = Route.useParams();

  const dashboard = useQuery({ ...dashboardQuery(dept), retry: false });
  const embedding = usePublishedEmbedding(dashboard.data);

  // Where the "edit" button leads. A department has exactly one dashboard row, so the ordinary back
  // office list of that kind is the answer, and only asked of somebody who could act on it: a button
  // that leads to a 403 is a button that teaches people to distrust buttons.
  const mayEdit = holdsPermission(bootstrap, CONTENT_EDIT, dept);
  const row = useQuery({
    ...contentListQuery(dept, listSearchSchema.parse({ pageSize: 1 }), 'Dashboard'),
    enabled: mayEdit,
  });

  const editable = row.data?.items[0];

  return (
    <PageShell
      title={t('dashboard.title', { department: t(`departments.${dept}`) })}
      description={t('dashboard.description')}
      breadcrumb={[{ label: dept }]}
      actions={
        editable === undefined ? undefined : (
          <Button asChild variant="secondary">
            <Link to="/staff/$dept/dashboard/$id" params={{ dept, id: String(editable.id) }}>
              <Pencil aria-hidden className="mr-2 size-4" />
              {t('dashboard.edit')}
            </Link>
          </Button>
        )
      }
    >
      {dashboard.data ? (
        <EmbeddingContext.Provider value={embedding}>
          <ContentRenderer body={readBody(dashboard.data.body)} />
        </EmbeddingContext.Provider>
      ) : (
        // An honest empty state rather than a blank page: a department whose dashboard was deleted,
        // or one added to the division since the last start, has nothing to show and is told why.
        <p className="text-muted-foreground text-sm">{t('dashboard.missing')}</p>
      )}
    </PageShell>
  );
}
