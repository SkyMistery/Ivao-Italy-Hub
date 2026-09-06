import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute } from '@tanstack/react-router';

import { ContentListScreen, CreateLabel, EditLabel } from '../../features/content/ContentListScreen';
import { CONTENT_KINDS } from '../../features/content/kinds';
import { contentListQuery } from '../../features/content/queries';
import { listSearchSchema } from '../../shared/list';

/**
 * Recipe 2 (design M0 §7.3), the pages of one department: paging, sorting and searching are the
 * typed search parameters of the route, and there is no table markup here — the columns are
 * declared in `features/content/kinds.ts` and drawn by `DataList`.
 *
 * The `kind` is fixed to `Page`, and its two siblings — `/staff/<dept>/news` and
 * `/staff/<dept>/documents` — are this same screen with the other two kinds (design M1 §3.2).
 *
 * Templates are not in the list. The server keeps them out unless a caller asks, and the one caller
 * that does is the picker inside the screen (`CrudOptions.DefaultFilters`).
 *
 * The department and its guard are on the layout above.
 */
const CONFIG = CONTENT_KINDS.Page;

export const Route = createFileRoute('/_staff/staff/$dept/content/')({
  validateSearch: listSearchSchema,
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps, params }) =>
    context.queryClient.ensureQueryData(contentListQuery(params.dept, deps, CONFIG.kind)),
  component: PagesPage,
});

function PagesPage() {
  const { bootstrap } = Route.useRouteContext();
  const { dept } = Route.useParams();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  const open = (id: string) => void navigate({ to: '/staff/$dept/content/$id', params: { dept, id } });

  return (
    <ContentListScreen
      config={CONFIG}
      bootstrap={bootstrap}
      department={dept}
      search={search}
      onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
      onCreatedFromTemplate={(id) => open(String(id))}
      createButton={
        <Button asChild>
          <Link to="/staff/$dept/content/$id" params={{ dept, id: 'new' }}>
            <CreateLabel titles={CONFIG.titles} />
          </Link>
        </Button>
      }
      rowAction={(row) => (
        <Button asChild variant="ghost" size="sm">
          <Link to="/staff/$dept/content/$id" params={{ dept, id: String(row.id) }}>
            <EditLabel />
          </Link>
        </Button>
      )}
    />
  );
}
