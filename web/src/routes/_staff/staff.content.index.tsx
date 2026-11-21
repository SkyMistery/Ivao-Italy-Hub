import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { contentSearchSchema, defaultView } from '../../features/content/contentSearch';
import { ContentListScreen, CreateLabel, EditLabel } from '../../features/content/ContentListScreen';
import { CONTENT_KINDS } from '../../features/content/kinds';
import { contentListQuery, templateListQuery } from '../../features/content/queries';

/**
 * Recipe 2 (design M0 §7.3): the pages, the news, the documents and the templates of the back
 * office, one screen with the kind and the department as typed search parameters (note
 * 2026-09-13-contenuti-centralizzati). There is no table markup here: the columns are declared in
 * `features/content/kinds.ts` and drawn by `DataList`.
 *
 * Templates of the ordinary kinds are not in the list of pages. The server keeps them out unless a
 * caller asks (`CrudOptions.DefaultFilters`), and the two callers that do are the "Templates" kind of
 * this screen and the picker inside it.
 */
export const Route = createFileRoute('/_staff/staff/content/')({
  validateSearch: contentSearchSchema,
  loaderDeps: ({ search }) => search,
  loader: async ({ context, deps }) => {
    if (deps.kind === 'Template') {
      await context.queryClient.ensureQueryData(templateListQuery(deps.department, deps));
    } else if (deps.kind === 'Page' && defaultView(deps) === 'tree') {
      // The tree is read by the screen, in the language on screen, which names the pages written in code.
    } else {
      await context.queryClient.ensureQueryData(contentListQuery(deps.department, deps, deps.kind));
    }
  },
  component: ContentPage,
});

function ContentPage() {
  const { t } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  const open = (id: number) => void navigate({ to: '/staff/content/$id', params: { id: String(id) } });

  return (
    <ContentListScreen
      bootstrap={bootstrap}
      search={search}
      onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
      onCreatedFromTemplate={open}
      createLink={({ department, kind, template }) => (
        <Button asChild>
          <Link
            to="/staff/content/$id"
            params={{ id: 'new' }}
            search={{ kind, department, ...(template ? { template } : {}) }}
          >
            <CreateLabel titles={template ? 'templates' : CONTENT_KINDS[kind].titles} />
          </Link>
        </Button>
      )}
      rowAction={(row) => (
        <Button asChild variant="ghost" size="sm">
          <Link to="/staff/content/$id" params={{ id: String(row.id) }}>
            <EditLabel />
          </Link>
        </Button>
      )}
      treeAction={(row, newHere) =>
        row.id === null ? null : (
          <>
            {newHere === undefined ? null : (
              <Button asChild variant="ghost" size="sm">
                <Link
                  to="/staff/content/$id"
                  params={{ id: 'new' }}
                  search={{ kind: 'Page', department: newHere, parent: row.id }}
                >
                  {t('content.tree.newHere')}
                </Link>
              </Button>
            )}
            {row.mayEdit ? (
              <Button asChild variant="ghost" size="sm">
                <Link to="/staff/content/$id" params={{ id: String(row.id) }}>
                  <EditLabel />
                </Link>
              </Button>
            ) : null}
          </>
        )
      }
    />
  );
}
