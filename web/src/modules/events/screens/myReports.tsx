import { Badge, Button, H2, H3, Subtle } from '@ivao/atmosphere-react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { bootstrapQuery, preferenceQuery, useSavePreference } from '../../../features/me/queries';
import { SchemaForm, describeProblem } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { Dialog, Notice } from '../../../shared/ui';
import {
  myReportsQuery,
  useSendReport,
  type MyReportDto,
  type ReportKind,
  type ReportableEventDto,
} from '../api';
import {
  atcReportSchema,
  autoReportSchema,
  atcReportValues,
  emptyAtcReport,
  emptyPilotReport,
  pilotReportSchema,
  pilotReportValues,
  type AtcReportFormValues,
  type PilotReportFormValues,
} from '../schemas';

import { eventHref } from './cards';
import { atcItems, checkLine, itemLine, pilotItems } from './reportWords';

/**
 * A member's reports of support on `/events/mine` (design M4 §5.2, §7.1, E14a; note 2026-10-17-il-pirep-di-supporto, readings 3–6): the
 * events still open to one — over, until `reportDays` after their end —, each with «Send a pilot report» and «Send an ATC report» for the
 * kinds not sent yet; and the member's reports, each with where it stands, what the check found of every item, the validator's note, and
 * «Correct» while it is not decided. Sending again replaces every item and checks them again. Drawn when its answer arrives: one that failed
 * says so here, and the rest of the page is read all the same.
 */

/** The verification of a report as a badge colour: found, partly, not, or not asked. */
const VERIFICATION_COLOURS = {
  Valid: 'green',
  Partial: 'yellow',
  Invalid: 'red',
  Unavailable: 'gray',
} as const;

/** Where a report stands as a badge colour. */
const STATE_COLOURS = {
  Submitted: 'blue',
  Accepted: 'green',
  Rejected: 'red',
} as const;

export function ReportsSection() {
  const { t, i18n } = useTranslation();
  const reports = useQuery(myReportsQuery());
  const failed = describeProblem(reports.error, t, i18n.language);
  const sent = reports.data?.reports ?? [];
  const open = reports.data?.open ?? [];

  return (
    <section className="flex flex-col gap-4" aria-label={t('events:mine.reports.title')}>
      <H2>{t('events:mine.reports.title')}</H2>
      <Subtle className="text-sm">{t('events:mine.reports.lead')}</Subtle>
      <AutoReportSwitch />
      {failed === null ? null : <Notice tone="error" title={failed} />}
      {open.length === 0 ? null : (
        <ul className="flex flex-col gap-4" aria-label={t('events:mine.reports.open')}>
          {open.map((event) => (
            <OpenEvent
              key={event.id}
              event={event}
              sent={sent.filter((report) => report.eventId === event.id)}
            />
          ))}
        </ul>
      )}
      {sent.length === 0 ? null : (
        <ul className="flex flex-col gap-4">
          {sent.map((report) => (
            <ReportItem
              key={report.id}
              report={report}
              event={open.find((event) => event.id === report.eventId)}
            />
          ))}
        </ul>
      )}
    </section>
  );
}

/** An event open to a report: its title, until when, and a button for each kind the member has not sent yet. */
function OpenEvent({ event, sent }: { event: ReportableEventDto; sent: readonly MyReportDto[] }) {
  const { t } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const title = read(event.title) || event.slug;
  const missing = (['Pilot', 'Atc'] as const).filter((kind) => !sent.some((report) => report.kind === kind));

  if (missing.length === 0) {
    return null;
  }

  return (
    <li
      className="bg-card text-card-foreground border-border flex flex-col gap-2 rounded-lg border p-4"
      aria-label={title}
    >
      <H3 className="text-lg">
        <RouterAnchor href={eventHref(event.slug)} className="underline">
          {title}
        </RouterAnchor>
      </H3>
      <Subtle className="text-sm">
        {t('events:mine.reports.until', { date: moment(event.closesAtUtc) })}
      </Subtle>
      <div className="flex flex-wrap gap-2">
        {missing.map((kind) => (
          <ReportDialog key={kind} event={event} kind={kind} title={title} />
        ))}
      </div>
    </li>
  );
}

/**
 * A report of the member: its event, its kind, where it stands and what the check made of it; each item with what was found; the
 * validator's note; and «Correct» while it may be.
 */
function ReportItem({ report, event }: { report: MyReportDto; event: ReportableEventDto | undefined }) {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const title = read(report.eventTitle) || report.eventSlug;

  return (
    <li
      className="bg-card text-card-foreground border-border flex flex-col gap-3 rounded-lg border p-4"
      aria-label={t(`events:mine.reports.of.${report.kind}`, { title })}
    >
      <div className="flex flex-wrap items-center gap-2">
        <H3 className="text-lg">
          <RouterAnchor href={eventHref(report.eventSlug)} className="underline">
            {title}
          </RouterAnchor>
        </H3>
        <Badge variant="flat" color="blue" text={t(`events:reports.options.kind.${report.kind}`)} />
        <Badge
          variant="flat"
          color={STATE_COLOURS[report.state]}
          text={t(`events:reports.options.state.${report.state}`)}
        />
        <Badge
          variant="flat"
          color={VERIFICATION_COLOURS[report.verification]}
          text={t(`events:reports.options.verification.${report.verification}`)}
        />
      </div>
      <Subtle className="text-sm tabular-nums">
        {t('events:mine.reports.sent', { date: moment(report.submittedAt) })}
      </Subtle>
      <ul className="flex flex-col gap-1 text-sm">
        {report.items.map((item) => (
          <li key={item.id} className="flex flex-col">
            <span className="tabular-nums">{itemLine(item, report.kind, t, i18n.language)}</span>
            <Subtle className="text-xs">{checkLine(item, report.kind, t)}</Subtle>
          </li>
        ))}
      </ul>
      {report.verification === 'Unavailable' && report.correctable ? (
        <Subtle className="text-sm">{t('events:mine.reports.unavailable')}</Subtle>
      ) : null}
      {report.decisionNote === null ? null : (
        <Subtle className="text-sm">{t('events:mine.reports.note', { note: report.decisionNote })}</Subtle>
      )}
      {report.correctable && event !== undefined ? (
        <div className="flex flex-wrap gap-2">
          <ReportDialog event={event} kind={report.kind} title={title} report={report} />
        </div>
      ) : report.state === 'Submitted' ? (
        <Subtle className="text-sm">{t('events:mine.reports.closed')}</Subtle>
      ) : null}
    </li>
  );
}

/**
 * «Send» or «Correct» a report of a kind: the generated form of its items in a dialog — a pilot's flights, a controller's shifts —, loaded
 * with the items sent when it corrects. What the server refuses lands on the field of its item.
 */
function ReportDialog({
  event,
  kind,
  title,
  report,
}: {
  event: ReportableEventDto;
  kind: ReportKind;
  title: string;
  report?: MyReportDto;
}) {
  const { t } = useTranslation();
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const send = useSendReport();
  const [open, setOpen] = useState(false);

  if (bootstrap === undefined) {
    return null;
  }

  const division = { defaultLocale: bootstrap.division.defaultLocale, timezone: bootstrap.division.timezone };
  const verb = report === undefined ? 'send' : 'correct';
  const trigger = t(`events:mine.reports.${verb}.${kind}`);

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (next) {
          send.reset();
        }
      }}
      trigger={<Button variant={report === undefined ? 'primary' : 'outline'}>{trigger}</Button>}
      title={t(`events:mine.reports.dialogTitle.${kind}`, { title })}
      description={
        kind === 'Atc'
          ? t('events:mine.reports.atcDescription')
          : event.wholeDivision || event.airports.length === 0
            ? t('events:mine.reports.pilotDescription')
            : t('events:mine.reports.pilotDescriptionAirports', { airports: event.airports.join(', ') })
      }
    >
      {kind === 'Pilot' ? (
        <SchemaForm<PilotReportFormValues>
          schema={pilotReportSchema}
          defaults={
            report === undefined ? emptyPilotReport(event.startsAtUtc) : pilotReportValues(report.items)
          }
          locales={[]}
          labels="events:mine.reports.pilot"
          division={division}
          onSubmit={async (values) => {
            await send.mutateAsync({ eventId: event.id, kind, items: pilotItems(values) });
            setOpen(false);
          }}
          submitLabel={trigger}
        />
      ) : (
        <SchemaForm<AtcReportFormValues>
          schema={atcReportSchema}
          defaults={
            report === undefined
              ? emptyAtcReport(event.startsAtUtc, event.endsAtUtc)
              : atcReportValues(report.items)
          }
          locales={[]}
          labels="events:mine.reports.atc"
          division={division}
          onSubmit={async (values) => {
            await send.mutateAsync({ eventId: event.id, kind, items: atcItems(values) });
            setOpen(false);
          }}
          submitLabel={trigger}
        />
      )}
    </Dialog>
  );
}

/** The key of the preference on the server (AutoReports.PreferenceKey, design M4 §1.11). */
export const AUTO_REPORT_PREFERENCE = 'events.autoReport';

/**
 * «Report my participation in events automatically» (design M4 §1.11, E14b): off until the member switches it on, kept on their account. Then,
 * after each event, the system looks for their flights and their shifts and writes their report of support, which a validator decides as any
 * other; one they sent by hand stays as it is.
 *
 * The generated form reads its defaults once, when it is drawn: a save refused draws it again from what the server kept, so the switch
 * never stays turned on a value nobody saved (#333, item 4). The same when the member switched again while a save was on its way: once
 * the last one is answered, a switch that does not say what the server keeps is drawn again from it — never redrawn when it does, which
 * would take the focus off it at every save.
 */
export function AutoReportSwitch() {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const preference = useQuery(preferenceQuery(AUTO_REPORT_PREFERENCE));
  const save = useSavePreference(AUTO_REPORT_PREFERENCE);
  const failed = describeProblem(save.error, t, i18n.language);
  const [drawn, setDrawn] = useState(0);
  const shown = useRef<boolean | null>(null);

  // TanStack calls `mutate`'s own callbacks for the last save only: an earlier one, overtaken, is answered by the last.
  const settled = () => {
    const kept = queryClient.getQueryData(preferenceQuery(AUTO_REPORT_PREFERENCE).queryKey) === true;
    if (shown.current !== kept) {
      shown.current = kept;
      setDrawn((count) => count + 1);
    }
  };

  if (preference.isPending) {
    return null;
  }

  return (
    <div className="flex flex-col gap-2">
      <SchemaForm
        key={drawn}
        schema={autoReportSchema}
        defaults={{ autoReport: preference.data === true }}
        locales={[]}
        labels="events:mine.reports.auto"
        onChange={(values) => {
          shown.current = values.autoReport;
          if (values.autoReport !== (preference.data === true)) {
            save.mutate(values.autoReport, { onSettled: settled });
          }
        }}
      />
      {failed === null ? null : <Notice tone="error" title={failed} />}
    </div>
  );
}
