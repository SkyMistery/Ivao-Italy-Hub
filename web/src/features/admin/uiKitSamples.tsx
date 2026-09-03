import { Button } from '@ivao/atmosphere-react';
import { queryOptions } from '@tanstack/react-query';
import { Plane } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import type { Bootstrap } from '../../shared/api/bootstrap';
import { ProblemAlert, SchemaForm, localized } from '../../shared/forms';
import { DataList, col, listSearchSchema, type ColumnSpec, type Page } from '../../shared/list';
import {
  ConfirmDialog,
  EmptyState,
  Hero,
  MarkdownContent,
  PageShell,
  SectionHeader,
  StatTile,
} from '../../shared/ui';

/**
 * One mounted example per component of the closed list. Each is a component of its own so that it
 * may use hooks, and each takes its text from i18n exactly like every other screen: the gallery is
 * not exempt from the rule it exists to demonstrate.
 *
 * `uiKitSections.tsx` is what puts them in order; nothing here knows about the list.
 */

/** A schema that exercises every kind of field the generator draws. */
const sampleSchema = z.object({
  title: localized(),
  note: z.string().meta({ multiline: true }),
  reference: z.string(),
  weight: z.number().int(),
  published: z.boolean(),
  visibility: z.enum(['Public', 'Members', 'Staff', 'Department']),
});

const localizedOnlySchema = z.object({ title: localized() });

interface SampleRow {
  id: number;
  title: Record<string, string>;
  url: string;
  isActive: boolean;
  updatedAt: string;
}

const sampleColumns: readonly ColumnSpec<SampleRow>[] = [
  col.localized('title'),
  col.text('url'),
  col.boolean('isActive'),
  col.date('updatedAt'),
];

const samplePage: Page<SampleRow> = {
  items: [
    {
      id: 1,
      title: { en: 'Flight plan', it: 'Piano di volo' },
      url: 'https://www.ivao.aero',
      isActive: true,
      updatedAt: '2026-09-03T10:00:00Z',
    },
  ],
  page: 1,
  pageSize: 25,
  total: 1,
};

const sampleListQuery = queryOptions({
  queryKey: ['ui-kit', 'sample-list'] as const,
  queryFn: () => Promise.resolve(samplePage),
  staleTime: Number.POSITIVE_INFINITY,
});

const blank = (locales: readonly string[]) => Object.fromEntries(locales.map((locale) => [locale, '']));

export function HeroSample() {
  const { t } = useTranslation();
  return <Hero title={t('uiKit.sample.heroTitle')} lead={t('uiKit.sample.heroLead')} />;
}

export function SectionHeaderSample() {
  const { t } = useTranslation();
  return <SectionHeader title={t('uiKit.sample.sectionTitle')} description={t('uiKit.sample.heroLead')} />;
}

export function StatTileSample() {
  const { t } = useTranslation();
  return <StatTile label={t('uiKit.sample.statLabel')} value="128" Icon={Plane} />;
}

export function PageShellSample() {
  const { t } = useTranslation();
  return (
    <PageShell
      title={t('uiKit.sample.pageTitle')}
      breadcrumb={[{ label: 'ED' }, { label: t('uiKit.sample.pageTitle') }]}
    >
      <p className="text-muted-foreground text-sm">{t('uiKit.sample.pageBody')}</p>
    </PageShell>
  );
}

export function EmptyStateSample() {
  const { t } = useTranslation();
  return <EmptyState title={t('list.empty.title')} description={t('list.empty.description')} />;
}

export function MarkdownSample() {
  const { t } = useTranslation();
  return <MarkdownContent source={t('uiKit.sample.markdown')} />;
}

export function ProblemAlertSample() {
  const { t } = useTranslation();
  return <ProblemAlert summary={t('uiKit.sample.problem')} />;
}

export function ConfirmDialogSample() {
  const { t } = useTranslation();
  return (
    <ConfirmDialog
      triggerText={t('common.delete')}
      title={t('links.delete.title')}
      description={t('links.delete.description')}
      confirmText={t('common.delete')}
      onConfirm={() => undefined}
    />
  );
}

export function LocaleFieldsSample({ locales }: { locales: readonly string[] }) {
  const { t } = useTranslation();

  return (
    <SchemaForm
      schema={localizedOnlySchema}
      defaults={{ title: blank(locales) }}
      locales={locales}
      labels="uiKit.sample.form"
      submitLabel={t('common.save')}
      onSubmit={() => Promise.resolve()}
    />
  );
}

export function SchemaFormSample({ locales }: { locales: readonly string[] }) {
  const { t } = useTranslation();

  return (
    <SchemaForm
      schema={sampleSchema}
      defaults={{
        title: blank(locales),
        note: '',
        reference: '',
        weight: 0,
        published: true,
        visibility: 'Public',
      }}
      locales={locales}
      labels="uiKit.sample.form"
      submitLabel={t('common.save')}
      onSubmit={() => Promise.resolve()}
    />
  );
}

export function DataListSample({ bootstrap }: { bootstrap: Bootstrap }) {
  const { t, i18n } = useTranslation();

  return (
    <DataList
      columns={sampleColumns}
      query={sampleListQuery}
      labels="uiKit.sample.list"
      locale={i18n.language}
      defaultLocale={bootstrap.division.defaultLocale}
      timezone={bootstrap.division.timezone}
      search={listSearchSchema.parse({})}
      onSearchChange={() => undefined}
      actions={() => (
        <Button variant="ghost" size="sm">
          {t('common.edit')}
        </Button>
      )}
    />
  );
}
