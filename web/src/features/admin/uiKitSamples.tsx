import { Button } from '@ivao/atmosphere-react';
import { queryOptions } from '@tanstack/react-query';
import { Plane } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import type { Bootstrap } from '../../shared/api/bootstrap';
import { ProblemAlert, SchemaForm, localized, localizedObject } from '../../shared/forms';
import { DataList, col, listSearchSchema, type ColumnSpec, type Page } from '../../shared/list';
import {
  CALENDAR_VIEWS,
  CalendarView,
  ConfirmDialog,
  ContactForm,
  EmptyState,
  Hero,
  LiveStatusStrip,
  MarkdownContent,
  MediaPicker,
  Notice,
  PageShell,
  SectionHeader,
  StatTile,
  useNotice,
  type MediaLibraryQuery,
  type PickableMedia,
  type CalendarItem,
  type CalendarViewMode,
} from '../../shared/ui';

/**
 * One mounted example per component of the closed list. Each is a component of its own so that it
 * may use hooks, and each takes its text from i18n exactly like every other screen: the gallery is
 * not exempt from the rule it exists to demonstrate.
 *
 * `uiKitSections.tsx` is what puts them in order; nothing here knows about the list.
 */

/**
 * A schema that exercises every kind of field the generator draws — including the five it learned
 * in G2, which is what makes this gallery the place to check one rather than a screen that happens
 * to use it.
 */
const sampleSchema = z.object({
  title: localized(),
  note: z.string().meta({ multiline: true }),
  reference: z.string(),
  weight: z.number().int(),
  published: z.boolean(),
  visibility: z.enum(['Public', 'Members', 'Staff', 'Department']),
  picture: z.number().optional().meta({ media: true }),
  icon: z.string().optional().meta({ icon: true }),
  happensAt: z.string().optional().meta({ datetime: true }),
  expiresOn: z.string().optional().meta({ date: true }),
  seo: localizedObject({
    title: z.string().optional(),
    ogImageMediaId: z.number().optional().meta({ media: true }),
  }).optional(),
  cards: z.array(z.object({ name: z.string() })),
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

/**
 * Two files to choose between, one an image and one not, so the gallery shows both halves of the
 * picker. The picture is a data URI rather than an upload: the gallery must not depend on an
 * installation having a file in it.
 */
const sampleMedia: PickableMedia[] = [
  {
    id: 1,
    fileName: 'banner.png',
    contentType: 'image/png',
    alt: { en: 'A blue square', it: 'Un quadrato blu' },
    url:
      'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk' +
      'YPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==',
  },
  {
    id: 2,
    fileName: 'briefing.pdf',
    contentType: 'application/pdf',
    alt: { en: 'Briefing', it: 'Briefing' },
    url: '#',
  },
];

// The key is typed as the loose one the picker's prop declares: a library is carried around by a
// component that cannot know which resource it came from.
const sampleMediaKey: readonly unknown[] = ['ui-kit', 'sample-media'];

const sampleMediaQuery: MediaLibraryQuery = queryOptions({
  queryKey: sampleMediaKey,
  queryFn: () => Promise.resolve({ items: sampleMedia, total: sampleMedia.length }),
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

export function NoticeSample() {
  const { t } = useTranslation();
  const notice = useNotice();

  return (
    <div className="flex flex-col gap-3">
      <Notice tone="error" title={t('uiKit.sample.noticeError')} />
      <Notice tone="warning" title={t('uiKit.sample.noticeWarning')} />
      <Notice tone="success" title={t('uiKit.sample.noticeSuccess')} />
      <Notice tone="info" title={t('uiKit.sample.noticeInfo')} />

      {/* The other half of the same component: a confirmation is said in the corner and then gone,
          and the gallery is the one place both halves can be seen next to each other. */}
      <div>
        <Button
          type="button"
          variant="secondary"
          onClick={() => notice({ tone: 'success', title: t('uiKit.sample.noticeSuccess') })}
        >
          {t('uiKit.sample.noticeToast')}
        </Button>
      </div>
    </div>
  );
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

export function SchemaFormSample({ bootstrap }: { bootstrap: Bootstrap }) {
  const { t } = useTranslation();
  const locales = bootstrap.division.locales;

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
        happensAt: '2026-06-01T12:00:00Z',
        seo: Object.fromEntries(locales.map((locale) => [locale, { title: '' }])),
        cards: [{ name: 'One' }, { name: 'Two' }],
      }}
      locales={locales}
      labels="uiKit.sample.form"
      submitLabel={t('common.save')}
      onSubmit={() => Promise.resolve()}
      // The gallery is a page about the components, so the library is the two invented files above
      // rather than whatever this installation happens to hold today.
      mediaLibrary={sampleMediaQuery}
      division={{
        defaultLocale: bootstrap.division.defaultLocale,
        timezone: bootstrap.division.timezone,
      }}
    />
  );
}

export function MediaPickerSample({ bootstrap }: { bootstrap: Bootstrap }) {
  const { i18n } = useTranslation();
  const [chosen, setChosen] = useState<number | null>(sampleMedia[0]?.id ?? null);

  return (
    <MediaPicker
      query={sampleMediaQuery}
      value={chosen}
      onChange={setChosen}
      locale={i18n.language}
      defaultLocale={bootstrap.division.defaultLocale}
    />
  );
}

/**
 * Three entries around today, so the grid has something in it whichever day the gallery is opened.
 * Invented here rather than fetched: the gallery is a page about the components.
 */
const sampleCalendar: CalendarItem[] = [
  {
    id: 1,
    kind: 'meeting',
    title: { en: 'Staff meeting', it: 'Riunione dello staff' },
    startsAt: new Date(Date.now() + 36e5).toISOString(),
    allDay: false,
  },
  {
    id: 2,
    kind: 'deadline',
    title: { en: 'Applications close', it: 'Chiusura delle candidature' },
    startsAt: new Date(Date.now() + 3 * 864e5).toISOString(),
    allDay: true,
  },
  {
    id: 3,
    kind: 'event',
    title: { en: 'Night flight', it: 'Volo notturno' },
    startsAt: new Date(Date.now() + 6 * 864e5).toISOString(),
    allDay: false,
  },
];

export function CalendarViewSample({ bootstrap }: { bootstrap: Bootstrap }) {
  const { t } = useTranslation();
  const [view, setView] = useState<CalendarViewMode>('month');
  const [anchor, setAnchor] = useState(() => new Date());

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap gap-2">
        {CALENDAR_VIEWS.map((mode) => (
          <Button
            key={mode}
            size="sm"
            variant={mode === view ? 'primary' : 'ghost'}
            onClick={() => setView(mode)}
          >
            {t(`calendar.public.views.${mode}`)}
          </Button>
        ))}
      </div>

      <CalendarView
        items={sampleCalendar}
        view={view}
        anchor={anchor}
        onAnchorChange={setAnchor}
        // The zone of the division, exactly as the real screens hand it over: the gallery is where
        // a fork sees that every time comes with UTC beside it.
        timezone={bootstrap.division.timezone}
        empty={t('calendar.public.empty')}
      />
    </div>
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

/**
 * The live strip, drawn from an answer written here rather than from the network. Same reason the
 * data blocks are mounted with their `exampleData`: a gallery that asked the API would show whatever
 * is connected right now — which on a quiet night, or on an installation with no IVAO credentials,
 * is nothing at all, and an empty section teaches nobody what the component looks like.
 */
export function LiveStatusStripSample() {
  return (
    <LiveStatusStrip
      status={{
        updatedAt: '2026-09-07T09:00:00Z',
        figures: [
          { figure: 'divisionAtc', value: 4 },
          { figure: 'divisionPilots', value: 37 },
        ],
      }}
    />
  );
}

/**
 * The contact form, wired to nothing. A gallery that actually sent a message would put a row in the
 * queue of a department every time somebody scrolled past it.
 */
export function ContactFormSample() {
  return <ContactForm onSubmit={() => Promise.resolve()} />;
}
