import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { Pencil } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { ContentRenderer, EmbeddingContext, readBody, usePublishedEmbedding } from '../../blocks';
import { holdsPermission, type Bootstrap } from '../../shared/api/bootstrap';
import { useLocalized } from '../../shared/i18n/useLocalized';
import { PageShell } from '../../shared/ui';

import { dashboardQuery } from './dashboards';

/** What editing a dashboard needs; it is `Content.Edit`, because a dashboard is content. */
const CONTENT_EDIT = 'Content.Edit';

/**
 * A dashboard on a screen of its own: a compact bar with the title and, for whoever may, "edit"; under
 * it the tiles (note 2026-09-13-le-dashboard-a-tutto-schermo §3.2). `/staff/{dept}`, `/staff` and
 * `/me` are this with a different slug — a difference between the three would be a second rule to
 * remember.
 *
 * "Edit" leads to the row the screen is showing, found by the identifier the published version
 * carries. ⚠️ Not the first dashboard of the department in the back office list: since D3 the site's
 * department owns three of them (its own, `me` and `staff`), and the first of a list is any of them.
 */
export function DashboardScreen({
  bootstrap,
  slug,
  title,
  description,
  missing,
}: {
  bootstrap: Bootstrap;
  slug: string;
  /** What the bar says. Left out, the title of the row, which the web team writes. */
  title?: string;
  description?: string;
  /** What to say when there is no published row to read. */
  missing: string;
}) {
  const { t } = useTranslation();
  const read = useLocalized();

  const dashboard = useQuery({ ...dashboardQuery(slug), retry: false });
  const embedding = usePublishedEmbedding(dashboard.data);

  const row = dashboard.data;
  // Only offered to somebody who could act on it: a button that leads to a 403 is a button that
  // teaches people to distrust buttons.
  const mayEdit = row !== undefined && holdsPermission(bootstrap, CONTENT_EDIT, row.ownerDepartment);

  return (
    <PageShell
      title={title ?? (row ? read(row.title) : '')}
      {...(description === undefined ? {} : { description })}
      actions={
        mayEdit ? (
          <Button asChild variant="secondary">
            <Link to="/staff/$dept/dashboard/$id" params={{ dept: row.ownerDepartment, id: String(row.id) }}>
              <Pencil aria-hidden className="mr-2 size-4" />
              {t('dashboard.edit')}
            </Link>
          </Button>
        ) : undefined
      }
    >
      {row ? (
        <EmbeddingContext.Provider value={embedding}>
          <ContentRenderer body={readBody(row.body)} media={row.media} dashboard />
        </EmbeddingContext.Provider>
      ) : dashboard.isPending ? null : (
        // An honest empty state rather than a blank page: a dashboard that was deleted, or never
        // published, has nothing to show and is told why.
        <p className="text-muted-foreground text-sm">{missing}</p>
      )}
    </PageShell>
  );
}
