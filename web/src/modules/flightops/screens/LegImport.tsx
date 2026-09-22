import {
  Badge,
  Button,
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
import { useQuery } from '@tanstack/react-query';
import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ApiError } from '../../../shared/api/problem';
import { describeProblem } from '../../../shared/forms';
import { Notice } from '../../../shared/ui';
import {
  legImportPreview,
  tourLegsQuery,
  useLegChange,
  type LegImportLineDto,
  type LegImportRequest,
  type TourDetailDto,
} from '../api';
import { downloadLegFileModel, LEG_FILE_COLUMNS, readLegFile, type LegFile } from './legFile';

/**
 * The import of the legs of a tour from a file (design M2 §8.4, T8), part of the leg editor and of its declared
 * exception. The file is read here; the server answers the differences without writing — added, changed, restored,
 * kept, deleted, retired — and the import is applied only as it was previewed: the server refuses it if the legs
 * changed in between. "Merge" is the default, because a partial file is normal (ADR-051 of Toursystem).
 */

type Mode = LegImportRequest['mode'];

const OUTCOME_COLORS: Readonly<Record<LegImportLineDto['outcome'], 'gray' | undefined>> = {
  Added: undefined,
  Changed: undefined,
  Restored: undefined,
  Unchanged: 'gray',
  Kept: 'gray',
  Deleted: undefined,
  Retired: undefined,
};

/** "rows[3].arrivalIcao": the row of the request and the field. */
const ROW_FIELD = /^rows\[(\d+)\]\.(\w+)$/;

export function LegImport({ tour, onClose }: { tour: TourDetailDto; onClose: () => void }) {
  const { t, i18n } = useTranslation();
  const fileId = useId();
  const modeId = useId();
  const change = useLegChange(tour.id);
  const [file, setFile] = useState<LegFile | null>(null);
  const [mode, setMode] = useState<Mode>('Merge');
  const [reason, setReason] = useState('');
  const [refusal, setRefusal] = useState<string | null>(null);

  // The grid is part of the key: a leg written meanwhile asks for the differences again.
  const grid = useQuery(tourLegsQuery(tour.id)).data;
  const rows = file !== null && file.problems.length === 0 ? file.rows : null;
  const preview = useQuery({
    queryKey: ['flightops', 'legs', tour.id, 'import', mode, rows, grid] as const,
    queryFn: () => legImportPreview(tour.id, { rows: rows ?? [], mode, reason: null, fingerprint: null }),
    enabled: rows !== null,
    retry: false,
    gcTime: 0,
  });

  /** A refusal of the server, said on the line of the file it is about. */
  const rowProblems = (error: unknown): string[] | null => {
    if (!(error instanceof ApiError) || error.status !== 400 || file === null) {
      return null;
    }

    const said: string[] = [];
    for (const [key, messages] of Object.entries(error.problem?.errors ?? {})) {
      const match = ROW_FIELD.exec(key);
      if (match === null) {
        return null;
      }

      said.push(
        t('flightops:legs.import.rowProblem', {
          line: file.lines[Number(match[1])] ?? '?',
          field: t(`flightops:legs.fields.${match[2]}`, { defaultValue: match[2] }),
          problem: messages.map((message) => t(message)).join(' '),
        }),
      );
    }

    return said;
  };

  const choose = async (chosen: File | undefined) => {
    setRefusal(null);
    setFile(chosen === undefined ? null : await readLegFile(chosen));
  };

  const apply = () => {
    if (rows === null || preview.data === undefined) {
      return;
    }

    setRefusal(null);
    change.mutate(
      {
        kind: 'import',
        request: {
          rows,
          mode,
          reason: reason.trim() === '' ? null : reason.trim(),
          fingerprint: preview.data.fingerprint,
        },
      },
      {
        onSuccess: onClose,
        onError: (error) => setRefusal(describeProblem(error, t, i18n.language)),
      },
    );
  };

  const lines = preview.data?.lines ?? [];
  const counts = lines.reduce<Partial<Record<LegImportLineDto['outcome'], number>>>(
    (sum, line) => ({ ...sum, [line.outcome]: (sum[line.outcome] ?? 0) + 1 }),
    {},
  );
  const serverRows = preview.isError ? rowProblems(preview.error) : null;
  const reasonRequired = preview.data?.reasonRequired === true;
  const nothingToDo = lines.every((line) => line.outcome === 'Unchanged' || line.outcome === 'Kept');

  return (
    <section
      aria-label={t('flightops:legs.import.title')}
      className="border-border flex flex-col gap-4 rounded-md border p-4"
    >
      <div className="flex flex-col gap-1">
        <h3 className="font-semibold">{t('flightops:legs.import.title')}</h3>
        <Subtle className="text-sm">
          {t('flightops:legs.import.help', { columns: LEG_FILE_COLUMNS.join(', ') })}
        </Subtle>
      </div>

      <div className="flex flex-wrap items-end gap-4">
        <div className="flex flex-col gap-1">
          <label htmlFor={fileId} className="text-sm">
            {t('flightops:legs.import.file.label')}
          </label>
          <Input
            id={fileId}
            type="file"
            accept=".xlsx,.xls,.ods,.csv"
            disabled={change.isPending}
            onChange={(event) => void choose(event.target.files?.[0])}
          />
        </div>
        <div className="flex w-72 flex-col gap-1">
          <label htmlFor={modeId} className="text-sm">
            {t('flightops:legs.import.mode.label')}
          </label>
          <Select
            id={modeId}
            value={mode}
            disabled={change.isPending}
            onValueChange={(next) => setMode(next as Mode)}
            items={[
              { value: 'Merge', label: t('flightops:legs.import.mode.Merge') },
              { value: 'Replace', label: t('flightops:legs.import.mode.Replace') },
            ]}
          />
        </div>
        <Button type="button" variant="ghost" onClick={() => void downloadLegFileModel(`legs-${tour.slug}`)}>
          {t('flightops:legs.import.model')}
        </Button>
      </div>
      <Subtle className="text-sm">{t(`flightops:legs.import.mode.explain.${mode}`)}</Subtle>

      {file !== null && file.problems.length > 0 ? (
        <Notice
          tone="error"
          title={t('flightops:legs.import.file.refused')}
          description={
            <ul className="list-disc pl-4">
              {file.problems.map((problem) => (
                <li key={`${problem.line}-${problem.key}-${problem.column}`}>
                  {t(`flightops:legs.import.file.${problem.key}`, {
                    line: problem.line,
                    column: problem.column,
                  })}
                </li>
              ))}
            </ul>
          }
        />
      ) : null}

      {preview.isError ? (
        serverRows === null ? (
          <Notice tone="error" title={describeProblem(preview.error, t, i18n.language) ?? ''} />
        ) : (
          <Notice
            tone="error"
            title={t('flightops:legs.import.file.refused')}
            description={
              <ul className="list-disc pl-4">
                {serverRows.map((said) => (
                  <li key={said}>{said}</li>
                ))}
              </ul>
            }
          />
        )
      ) : null}

      {preview.data === undefined ? null : (
        <>
          <div className="flex flex-wrap gap-2" aria-label={t('flightops:legs.import.summary')}>
            {Object.entries(counts).map(([outcome, count]) => (
              <Badge
                key={outcome}
                variant="flat"
                text={t(`flightops:legs.import.outcome.${outcome}`, { count })}
              />
            ))}
          </div>

          <div className="overflow-x-auto">
            <TableRoot>
              <TableHeader>
                <TableRow>
                  <TableHead className="px-2">{t('flightops:legs.import.columns.number')}</TableHead>
                  <TableHead className="px-2">{t('flightops:legs.import.columns.route')}</TableHead>
                  <TableHead className="px-2">{t('flightops:legs.import.columns.outcome')}</TableHead>
                  <TableHead className="px-2">{t('flightops:legs.import.columns.changes')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {lines.map((line) => (
                  <TableRow
                    key={`${line.legId ?? 'new'}-${line.row ?? 'absent'}`}
                    className={
                      line.outcome === 'Unchanged' || line.outcome === 'Kept' ? 'opacity-60' : undefined
                    }
                  >
                    <TableCell className="px-2 tabular-nums">
                      {line.numberBefore === null || line.numberBefore === line.numberAfter
                        ? (line.numberAfter ?? line.numberBefore)
                        : `${line.numberBefore} → ${line.numberAfter ?? '–'}`}
                    </TableCell>
                    <TableCell className="px-2">
                      {line.departureIcao} → {line.arrivalIcao}
                      {line.row === null ? null : (
                        <Subtle className="text-xs">
                          {t('flightops:legs.import.line', { line: file?.lines[line.row] ?? '?' })}
                        </Subtle>
                      )}
                    </TableCell>
                    <TableCell className="px-2">
                      <div className="flex flex-wrap gap-1">
                        <Badge
                          variant="filled"
                          {...(OUTCOME_COLORS[line.outcome] === undefined
                            ? {}
                            : { color: OUTCOME_COLORS[line.outcome] })}
                          text={t(`flightops:legs.import.state.${line.outcome}`)}
                        />
                        {line.hasReports ? (
                          <Badge variant="flat" color="gray" text={t('flightops:legs.state.hasReports')} />
                        ) : null}
                      </div>
                    </TableCell>
                    <TableCell className="px-2">
                      {line.changes.map((field) => t(`flightops:legs.fields.${field}`)).join(', ')}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </TableRoot>
          </div>

          {reasonRequired ? (
            <Input
              aria-label={t('flightops:legs.import.reason')}
              placeholder={t('flightops:legs.import.reason')}
              value={reason}
              onChange={(event) => setReason(event.target.value)}
            />
          ) : null}
        </>
      )}

      {refusal === null ? null : <Notice tone="error" title={refusal} />}

      <div className="flex flex-wrap gap-2">
        <Button
          type="button"
          disabled={
            change.isPending ||
            preview.data === undefined ||
            nothingToDo ||
            (reasonRequired && reason.trim() === '')
          }
          onClick={apply}
        >
          {t('flightops:legs.import.apply')}
        </Button>
        <Button type="button" variant="ghost" disabled={change.isPending} onClick={onClose}>
          {t('common.cancel')}
        </Button>
      </div>
    </section>
  );
}
