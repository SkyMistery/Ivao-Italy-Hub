import { Button } from '@ivao/atmosphere-react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import {
  emptyMenuItem,
  toFormValues,
  useCreateMenuItem,
  useDeleteMenuItem,
  useUpdateMenuItem,
} from '../../features/menu/mutations';
import { menuDestinationPagesQuery } from '../../features/content/queries';
import { activeLinksQuery } from '../../features/links/queries';
import { menuItemQuery, menuParentsQuery, type MenuItemDetailDto } from '../../features/menu/queries';
import { menuItemSchema, type MenuItemFormValues } from '../../features/menu/schema';
import { deptParam } from '../../shared/api/department';
import { SchemaForm, type ChoiceOption, type Suggestion } from '../../shared/forms';
import { useLocalized } from '../../shared/i18n/useLocalized';
import { ConfirmDialog, PageShell } from '../../shared/ui';

/**
 * One entry of the site menu, and `new` for one that does not exist yet.
 *
 * There is no field in this file: `SchemaForm` reads `features/menu/schema.ts` and draws every one
 * of them, the language tabs of the label included (design M0 §7.5). What this file adds is the one
 * thing a schema cannot hold — the entries this one may hang under, which are rows.
 */
export const Route = createFileRoute('/_staff/staff/$dept/menu/$id')({
  loader: async ({ context, params }): Promise<MenuItemDetailDto | null> =>
    params.id === 'new' ? null : context.queryClient.ensureQueryData(menuItemQuery(Number(params.id))),
  component: MenuItemForm,
});

/**
 * The addresses of this application that are not rows: they are routes, so they cannot be read from
 * a table and they belong here, next to the screen that offers them. A fork that adds a screen adds
 * a line; a fork that removes one removes a line, and the menu of that installation stops offering
 * an address it does not have.
 *
 * ⚠️ The other half is `MenuItemWriteDtoValidator.Screens`, and the two agree **by hand** — a
 * route of this client is not something the contract can carry. An integration test posts a screen
 * the server does not know, exactly as one does for the backgrounds of a section.
 */
const SITE_SCREENS = ['/', '/calendar', '/news', '/documents', '/search', '/contact'] as const;

function MenuItemForm() {
  const { t } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = Route.useRouteContext();
  const { dept, id } = Route.useParams();
  const navigate = useNavigate();

  const isNew = id === 'new';
  const locales = bootstrap.division.locales;
  // The row as it stands now. ⚠️ Not `Route.useLoaderData()`: a loader runs on navigation and
  // never again, so after one save the screen still held the `rowVersion` from when the page
  // opened, and the second save was answered 409 — blaming somebody who does not exist. The loader
  // above is the *preload*; what the screen reads is the query it filled (design M0 §7.3).
  const item = useQuery({ ...menuItemQuery(Number(id)), enabled: id !== 'new' }).data ?? null;

  const defaults = item === null ? emptyMenuItem(locales) : toFormValues(item, locales);

  // The entries this one may hang under: the top level of the menu it is in, and not itself.
  //
  // ⚠️ Read from the menu the row is in *now*, not from the one the select is showing: the
  // generator draws a schema and does not report a value while somebody is typing, and teaching it
  // to would be an extension of the form generator for one screen. Somebody who switches menu and
  // then picks a parent of the other one is refused by the server, on the field, with the message
  // `MenuItemWriteDtoValidator` gives — which is the same answer, one round trip later.
  const siblings = useQuery(menuParentsQuery(defaults.scope));

  const parents: ChoiceOption[] = (siblings.data?.items ?? [])
    // Depth is one: only a top level entry may be a parent, and an entry is never its own.
    .filter((row) => row.parentId === null && String(row.id) !== id)
    .map((row) => ({ value: String(row.id), label: read(row.label) || row.path }));

  // ⚠️ What is being typed in the address, reported by the generator after a pause. Both lists are
  // a page of a hundred rows — the ceiling of the list engine — and a **closed** field that cannot
  // offer the hundred and first cannot point at it either: the address exists, the server would
  // accept it, and the form says "nothing matches here". So what is typed becomes a question to the
  // server rather than a filter over rows already in hand (decided 9 Sep 2026).
  const [typed, setTyped] = useState('');

  // Where a menu entry may lead, and it is the **whole** of it: the field takes one of these and
  // nothing else, here and at the server. Asked for while running the demo of M1 — first "propose
  // the address", then "lock it", so that every address leaving the site lives in `cms_links` and
  // moving the forum is one row rather than a hunt through the site.
  //
  // `keepPreviousData` so the list does not blink empty between one keystroke and the answer: an
  // empty popover reads as "nothing matches", which is the one thing it must not say while asking.
  const pages = useQuery({ ...menuDestinationPagesQuery(typed), placeholderData: keepPreviousData });
  const links = useQuery({ ...activeLinksQuery(typed), placeholderData: keepPreviousData });

  const addresses: Suggestion[] = [
    // The pages, grouped by the department that wrote them. A draft says so: the entry can be
    // written now and switched on when the page goes out.
    ...(pages.data?.items ?? []).map((page) => ({
      value: `/${page.path}`,
      label:
        page.status === 'Draft'
          ? t('menu.draftSuffix', { title: read(page.title) || page.slug })
          : read(page.title) || page.slug,
      group: t(`departments.${page.ownerDepartment}`),
    })),
    // The screens of the application, which are not rows and never will be: a division that wants
    // its calendar in the menu is pointing at a route and not at a page somebody wrote.
    ...SITE_SCREENS.map((path) => ({
      value: path,
      label: t(`menu.screens.${path}`),
      group: t('menu.screensGroup'),
    })),
    // ⚠️ And the addresses that leave the site, which live in one table and only there: a menu
    // entry may lead to a link of the library and to nothing else outside (decided 8 Sep 2026).
    // Whoever owns them — the menu belongs to the site, not to one department.
    ...(links.data?.items ?? []).map((link) => ({
      value: link.url,
      label: read(link.title) || link.url,
      group: t('menu.linksGroup'),
    })),
  ];

  const create = useCreateMenuItem();
  const update = useUpdateMenuItem(Number(id));
  const remove = useDeleteMenuItem();

  const backToList = () => void navigate({ to: '/staff/$dept/menu', params: { dept } });

  const submit = async (values: MenuItemFormValues) => {
    if (isNew) {
      await create.mutateAsync(values);
    } else {
      await update.mutateAsync(values);
    }
    backToList();
  };

  return (
    <PageShell
      title={isNew ? t('menu.create') : t('menu.edit')}
      breadcrumb={[
        { label: dept },
        { label: t('menu.title'), to: `/staff/${deptParam.format(dept)}/menu` },
        { label: isNew ? t('menu.create') : t('menu.edit') },
      ]}
      actions={
        isNew ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('menu.delete.title')}
            description={t('menu.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(Number(id), { onSuccess: backToList })}
          />
        )
      }
    >
      <SchemaForm
        schema={menuItemSchema(parents, addresses)}
        defaults={defaults}
        locales={locales}
        labels="menu"
        onSubmit={submit}
        // The address is the only suggested field here, and the only one that needs asking again.
        onSuggestSearch={(field, text) => {
          if (field === 'path') {
            setTyped(text);
          }
        }}
        submitLabel={t('common.save')}
        secondaryAction={
          <Button asChild variant="ghost">
            <Link to="/staff/$dept/menu" params={{ dept }}>
              {t('common.cancel')}
            </Link>
          </Button>
        }
      />
    </PageShell>
  );
}
