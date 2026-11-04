import { Button } from '@ivao/atmosphere-react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Upload } from 'lucide-react';
import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { registry } from '../../app/registry';
import { emptyBody, readBody, type Body } from '../../blocks';
import { ApiError } from '../../shared/api/problem';
import { Dialog, Notice, useNotice } from '../../shared/ui';
import { bootstrapQuery } from '../me/queries';

import { useBodyPathDescription } from './bodyPath';
import {
  MAX_IMPORT_BYTES,
  pendingNow,
  readClaudeFile,
  type ImportPending,
  type ImportReady,
  type ImportRefusal,
} from './claudeFile';
import { writesWithClaude } from './claudeInstructions';
import { offeredBlocks } from './palette';
import { siteAddressesQuery, type ContentDetailDto, type ContentKind } from './queries';
import { knownAddresses } from './siteAddresses';
import type { SectionRule } from './templateRules';

/** A refusal, with the paths of its list in the words of the editor («Hero › Text › text», note §3.3). */
function RefusalDialog({ refusal, onClose }: { refusal: ImportRefusal; onClose: () => void }) {
  const { t } = useTranslation();
  const describe = useBodyPathDescription(refusal.body ?? emptyBody());

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={t('content.import.refusedTitle')}
      description={t(refusal.message, refusal.values ?? {})}
    >
      {refusal.problems.length === 0 ? null : (
        <ul className="scroll-thin flex max-h-80 flex-col gap-2 overflow-y-auto text-sm">
          {refusal.problems.map((problem, index) => (
            // The same refusal may be said twice of one path: nothing here is ever reordered.
            <li key={index} className="flex flex-col">
              {problem.path === null ? null : <span className="font-medium">{describe(problem.path)}</span>}
              <span className="opacity-90">{t(problem.key, problem.values ?? {})}</span>
              {problem.detail === undefined ? null : (
                <span className="text-muted-foreground font-mono text-xs">{problem.detail}</span>
              )}
            </li>
          ))}
        </ul>
      )}
    </Dialog>
  );
}

/**
 * «Import from Claude» in the toolbar, beside the download (PR 3.0b; note `2026-10-30-una-pagina-scritta-con-claude`
 * §3, §4). Only in the draft, offered to whoever is offered the download. The file is read here and nothing is uploaded
 * until it has passed every check; then `apply` saves it like any draft, and it **replaces** the draft (answer 3).
 */
export function ClaudeImport({
  content,
  kind,
  locales,
  template,
  holds,
  isDirty,
  draftBase,
  apply,
  disabled,
}: {
  content: ContentDetailDto | null;
  kind: ContentKind;
  locales: readonly string[];
  template: ReadonlyMap<string, SectionRule>;
  holds: (permission: string) => boolean;
  /** Whether what is on screen differs from what was last stored, asked when the file is read. */
  isDirty: () => boolean;
  /** The row the draft is based on, as the server last answered it to this editor. */
  draftBase: () => ContentDetailDto | null;
  /** Saves the file over the draft and puts it on screen; rejects when the server refused it. */
  apply: (file: ImportReady) => Promise<void>;
  disabled: boolean;
}) {
  const { t } = useTranslation();
  const notice = useNotice();
  const queryClient = useQueryClient();
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const input = useRef<HTMLInputElement>(null);
  const [working, setWorking] = useState(false);
  const [refusal, setRefusal] = useState<ImportRefusal | null>(null);
  const [startedOver, setStartedOver] = useState<ImportReady | null>(null);

  if (!writesWithClaude(content, kind, holds)) {
    return null;
  }

  const save = async (file: ImportReady) => {
    // The question may have stayed open: what it was asked about has to be what is still there.
    if (isDirty() || draftBase()?.rowVersion !== file.rowVersion) {
      setRefusal({ outcome: 'refused', message: 'content.import.draftChanged', problems: [], body: null });
      return;
    }

    try {
      await apply(file);
      notice({ tone: 'success', title: t('content.import.done') });
    } catch (error) {
      if (!(error instanceof ApiError)) {
        notice({ tone: 'error', title: t('content.import.failed') });
      } else if (error.status === 409) {
        setRefusal({ outcome: 'refused', message: 'content.import.draftChanged', problems: [], body: null });
      } else {
        // The server's own checks: the same paths, against the body that was sent.
        setRefusal({
          outcome: 'refused',
          message: 'content.import.refused',
          problems: Object.entries(error.problem?.errors ?? {}).flatMap(([path, keys]) =>
            keys.map((key) => ({ path: path.replace(/^body\./, ''), key })),
          ),
          body: file.body,
        });
      }
    }
  };

  const read = async (file: File) => {
    const row = draftBase();
    if (row === null) {
      return;
    }

    if (file.size > MAX_IMPORT_BYTES) {
      setRefusal({
        outcome: 'refused',
        message: 'content.import.notFromInstructions',
        problems: [],
        body: null,
      });
      return;
    }

    setWorking(true);
    try {
      const [text, rows] = await Promise.all([
        file.text(),
        queryClient.fetchQuery({ ...siteAddressesQuery(), staleTime: 0 }),
      ]);

      const answer = await readClaudeFile(text, {
        content: { id: row.id, kind },
        rowVersion: row.rowVersion,
        body: readBody(row.body),
        visibility: row.visibility,
        locales,
        blocks: offeredBlocks(registry.blocks, holds, bootstrap?.registries.blocks ?? [], row.visibility),
        template,
        dirty: isDirty(),
        registered: registry.blocks,
        addresses: knownAddresses(rows),
      });

      if (answer.outcome === 'refused') {
        setRefusal(answer);
      } else if (answer.startedOver) {
        setStartedOver(answer);
      } else {
        await save(answer);
      }
    } catch {
      notice({ tone: 'error', title: t('content.import.failed') });
    } finally {
      setWorking(false);
    }
  };

  return (
    <>
      <Button
        type="button"
        variant="ghost"
        disabled={disabled || working}
        title={t('content.import.hint')}
        onClick={() => input.current?.click()}
      >
        <Upload className="size-4" aria-hidden />
        {t('content.import.label')}
      </Button>
      <input
        ref={input}
        type="file"
        accept=".json,application/json"
        aria-label={t('content.import.label')}
        className="hidden"
        tabIndex={-1}
        onChange={(event) => {
          const file = event.target.files?.[0];
          // The same file chosen twice in a row fires no change unless the value is cleared, and "Claude mended it and I
          // chose it again" is exactly what somebody does here.
          event.target.value = '';
          if (file !== undefined) {
            void read(file);
          }
        }}
      />

      {refusal === null ? null : <RefusalDialog refusal={refusal} onClose={() => setRefusal(null)} />}

      {startedOver === null ? null : (
        <Dialog
          open
          onOpenChange={(open) => {
            if (!open) {
              setStartedOver(null);
            }
          }}
          title={t('content.import.startedOverTitle')}
          description={t('content.import.startedOver')}
          footer={
            <>
              <Button type="button" variant="outline" onClick={() => setStartedOver(null)}>
                {t('common.cancel')}
              </Button>
              <Button
                type="button"
                variant="destructive"
                onClick={() => {
                  const file = startedOver;
                  setStartedOver(null);
                  void save(file);
                }}
              >
                {t('content.import.startedOverConfirm')}
              </Button>
            </>
          }
        />
      )}
    </>
  );
}

/**
 * What an import left for the person (note §3.5): «3 pictures to choose», beside each block, the way `PublishProblems`
 * says what stands before publication. It empties itself as they are done.
 */
export function ImportLeftovers({ body, pending }: { body: Body; pending: readonly ImportPending[] }) {
  const { t } = useTranslation();
  const describe = useBodyPathDescription(body);
  const open = pendingNow(body, pending);

  if (open.length === 0) {
    return null;
  }

  const count = (kind: ImportPending['kind']) => open.filter(({ entry }) => entry.kind === kind).length;

  return (
    <Notice
      tone="warning"
      title={(['picture', 'value', 'link'] as const)
        .filter((kind) => count(kind) > 0)
        .map((kind) => t(`content.import.pending.${kind}Count`, { count: count(kind) }))
        .join(' · ')}
      description={
        <ul className="flex flex-col gap-1 text-sm">
          {open.map(({ path, entry }) => (
            <li key={`${entry.owner}-${entry.field}-${entry.address}`} className="flex flex-col">
              <span className="font-medium">{describe(path)}</span>
              <span className="opacity-90">
                {entry.kind === 'link' && entry.address !== ''
                  ? t('content.import.pending.linkUnknown', { address: entry.address })
                  : t(`content.import.pending.${entry.kind}`)}
              </span>
            </li>
          ))}
        </ul>
      }
    />
  );
}
