import {
  Badge,
  Button,
  DropdownMenu,
  Input,
  Select,
  Subtle,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRoot,
  TableRow,
} from '@ivao/atmosphere-react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useEffect, useId, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ApiError } from '../../../shared/api/problem';
import { NEW_ROW_VERSION } from '../../../shared/api/rowVersion';
import { describeProblem } from '../../../shared/forms';
import { ConfirmDialog, Notice } from '../../../shared/ui';
import {
  airportsQuery,
  legRemoval,
  rotationsQuery,
  tourLegsQuery,
  useLegChange,
  type LegDto,
  type LegRemovalDto,
  type LegWriteDto,
  type TourDetailDto,
} from '../api';

/**
 * The legs of a tour, as a table (design M2 §8.4): **the declared exception** to the list and form engine (plan 0.79,
 * §16.6), and the `LegGrid` of the closed list of components. Every row is edited where it stands and saved by itself
 * with its version; a refusal lands under its cell. Adding a leg after another — "duplicate", "follows", "close the
 * tour" — fills a new row in, which the server numbers when it is saved; removing asks the server first whether the
 * leg is deleted or retired (§1.4.1), and a retired leg stays in the table with its reason and can be restored.
 *
 * The distance and the estimated time are the server's, computed at every read (§1.5): the table shows them and never
 * computes them. Every write answers with the whole grid, which replaces the one on screen.
 *
 * On a hub tour one more column says where a leg belongs (§1.3, T7b): a rotation of a hub, the connection between two
 * hubs, or nowhere yet. Its place inside the rotation is the server's, from the order of the legs.
 */

/** Where a leg of a hub tour belongs: nowhere yet, the connection between two hubs, or a rotation by its identifier. */
const NOWHERE = 'none';
const CONNECTION = 'connection';

/** A row being written: the saved leg it edits, or a new one after a leg (null: at the end). */
interface Draft {
  readonly key: string;
  readonly legId: number | null;
  readonly after: number | null;
  readonly departureIcao: string;
  readonly arrivalIcao: string;
  readonly realCallsign: string;
  readonly flightNumber: string;
  /** The types, as typed: "A320, A20N". */
  readonly types: string;
  readonly groupIds: readonly number[];
  /** `YYYY-MM-DDTHH:mm`, UTC, like every instant a form of the hub writes; empty for none. */
  readonly releaseAt: string;
  readonly changeReason: string;
  /** `none`, `connection`, or the identifier of a rotation: only a hub tour shows it. */
  readonly placement: string;
}

type Errors = Readonly<Record<string, string>>;

const FIELDS = [
  'departureIcao',
  'arrivalIcao',
  'realCallsign',
  'flightNumber',
  'aircraft',
  'releaseAt',
  'kind',
  'rotationId',
] as const;

function fromLeg(leg: LegDto): Draft {
  return {
    key: String(leg.id),
    legId: leg.id,
    after: null,
    departureIcao: leg.departureIcao,
    arrivalIcao: leg.arrivalIcao,
    realCallsign: leg.realCallsign ?? '',
    flightNumber: leg.flightNumber ?? '',
    types: leg.aircraft.types.join(', '),
    groupIds: leg.aircraft.groupIds,
    releaseAt: leg.releaseAt?.slice(0, 16) ?? '',
    changeReason: '',
    placement:
      leg.kind === 'HubConnection' ? CONNECTION : leg.rotationId === null ? NOWHERE : String(leg.rotationId),
  };
}

function blank(
  key: string,
  after: number | null,
  departureIcao = '',
  arrivalIcao = '',
  placement = NOWHERE,
): Draft {
  return {
    key,
    legId: null,
    after,
    departureIcao,
    arrivalIcao,
    realCallsign: '',
    flightNumber: '',
    types: '',
    groupIds: [],
    releaseAt: '',
    changeReason: '',
    placement,
  };
}

function sameAs(draft: Draft, leg: LegDto): boolean {
  const saved = fromLeg(leg);
  return (
    draft.departureIcao.trim().toUpperCase() === saved.departureIcao &&
    draft.arrivalIcao.trim().toUpperCase() === saved.arrivalIcao &&
    draft.realCallsign.trim() === saved.realCallsign &&
    draft.flightNumber.trim() === saved.flightNumber &&
    draft.types.trim() === saved.types &&
    draft.groupIds.join() === saved.groupIds.join() &&
    draft.releaseAt === saved.releaseAt &&
    draft.placement === saved.placement
  );
}

function toPayload(draft: Draft, rowVersion: string): LegWriteDto {
  const text = (value: string) => (value.trim() === '' ? null : value.trim());

  return {
    departureIcao: draft.departureIcao.trim().toUpperCase(),
    arrivalIcao: draft.arrivalIcao.trim().toUpperCase(),
    realCallsign: text(draft.realCallsign),
    flightNumber: text(draft.flightNumber),
    aircraft: {
      types: draft.types
        .split(/[\s,;]+/)
        .map((type) => type.trim().toUpperCase())
        .filter((type) => type !== ''),
      groupIds: [...draft.groupIds],
    },
    releaseAt: draft.releaseAt === '' ? null : `${draft.releaseAt}:00Z`,
    changeReason: text(draft.changeReason),
    rowVersion,
    kind: draft.placement === CONNECTION ? 'HubConnection' : 'Normal',
    rotationId:
      draft.placement === CONNECTION || draft.placement === NOWHERE ? null : Number(draft.placement),
  };
}

function minutes(value: number | null): string {
  if (value === null) {
    return '';
  }

  return `${Math.floor(value / 60)}:${String(value % 60).padStart(2, '0')}`;
}

/** The cell of an airport: what is typed is the ICAO code, and the airports that match are offered as it changes. */
function AirportCell({
  value,
  iata,
  label,
  error,
  disabled,
  onChange,
}: {
  value: string;
  iata: string | null;
  label: string;
  error: string | undefined;
  disabled: boolean;
  onChange: (next: string) => void;
}) {
  const list = useId();
  const [typed, setTyped] = useState('');
  const offered = useQuery({
    ...airportsQuery(typed),
    enabled: typed.length >= 2,
    placeholderData: keepPreviousData,
  });

  // The same pause as the search box of a list: a request per word, not per letter.
  useEffect(() => {
    const timer = setTimeout(() => setTyped(value.trim()), 300);
    return () => clearTimeout(timer);
  }, [value]);

  return (
    <div className="flex w-20 flex-col gap-1">
      <Input
        aria-label={label}
        aria-invalid={error === undefined ? undefined : true}
        className="px-2 uppercase"
        list={list}
        maxLength={4}
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value.toUpperCase())}
      />
      <datalist id={list}>
        {(offered.data ?? []).map((airport) => (
          <option key={airport.icao} value={airport.icao}>
            {[airport.iata, airport.name].filter(Boolean).join(' · ')}
          </option>
        ))}
      </datalist>
      {iata === null ? null : <Subtle className="text-xs">{iata}</Subtle>}
      {error === undefined ? null : <span className="text-destructive text-xs">{error}</span>}
    </div>
  );
}

function TextCell({
  value,
  label,
  error,
  disabled,
  type = 'text',
  width = 'w-24',
  onChange,
}: {
  value: string;
  label: string;
  error: string | undefined;
  disabled: boolean;
  type?: 'text' | 'datetime-local';
  /** A table of a dozen columns fits a screen only if every cell knows how wide it is. */
  width?: string;
  onChange: (next: string) => void;
}) {
  return (
    <div className={`flex ${width} flex-col gap-1`}>
      <Input
        aria-label={label}
        aria-invalid={error === undefined ? undefined : true}
        className="px-2"
        type={type}
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
      />
      {error === undefined ? null : <span className="text-destructive text-xs">{error}</span>}
    </div>
  );
}

/** Where a leg of a hub tour belongs: a rotation, the connection between two hubs, or nowhere yet. */
function PlacementCell({
  value,
  label,
  items,
  error,
  disabled,
  onChange,
}: {
  value: string;
  label: string;
  items: { value: string; label: string }[];
  error: string | undefined;
  disabled: boolean;
  onChange: (next: string) => void;
}) {
  const id = useId();

  return (
    <div className="flex w-36 flex-col gap-1">
      <label htmlFor={id} className="sr-only">
        {label}
      </label>
      <Select id={id} value={value} disabled={disabled} onValueChange={onChange} items={items} />
      {error === undefined ? null : <span className="text-destructive text-xs">{error}</span>}
    </div>
  );
}

/** "Remove": the server says first whether the leg is deleted or retired, and a retirement needs a reason. */
function RemoveLeg({
  tourId,
  leg,
  disabled,
  onRemove,
}: {
  tourId: number;
  leg: LegDto;
  disabled: boolean;
  onRemove: (reason: string | null) => void;
}) {
  const { t } = useTranslation();
  const [removal, setRemoval] = useState<LegRemovalDto | null>(null);
  const [reason, setReason] = useState('');
  const retiring = removal !== null && removal.outcome !== 'Delete';

  return (
    <ConfirmDialog
      triggerText={t('flightops:legs.actions.remove')}
      title={t('flightops:legs.remove.title', { number: leg.number })}
      {...(removal === null
        ? {}
        : {
            description: t(`flightops:legs.remove.${removal.outcome}`, {
              numbers: removal.numbers.join(', '),
            }),
          })}
      confirmText={t(retiring ? 'flightops:legs.actions.retire' : 'common.delete')}
      disabled={disabled}
      confirmDisabled={removal === null || (retiring && reason.trim() === '')}
      onOpenChange={(open) => {
        setRemoval(null);
        setReason('');
        if (open) {
          void legRemoval(tourId, leg.id).then(setRemoval);
        }
      }}
      onConfirm={() => onRemove(retiring ? reason.trim() : null)}
    >
      {retiring ? (
        <Input
          aria-label={t('flightops:legs.fields.reason')}
          placeholder={t('flightops:legs.fields.reason')}
          value={reason}
          onChange={(event) => setReason(event.target.value)}
        />
      ) : undefined}
    </ConfirmDialog>
  );
}

function RestoreLeg({
  leg,
  disabled,
  onRestore,
}: {
  leg: LegDto;
  disabled: boolean;
  onRestore: (reason: string) => void;
}) {
  const { t } = useTranslation();
  const [reason, setReason] = useState('');

  return (
    <ConfirmDialog
      triggerText={t('flightops:legs.actions.restore')}
      title={t('flightops:legs.restore.title', { number: leg.number })}
      description={t('flightops:legs.restore.description')}
      confirmText={t('flightops:legs.actions.restore')}
      confirmVariant="primary"
      disabled={disabled}
      confirmDisabled={reason.trim() === ''}
      onOpenChange={() => setReason('')}
      onConfirm={() => onRestore(reason.trim())}
    >
      <Input
        aria-label={t('flightops:legs.fields.reason')}
        placeholder={t('flightops:legs.fields.reason')}
        value={reason}
        onChange={(event) => setReason(event.target.value)}
      />
    </ConfirmDialog>
  );
}

export function LegGrid({ tour, editable }: { tour: TourDetailDto; editable: boolean }) {
  const { t, i18n } = useTranslation();
  const grid = useQuery(tourLegsQuery(tour.id)).data;
  const change = useLegChange(tour.id);
  const hub = tour.kind === 'Hub';
  const rotations = useQuery({ ...rotationsQuery(tour.id), enabled: hub }).data?.items ?? [];

  // The rows being written, by key: a saved leg's identifier, or "new-…" for a leg not saved yet.
  const [drafts, setDrafts] = useState<Readonly<Record<string, Draft>>>({});
  const [errors, setErrors] = useState<Readonly<Record<string, Errors>>>({});
  const [refusal, setRefusal] = useState<string | null>(null);
  const [counter, setCounter] = useState(0);

  if (grid === undefined) {
    return null;
  }

  const legs = grid.legs;
  const busy = change.isPending;

  const edit = (draft: Draft, patch: Partial<Draft>) =>
    setDrafts((current) => ({ ...current, [draft.key]: { ...draft, ...patch } }));

  const drop = (key: string) => {
    setDrafts((current) => Object.fromEntries(Object.entries(current).filter(([row]) => row !== key)));
    setErrors((current) => Object.fromEntries(Object.entries(current).filter(([row]) => row !== key)));
  };

  const add = (
    after: number | null,
    departureIcao = '',
    arrivalIcao = '',
    copy?: Draft,
    placement?: string,
  ) => {
    const key = `new-${counter}`;
    setCounter(counter + 1);
    setDrafts((current) => ({
      ...current,
      [key]:
        copy === undefined
          ? blank(key, after, departureIcao, arrivalIcao, placement)
          : { ...copy, key, legId: null, after, changeReason: '' },
    }));
  };

  // "LIRF 2 (3/4)": the hub, the place of the rotation in it, and how many of its legs there are out of its size. Short,
  // because the table has to fit a screen without scrolling (T7a, looked at at 1500 px).
  const placements = [
    { value: NOWHERE, label: t('flightops:legs.placement.none') },
    { value: CONNECTION, label: t('flightops:legs.placement.connection') },
    ...rotations.map((rotation) => ({
      value: String(rotation.id),
      label: t('flightops:legs.placement.rotation', {
        hub: rotation.hubIcao ?? '',
        number: rotations.filter((other) => other.hubId === rotation.hubId).indexOf(rotation) + 1,
        legs: rotation.legs,
        size: rotation.size,
      }),
    })),
  ];

  /** A refusal: what names a cell goes under the cell, the rest — a ready tour that would stop being one — above. */
  const refused = (key: string, error: unknown) => {
    const cells: Record<string, string> = {};
    let rest = false;

    if (error instanceof ApiError && error.status === 400) {
      for (const [field, keys] of Object.entries(error.problem?.errors ?? {})) {
        if ((FIELDS as readonly string[]).includes(field) || field === 'changeReason') {
          cells[field] = keys.map((errorKey) => t(errorKey)).join(' ');
        } else {
          rest = true;
        }
      }
    } else {
      rest = true;
    }

    setErrors((current) => ({ ...current, [key]: cells }));
    setRefusal(rest ? describeProblem(error, t, i18n.language) : null);
  };

  const save = (draft: Draft, leg: LegDto | undefined) => {
    setRefusal(null);
    change.mutate(
      leg === undefined
        ? { kind: 'create', leg: toPayload(draft, NEW_ROW_VERSION), after: draft.after }
        : { kind: 'update', legId: leg.id, leg: toPayload(draft, leg.rowVersion) },
      { onSuccess: () => drop(draft.key), onError: (error) => refused(draft.key, error) },
    );
  };

  // The saved legs in their order, with each new row after the leg it follows; new rows at the end last.
  const rows: { draft: Draft; leg: LegDto | undefined }[] = [];
  const fresh = Object.values(drafts).filter((draft) => draft.legId === null);
  for (const leg of legs) {
    rows.push({ draft: drafts[String(leg.id)] ?? fromLeg(leg), leg });
    rows.push(...fresh.filter((draft) => draft.after === leg.id).map((draft) => ({ draft, leg: undefined })));
  }
  rows.push(
    ...fresh
      .filter((draft) => draft.after === null || !legs.some((leg) => leg.id === draft.after))
      .map((draft) => ({ draft, leg: undefined })),
  );

  const first = legs.find((leg) => leg.retiredAt === null);
  // The column of the estimated time only when there is one: a tour without a reference aircraft has none (§1.5).
  const estimates = grid.totalEstimatedMinutes !== null;
  // A dozen columns fit a screen only with narrow cells: the padding of the table is the first thing to go.
  const cell = 'px-2 py-2 align-top';

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <Subtle className="tabular-nums">
          {t('flightops:legs.totals', {
            count: legs.filter((leg) => leg.retiredAt === null).length,
            distance: grid.totalNm.toLocaleString(i18n.language),
          })}
          {grid.totalEstimatedMinutes === null
            ? null
            : ` · ${t('flightops:legs.totalTime', { time: minutes(grid.totalEstimatedMinutes) })}`}
        </Subtle>
        {editable ? (
          <Button type="button" variant="outline" disabled={busy} onClick={() => add(null)}>
            {t('flightops:legs.actions.add')}
          </Button>
        ) : null}
      </div>

      {refusal === null ? null : <Notice tone="error" title={refusal} />}

      {rows.length === 0 ? (
        <Notice tone="info" title={t('flightops:legs.empty')} />
      ) : (
        <div className="overflow-x-auto">
          <TableRoot>
            <TableHeader>
              <TableRow>
                <TableHead className="px-2">#</TableHead>
                <TableHead className="px-2">{t('flightops:legs.fields.departureIcao')}</TableHead>
                <TableHead className="px-2">{t('flightops:legs.fields.arrivalIcao')}</TableHead>
                {hub ? <TableHead className="px-2">{t('flightops:legs.fields.rotationId')}</TableHead> : null}
                <TableHead className="px-2 text-right">{t('flightops:legs.fields.distanceNm')}</TableHead>
                {estimates ? (
                  <TableHead className="px-2 text-right">
                    {t('flightops:legs.fields.estimatedMinutes')}
                  </TableHead>
                ) : null}
                <TableHead className="px-2">{t('flightops:legs.fields.realCallsign')}</TableHead>
                <TableHead className="px-2">{t('flightops:legs.fields.flightNumber')}</TableHead>
                <TableHead className="px-2">{t('flightops:legs.fields.aircraft')}</TableHead>
                <TableHead className="px-2">{t('flightops:legs.fields.releaseAt')}</TableHead>
                <TableHead className="px-2" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map(({ draft, leg }) => {
                const cellErrors = errors[draft.key] ?? {};
                const retired = leg !== undefined && leg.retiredAt !== null;
                const locked = !editable || busy || retired;
                const dirty = leg === undefined || sameAs(draft, leg) === false;
                const needsReason = leg?.hasReports === true && dirty;

                return (
                  <TableRow
                    key={draft.key}
                    // A name for the row, so that a reader — and a test — finds "leg 3" without counting cells.
                    aria-label={
                      leg === undefined
                        ? t('flightops:legs.state.new')
                        : t('flightops:legs.row', { number: leg.number })
                    }
                    className={retired ? 'opacity-60' : undefined}
                  >
                    <TableCell className={cell}>
                      {/* The number, and under it the state of the leg: a column of its own would not fit. */}
                      <div className="flex w-20 flex-col items-start gap-1">
                        <span className="pt-2 tabular-nums">{leg?.number ?? '+'}</span>
                        {retired ? (
                          <>
                            <Badge variant="filled" color="gray" text={t('flightops:legs.state.retired')} />
                            <Subtle className="text-xs">{leg.retiredReason}</Subtle>
                          </>
                        ) : leg === undefined ? (
                          <Badge variant="flat" text={t('flightops:legs.state.new')} />
                        ) : null}
                        {leg?.hasReports === true ? (
                          <Badge variant="flat" color="gray" text={t('flightops:legs.state.hasReports')} />
                        ) : null}
                      </div>
                    </TableCell>
                    <TableCell className={cell}>
                      <AirportCell
                        label={t('flightops:legs.fields.departureIcao')}
                        value={draft.departureIcao}
                        iata={
                          leg !== undefined && draft.departureIcao === leg.departureIcao
                            ? leg.departureIata
                            : null
                        }
                        error={cellErrors.departureIcao}
                        disabled={locked}
                        onChange={(next) => edit(draft, { departureIcao: next })}
                      />
                    </TableCell>
                    <TableCell className={cell}>
                      <AirportCell
                        label={t('flightops:legs.fields.arrivalIcao')}
                        value={draft.arrivalIcao}
                        iata={
                          leg !== undefined && draft.arrivalIcao === leg.arrivalIcao ? leg.arrivalIata : null
                        }
                        error={cellErrors.arrivalIcao}
                        disabled={locked}
                        onChange={(next) => edit(draft, { arrivalIcao: next })}
                      />
                    </TableCell>
                    {hub ? (
                      <TableCell className={cell}>
                        <PlacementCell
                          label={t('flightops:legs.fields.rotationId')}
                          value={draft.placement}
                          items={placements}
                          error={cellErrors.rotationId ?? cellErrors.kind}
                          disabled={locked}
                          onChange={(next) => edit(draft, { placement: next })}
                        />
                      </TableCell>
                    ) : null}
                    <TableCell className={`${cell} pt-4 text-right tabular-nums`}>
                      {leg === undefined ? '' : leg.distanceNm.toLocaleString(i18n.language)}
                    </TableCell>
                    {estimates ? (
                      <TableCell className={`${cell} pt-4 text-right tabular-nums`}>
                        {minutes(leg?.estimatedMinutes ?? null)}
                      </TableCell>
                    ) : null}
                    <TableCell className={cell}>
                      <TextCell
                        label={t('flightops:legs.fields.realCallsign')}
                        value={draft.realCallsign}
                        error={cellErrors.realCallsign}
                        disabled={locked}
                        onChange={(next) => edit(draft, { realCallsign: next })}
                      />
                    </TableCell>
                    <TableCell className={cell}>
                      <TextCell
                        label={t('flightops:legs.fields.flightNumber')}
                        value={draft.flightNumber}
                        error={cellErrors.flightNumber}
                        disabled={locked}
                        onChange={(next) => edit(draft, { flightNumber: next })}
                      />
                    </TableCell>
                    <TableCell className={cell}>
                      <TextCell
                        // A hub tour has one column more: the room comes from here (looked at at 1500 px, T7b).
                        width={hub ? 'w-28' : 'w-32'}
                        label={t('flightops:legs.fields.aircraft')}
                        value={draft.types}
                        error={cellErrors.aircraft}
                        disabled={locked}
                        onChange={(next) => edit(draft, { types: next })}
                      />
                    </TableCell>
                    <TableCell className={cell}>
                      <TextCell
                        type="datetime-local"
                        width="w-48"
                        label={t('flightops:legs.fields.releaseAt')}
                        value={draft.releaseAt}
                        error={cellErrors.releaseAt}
                        disabled={locked}
                        onChange={(next) => edit(draft, { releaseAt: next })}
                      />
                    </TableCell>
                    <TableCell className={cell}>
                      {editable ? (
                        <div className="flex flex-col items-end gap-1">
                          {needsReason ? (
                            <TextCell
                              width="w-48"
                              label={t('flightops:legs.fields.changeReason')}
                              value={draft.changeReason}
                              error={cellErrors.changeReason}
                              disabled={busy}
                              onChange={(next) => edit(draft, { changeReason: next })}
                            />
                          ) : null}
                          <div className="flex flex-col items-end gap-1">
                            {dirty && !retired ? (
                              <>
                                <Button
                                  type="button"
                                  size="sm"
                                  disabled={busy}
                                  onClick={() => save(draft, leg)}
                                >
                                  {t('common.save')}
                                </Button>
                                <Button
                                  type="button"
                                  size="sm"
                                  variant="ghost"
                                  disabled={busy}
                                  onClick={() => drop(draft.key)}
                                >
                                  {t('common.cancel')}
                                </Button>
                              </>
                            ) : null}
                            {leg === undefined || retired ? null : (
                              <DropdownMenu
                                trigger={
                                  <Button type="button" size="sm" variant="ghost" disabled={busy}>
                                    {t('flightops:legs.actions.addAfter')}
                                  </Button>
                                }
                                items={[
                                  {
                                    label: t('flightops:legs.actions.duplicate'),
                                    onSelect: () => add(leg.id, '', '', fromLeg(leg)),
                                  },
                                  {
                                    label: t('flightops:legs.actions.follows'),
                                    onSelect: () =>
                                      add(
                                        leg.id,
                                        leg.arrivalIcao,
                                        '',
                                        undefined,
                                        leg.rotationId === null ? NOWHERE : String(leg.rotationId),
                                      ),
                                  },
                                  ...(first === undefined
                                    ? []
                                    : [
                                        {
                                          label: t('flightops:legs.actions.closeTour'),
                                          onSelect: () => add(leg.id, leg.arrivalIcao, first.departureIcao),
                                        },
                                      ]),
                                ]}
                              />
                            )}
                            {leg === undefined ? null : retired ? (
                              <RestoreLeg
                                leg={leg}
                                disabled={busy}
                                onRestore={(reason) =>
                                  change.mutate(
                                    { kind: 'restore', legId: leg.id, reason, rowVersion: leg.rowVersion },
                                    { onError: (error) => refused(draft.key, error) },
                                  )
                                }
                              />
                            ) : (
                              <RemoveLeg
                                tourId={tour.id}
                                leg={leg}
                                disabled={busy}
                                onRemove={(reason) =>
                                  change.mutate(
                                    { kind: 'remove', legId: leg.id, reason, rowVersion: leg.rowVersion },
                                    {
                                      onSuccess: () => drop(draft.key),
                                      onError: (error) => refused(draft.key, error),
                                    },
                                  )
                                }
                              />
                            )}
                          </div>
                        </div>
                      ) : null}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </TableRoot>
        </div>
      )}
    </div>
  );
}
