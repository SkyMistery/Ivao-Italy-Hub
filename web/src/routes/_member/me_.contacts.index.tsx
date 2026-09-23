import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { myContactColumns } from '../../features/contacts/list';
import { myContactsListQuery } from '../../features/contacts/queries';
import { DataList, listSearchSchema } from '../../shared/list';
import { PageShell } from '../../shared/ui';

/**
 * The member's own threads (M2, T14a): what they wrote to the departments and the conversations they were added to.
 * Recipe 2 (design M0 §7.3), over the same engine as the queue of a department, as a personal view.
 */
export const Route = createFileRoute('/_member/me_/contacts/')({
  validateSearch: listSearchSchema,
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps }) => context.queryClient.ensureQueryData(myContactsListQuery(deps)),
  component: MyContactsPage,
});

function MyContactsPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  return (
    <PageShell
      title={t('contacts.mine.title')}
      description={t('contacts.mine.description')}
      breadcrumb={[{ label: t('contacts.mine.title') }]}
    >
      <DataList
        columns={myContactColumns}
        query={myContactsListQuery(search)}
        labels="contacts"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
        actions={(row) => (
          <Button asChild variant="ghost" size="sm">
            <Link to="/me/contacts/$id" params={{ id: String(row.id) }}>
              {t('contacts.open')}
            </Link>
          </Button>
        )}
      />
    </PageShell>
  );
}
