import { Button, Label, Textarea } from '@ivao/atmosphere-react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useBlocker } from '@tanstack/react-router';
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { frameAddress, readBody, type Body } from '../../blocks';
import type { Department } from '../../shared/api/bootstrap';
import { ApiError } from '../../shared/api/problem';
import { SchemaForm, type ChoiceOption } from '../../shared/forms';
import { useLocalized } from '../../shared/i18n/useLocalized';
import { useMoment } from '../../shared/i18n/useMoment';
import type { MediaLibraryQuery } from '../../shared/ui';
import { ConfirmDialog, PageActions } from '../../shared/ui';

import { AddressPreview } from './AddressPreview';
import { BodyEditor } from './BodyEditor';
import { CHANGELOG_MAX, changeSummary } from './changeSummary';
import { ClaudeDownload } from './ClaudeDownload';
import {
  forgetImported,
  rememberImported,
  versionLine,
  type ImportPending,
  type ImportReady,
} from './claudeFile';
import { ClaudeImport, ImportLeftovers } from './ClaudeImport';
import { emptyContent, spreadLocalized, toFormValues, type PublishRequest } from './mutations';
import type { PublishedView } from './PreviewFrame';
import { PublishProblems } from './publishProblems';
import {
  contentQuery,
  contentReviewQuery,
  publishedContentQuery,
  type ContentDetailDto,
  type ContentKind,
  type ContentPublishProblemsDto,
} from './queries';
import {
  contentMetadataSchema,
  type ContentFormValues,
  type DocumentChoices,
  type PageChoices,
} from './schema';
import { templateRules } from './templateRules';
import { useAutosave, type SaveOutcome } from './useAutosave';
import { VersionHistory } from './VersionHistory';

/**
 * The list editor of M0 (design M0 §7.7). Metadata at the top, what the page is made of on the
 * left, whatever is selected on the right, and a preview that is the very same renderer a visitor
 * gets — because "what will this look like" and "what does this look like" must not be two pieces
 * of code that can disagree.
 *
 * The draft lives in state and is saved whole — by a press on "save draft", and since G15 by itself
 * after a pause and on the way out (`useAutosave`). Publishing is a separate action on the *saved*
 * row: what is on screen is stored first, then published, so a page never says something nobody
 * saved.
 *
 * Since T6b (M2) the composing itself is `BodyEditor`, which a tour's briefing uses too; what is left
 * here is what a row of `cms_contents` is besides its body.
 */
/**
 * The metadata form's own `id`, so that `Save draft` can live in the toolbar at the top while the
 * form itself lives in the panel on the right (`SchemaForm`'s `id` and `actionsElsewhere`).
 */
const METADATA_FORM = 'content-metadata';

/**
 * The last import of this editor, as the way back and forth over it needs it (`useBodyHistory` undoes the body and
 * nothing else; note 2026-10-30-una-pagina-scritta-con-claude §3.3: **Annulla** takes the import away, and the import
 * wrote the title and the summary too). The two bodies are the two sides of it, told apart by identity: what the history
 * hands back is the very object it was given.
 */
interface ImportRun {
  readonly before: Body;
  readonly after: Body;
  readonly fieldsBefore: Pick<ContentFormValues, 'title' | 'summary'>;
  readonly fieldsAfter: Pick<ContentFormValues, 'title' | 'summary'>;
  readonly pending: readonly ImportPending[];
  /** Which side of it the editor is on. */
  applied: boolean;
}

/**
 * The version of the row a draft is based on: the one it was opened at, moved on by `adopt` with
 * every row this editor's own writes answer. Held in a ref, because it is read when a save starts —
 * after the one before it, which may have moved it — and never drawn.
 */
function useDraftBase(opened: ContentDetailDto | null) {
  const base = useRef(opened?.rowVersion ?? null);
  // The row itself, as the server last answered it to this editor: what the instructions for Claude show beside the
  // version they name, so that the body and the version in their header are always of the same save.
  const row = useRef(opened);

  const adopt = useCallback((stored: ContentDetailDto) => {
    base.current = stored.rowVersion;
    row.current = stored;
  }, []);

  const withVersion = useCallback(
    (values: ContentFormValues): ContentFormValues => ({
      ...values,
      rowVersion: base.current ?? values.rowVersion,
    }),
    [],
  );

  // Read when the instructions for Claude are composed, after the draft was stored: the row their header names, whose
  // version the next save is written over (PR 3.0a, note 2026-10-30-una-pagina-scritta-con-claude §4).
  const current = useCallback(() => row.current, []);

  return { adopt, withVersion, current };
}

export function ContentEditor({
  content,
  kind,
  startsAsTemplate = false,
  categories,
  successors = [],
  pageChoices,
  department,
  locales,
  division,
  mediaLibrary,
  uploadMedia,
  createCollection,
  canManageTemplates,
  holds,
  onSave,
  onPublish,
  onDelete,
  publishProblems,
  busy,
  locked = false,
  review,
}: {
  content: ContentDetailDto | null;
  /** Which kind this row is. Fixed by the list it was opened from, never a field on the form. */
  kind: ContentKind;
  /**
   * Whether a row that does not exist yet is going to be a template. An existing one says so itself;
   * this is only how the templates screen tells the editor what it is about to make, so that the
   * section properties offer `key`, `required`, `locked` and `allowedBlocks` from the first save
   * rather than after a reload.
   */
  startsAsTemplate?: boolean;
  /** The shelves of this department, already resolved into the language on screen. */
  categories: readonly ChoiceOption[];
  /** The published documents this one may say it was replaced by, already labelled. */
  successors?: readonly ChoiceOption[];
  /** Where a page may be put, and whether at the top (note 2026-09-13-contenuti-centralizzati). */
  pageChoices?: PageChoices;
  department: Department;
  locales: readonly string[];
  /** The two facts the `seo` field needs: which language is the fallback, and where the division is. */
  division: { defaultLocale: string; timezone: string };
  /** The library the picture of `seo` is chosen from — this department's. */
  mediaLibrary: MediaLibraryQuery;
  /**
   * Uploads a file into that same library and answers its identifier, for the picker to offer
   * "upload" beside "choose" (Carmine, 11 September 2026: "yes, if it lands in the library of the
   * department the document belongs to"). The same call the library screen makes.
   */
  uploadMedia?: ((file: File, category?: string) => Promise<number>) | undefined;
  /**
   * What the field of the collections draws to make a new one and tick it (#364): the screen's,
   * since it knows the department and the kind. Absent, the field offers the existing ones only.
   */
  createCollection?: ((choose: (key: string) => void) => React.ReactNode) | undefined;
  /**
   * Whether this member may change a template of that department. Asked as a question rather than
   * handed the bootstrap, because the answer is about the template's department and not this page's
   * — and which department that is, only the loaded template knows.
   */
  canManageTemplates: (department: Department) => boolean;
  /**
   * Whether this member holds a permission **in the department this row belongs to**. Asked as a
   * question for the same reason as the one above: what the editor needs is an answer, not a
   * bootstrap to interrogate. One block asks it today — the interactive one, which the palette
   * leaves out of the list for whoever may not add code.
   */
  holds: (permission: string) => boolean;
  /**
   * `autosave` marks a save the editor made by itself: no toast, and audited without the body.
   * Answers the row as stored, whose version the next save is written over.
   */
  onSave: (
    values: ContentFormValues,
    body: Body,
    options?: { autosave?: boolean },
  ) => Promise<ContentDetailDto>;
  /**
   * Null for a row that does not exist yet: there is nothing to publish until it is saved once.
   * Answers the row as published, and rejects when it was not — the screen has said why.
   */
  onPublish: ((request: PublishRequest) => Promise<ContentDetailDto>) | null;
  onDelete: (() => void) | null;
  /**
   * What the server says stands between this row and the public, asked before anybody presses
   * publish rather than after being refused (`publishProblemsQuery`).
   */
  publishProblems: ContentPublishProblemsDto | undefined;
  busy: boolean;
  /**
   * A page waiting for approval (G19): read, not written. Nothing is saved by itself and nothing can
   * be pressed that would change it; the server refuses a write anyway (`errors.content.review.inReview`).
   */
  locked?: boolean;
  /**
   * The review of this page, drawn under the toolbar. Handed the way to store what is on screen, so
   * that marking a page ready never sends for approval a draft that was not saved, and the way to
   * say what the review left: the row moves on with it, and so does the draft (`adopt`).
   */
  review?: (flush: () => Promise<boolean>, adopt: (row: ContentDetailDto) => void) => ReactNode;
}) {
  const { t } = useTranslation();
  const moment = useMoment();
  const read = useLocalized();
  const queryClient = useQueryClient();

  // The body as the editor last said it is: what a save sends. The editor owns the way back.
  const [body, setBody] = useState<Body>(() => readBody(content?.body));

  // The draft, or what visitors read now. Asked for only when asked to be shown; a row that does
  // not exist yet has nothing published to show.
  const [comparing, setComparing] = useState(false);
  const publishedQuery = useQuery({
    ...publishedContentQuery(kind, content?.slug ?? '', content?.path ?? ''),
    enabled: content !== null && comparing,
    retry: false,
  });
  const published: PublishedView | undefined =
    content === null
      ? undefined
      : publishedQuery.data !== undefined
        ? { state: 'ready', body: readBody(publishedQuery.data.body) }
        : publishedQuery.isError
          ? { state: 'none' }
          : { state: 'loading' };

  // The row's own fields as the form last had them valid — what a save made by itself sends. The
  // form still owns the fields; this is its shadow, kept because a save after a pause cannot press
  // a submit button. A value the schema refuses never arrives here, so an address emptied halfway
  // through travels as the last one that was whole (`SchemaForm.onChange`).
  const [metadata, setMetadata] = useState<ContentFormValues>(() =>
    content === null
      ? emptyContent(department, locales, kind, startsAsTemplate)
      : toFormValues(content, locales),
  );

  // The form as it starts again after an import, and a new form for it: `SchemaForm` reads its defaults once, and these
  // are not the row the screen holds. Nothing else remounts it, so the cursor stays where it is in every other save.
  const [formStart, setFormStart] = useState<{ generation: number; values: ContentFormValues } | null>(null);
  const takeFields = (values: ContentFormValues) => {
    setMetadata(values);
    setFormStart((current) => ({ generation: (current?.generation ?? 0) + 1, values }));
  };

  // What the last import left to do, and the way back over it.
  const [leftovers, setLeftovers] = useState<readonly ImportPending[]>([]);
  const importRun = useRef<ImportRun | null>(null);

  // What the form of a document offers beyond its own row.
  const documentChoices: DocumentChoices = { successors };

  // What the publish dialog is told, kept across its openings: a changelog half written and a
  // dialog closed by mistake should not be a changelog written twice.
  const [publishRequest, setPublishRequest] = useState<PublishRequest>({ changelog: '' });

  // The line the dialog last wrote by itself: replaced by a newer one at the next opening, unless the person has
  // written over it — then it is theirs, and stays.
  const suggested = useRef('');

  // The template this page was made from: its rules and its differences are the editor's to draw.
  const template = useQuery({
    ...contentQuery(content?.templateId ?? 0),
    enabled: typeof content?.templateId === 'number',
  });

  // ⚠️ The version a save is written over is the one **this draft is based on**: the row as it was
  // opened, moved on only by what this editor itself wrote — a save, a publication, a step of the
  // review (`adopt`). Not the version of the latest fetch: the detail is asked again whenever the
  // tab comes back into focus, and a draft sent with somebody else's version is a draft saved over
  // their save without a 409 (issue #330; rule 5 of `decisions/2026-09-11-l-editor-che-risponde.md`).
  //
  // Not a field of the form either. It used to be, and the form was remounted at every save to
  // refresh it — which a save made by itself would do under somebody's fingers, taking the cursor
  // out of the title. A row that does not exist yet has the version a new row carries, from the
  // form.
  const { adopt, withVersion, current: draftBase } = useDraftBase(content);

  // What the template still says about this page, for the instructions for Claude; the editor reads its own.
  const rules = useMemo(
    () => templateRules(template.data === undefined ? null : readBody(template.data.body)),
    [template.data],
  );

  // The draft, as one string: what the autosave compares, so that a change undone before the pause
  // ends is not a save.
  const snapshot = useMemo(() => JSON.stringify({ metadata, body }), [metadata, body]);

  const autosave = useAutosave({
    // Never before the first press on "save draft": a row that does not exist is not saved by
    // itself, or every "new page" somebody opened and left would be a page.
    enabled: content !== null && !locked,
    snapshot,
    busy,
    save: async (): Promise<SaveOutcome> => {
      try {
        adopt(await onSave(withVersion(metadata), body, { autosave: true }));
        return 'saved';
      } catch (error) {
        // 409 is somebody else's save, and the one refusal that must not be retried.
        return error instanceof ApiError && error.status === 409 ? 'conflict' : 'failed';
      }
    },
  });

  // What changed since the version online, said in the field before anybody writes it (issue #352). Asked of the
  // server — the comparison an approver reads — on the draft as stored, so it is stored first.
  const suggestChangelog = async (id: number) => {
    await autosave.flush();
    try {
      const changes = await queryClient.fetchQuery({ ...contentReviewQuery(id), staleTime: 0 });
      const row = draftBase();
      const line =
        row === null ? changeSummary(changes, t, read) : versionLine(row, changeSummary(changes, t, read), t);
      setPublishRequest((current) => {
        if (current.changelog !== '' && current.changelog !== suggested.current) {
          return current;
        }
        suggested.current = line;
        return { ...current, changelog: line };
      });
    } catch {
      // No suggestion: the field stays as it is, and the person writes the line.
    }
  };

  // The body as the editor says it is. Undoing back over an import takes its title and its summary with it, and doing it
  // again puts them back; anything else leaves the fields to whoever is writing them.
  const changeBody = (next: Body) => {
    setBody(next);

    const run = importRun.current;
    if (run === null || content === null) {
      return;
    }

    if (run.applied && next === run.before) {
      run.applied = false;
      takeFields({ ...metadata, ...run.fieldsBefore });
      setLeftovers([]);
      forgetImported(content.id);
    } else if (!run.applied && next === run.after) {
      run.applied = true;
      takeFields({ ...metadata, ...run.fieldsAfter });
      setLeftovers(run.pending);
      const row = draftBase();
      if (row !== null) {
        rememberImported(row);
      }
    }
  };

  // A file that passed its checks (`ClaudeImport`): saved first, as a press on «save draft» is, over the version it
  // worked on; only what the server took goes on screen, so a refusal leaves the draft as it was.
  const importFile = async (file: ImportReady, replace: (next: Body) => void) => {
    const fieldsAfter = {
      title: spreadLocalized(file.title, locales),
      summary: spreadLocalized(file.summary, locales),
    };
    const values = { ...metadata, ...fieldsAfter };

    const stored = await autosave.exclusive(async () => {
      const row = await onSave(withVersion(values), file.body);
      adopt(row);
      return row;
    });

    importRun.current = {
      before: body,
      after: file.body,
      fieldsBefore: { title: metadata.title, summary: metadata.summary },
      fieldsAfter,
      pending: file.pending,
      applied: true,
    };

    takeFields(values);
    replace(file.body);
    setLeftovers(file.pending);
    rememberImported(stored);
    autosave.settle(JSON.stringify({ metadata: values, body: file.body }));
  };

  // On the way out: stored, or asked. The in-application road is the router's blocker; the tab
  // being closed is the browser's own question, which is all a page is allowed to do there.
  const leaving = useRef({ autosave, existing: content !== null });
  useEffect(() => {
    leaving.current = { autosave, existing: content !== null };
  });

  useBlocker({
    shouldBlockFn: async () => {
      const { autosave: draft, existing } = leaving.current;
      if (!draft.dirty) {
        return false;
      }

      if (existing && !draft.stopped && (await draft.flush())) {
        return false;
      }

      // A new row, or a save that could not be made: the person decides, in the browser's own
      // dialog — the one thing a `beforeunload` can do too, so the two roads out ask the same way.
      return !window.confirm(t('content.editor.autosave.leave'));
    },
    enableBeforeUnload: () => leaving.current.autosave.dirty,
  });

  // What the line under the toolbar says about the draft. One sentence, in order of what matters:
  // a stop nobody can miss, then what is happening, then what is pending, then when it was stored.
  const draftStatus = autosave.stopped
    ? t('content.editor.autosave.stopped')
    : autosave.saving
      ? t('content.editor.autosave.saving')
      : autosave.failed
        ? t('content.editor.autosave.failed')
        : autosave.dirty
          ? t(content === null ? 'content.editor.autosave.unsavedNew' : 'content.editor.autosave.unsaved')
          : autosave.savedAt === null
            ? null
            : t('content.editor.autosave.saved', {
                time: moment(autosave.savedAt.toISOString(), { date: false, timeZone: division.timezone }),
              });

  const pageProperties = (
    <>
      <SchemaForm
        key={formStart?.generation ?? 0}
        id={METADATA_FORM}
        actionsElsewhere
        schema={contentMetadataSchema(kind, categories, documentChoices, pageChoices)}
        defaults={
          formStart?.values ??
          (content === null
            ? emptyContent(department, locales, kind, startsAsTemplate)
            : toFormValues(content, locales))
        }
        locales={locales}
        labels="content"
        division={division}
        mediaLibrary={mediaLibrary}
        uploadMedia={uploadMedia}
        {...(createCollection === undefined
          ? {}
          : { createChoice: (_field: string, choose: (value: string) => void) => createCollection(choose) })}
        // The shadow the autosave sends, kept in step with the fields as they are written.
        onChange={setMetadata}
        onSubmit={async (values) => {
          setMetadata(values);
          const stored = JSON.stringify({ metadata: values, body });

          // ⚠️ A new row leaves this screen for its own address from **inside** `onSave`, and the
          // blocker on the way out asked "leave? the changes will be lost" about a draft that
          // was being stored that very moment (found while making the first document of G14).
          // So a new row is settled before the store, and unsettled again if the store fails.
          if (content === null) {
            autosave.settle(stored);
          }

          // After the save in flight, if there is one: sent at once it would carry the version that
          // save is about to move (issue #332, item 8). Enter in a one-line field submits however
          // disabled the button is, so the button is not what keeps the two apart.
          await autosave.exclusive(async () => {
            try {
              adopt(await onSave(withVersion(values), body));
            } catch (error) {
              if (content === null) {
                autosave.settle('');
              }
              throw error;
            }

            // Stored by a press, so the draft as it stands is not dirty any more.
            autosave.settle(stored);
          });
        }}
        submitLabel={t('content.editor.saveDraft')}
      />
      {/* The address the fields above make, and whether it may be had. Not for a template, which
          has no address on the site. */}
      {startsAsTemplate || content?.isTemplate === true ? null : (
        <AddressPreview
          kind={kind}
          department={metadata.ownerDepartment}
          slug={metadata.slug}
          parentId={
            metadata.parentId === undefined || metadata.parentId === '' ? null : Number(metadata.parentId)
          }
          id={content?.id ?? null}
        />
      )}
      {content === null || content.isTemplate ? null : (
        <VersionHistory id={content.id} timezone={division.timezone} />
      )}
    </>
  );

  return (
    <BodyEditor
      initial={body}
      onChange={changeBody}
      dashboard={kind === 'Dashboard'}
      isTemplate={content?.isTemplate ?? startsAsTemplate}
      template={
        template.data === undefined
          ? null
          : {
              body: readBody(template.data.body),
              title: template.data.title,
              department: template.data.ownerDepartment,
              canManage: canManageTemplates(template.data.ownerDepartment),
            }
      }
      // A row that has never been saved has no frame to show: there is nothing on the server to
      // build one from. Once it has, it is the **draft** of this row, which only somebody who may
      // edit it is served.
      frameUrl={(blockId, locale) =>
        content === null ? null : frameAddress(content.id, 'draft', blockId, locale)
      }
      published={published}
      comparing={comparing}
      onCompare={setComparing}
      locked={locked}
      locales={locales}
      division={division}
      mediaLibrary={mediaLibrary}
      uploadMedia={uploadMedia}
      holds={holds}
      // A template has no readers of its own: the page made from it is the one held to its visibility.
      visibility={(content?.isTemplate ?? startsAsTemplate) ? undefined : metadata.visibility}
      pageProperties={pageProperties}
      toolbar={(tools, editor) => (
        // ⚠️ At the top and sticky, and it used to sit at the **bottom of the metadata form** — which
        // measured 1182 pixels in a window of 950, so the page being composed and the buttons that
        // save it were both below the fold. Measured, not guessed (road A1 of
        // `decisions/2026-09-09-comporre-una-pagina-guardandola.md`).
        //
        // `Save draft` submits by `form=`, which is how HTML has always let a button live outside the
        // form it belongs to: the form is in the panel on the right, where the page's own properties
        // are edited. ⚠️ And since 11 September 2026 on the frame's own line, beside the title, rather
        // than on a line of its own under it (Carmine: stop wasting the space at the top). `PageActions`
        // draws it up there while its state stays here, and that line is the sticky one now.
        <PageActions>
          <div className="flex flex-wrap items-center gap-2">
            <Button type="submit" form={METADATA_FORM} disabled={busy || locked}>
              {t('content.editor.saveDraft')}
            </Button>

            {tools}

            <ClaudeDownload
              content={content}
              kind={kind}
              locales={locales}
              template={rules}
              holds={holds}
              flush={autosave.flush}
              draftBase={draftBase}
              // Off while the template is on its way: its rules are part of what the instructions give.
              disabled={
                busy ||
                locked ||
                autosave.stopped ||
                (typeof content?.templateId === 'number' && template.data === undefined)
              }
            />

            <ClaudeImport
              content={content}
              kind={kind}
              locales={locales}
              template={rules}
              holds={holds}
              isDirty={() => leaving.current.autosave.dirty}
              draftBase={draftBase}
              apply={(file) => importFile(file, editor.replace)}
              // Only in the draft: a page waiting for approval is read, not written. Off with the template on its way,
              // as the download is: its rules are part of what the file is checked against.
              disabled={
                busy ||
                locked ||
                autosave.stopped ||
                (typeof content?.templateId === 'number' && template.data === undefined)
              }
            />

            {onPublish === null ? null : (
              // A question on the way: what changed, for the staff. What is on screen is stored first, then
              // published: one press, and never a page that says something nobody saved. A store
              // that fails leaves the draft where it is, and the line under the toolbar says why.
              <ConfirmDialog
                triggerText={t('content.editor.publish')}
                triggerVariant="secondary"
                title={t('content.editor.publishDialog.title')}
                description={t('content.editor.publishDialog.description')}
                confirmText={t('content.editor.publishDialog.confirm')}
                confirmVariant="primary"
                disabled={busy || autosave.stopped}
                onOpenChange={(open) => {
                  if (open && content !== null) {
                    void suggestChangelog(content.id);
                  }
                }}
                onConfirm={() => {
                  void (async () => {
                    // The draft first — after any save already in flight — and the publication in
                    // its turn, so that no save of this editor runs beside it.
                    if (!(await autosave.flush())) {
                      return;
                    }
                    try {
                      await autosave.exclusive(async () => adopt(await onPublish(publishRequest)));
                      // Published with its line: the next publication has a line of its own.
                      suggested.current = '';
                      setPublishRequest({ changelog: '' });
                    } catch {
                      // Refused: the screen has said so, and the draft is where it was.
                    }
                  })();
                }}
              >
                <div className="flex flex-col gap-1">
                  <Label htmlFor="publish-changelog">{t('content.editor.publishDialog.changelog')}</Label>
                  <Textarea
                    id="publish-changelog"
                    rows={3}
                    maxLength={CHANGELOG_MAX}
                    value={publishRequest.changelog}
                    onChange={(event) =>
                      setPublishRequest({ ...publishRequest, changelog: event.target.value })
                    }
                  />
                </div>
              </ConfirmDialog>
            )}

            {onDelete === null ? null : (
              <ConfirmDialog
                triggerText={t('common.delete')}
                title={t('content.delete.title')}
                description={t('content.delete.description')}
                confirmText={t('common.delete')}
                disabled={busy}
                onConfirm={onDelete}
              />
            )}
          </div>
        </PageActions>
      )}
      header={
        <>
          {review?.(autosave.flush, adopt)}

          <PublishProblems body={body} problems={publishProblems} />

          <ImportLeftovers body={body} pending={leftovers} />

          {draftStatus === null ? null : (
            // Named, because the drag and drop context draws a live region of its own for its
            // announcements, and "the status" would otherwise be two things on this screen.
            <p
              role="status"
              aria-label={t('content.editor.autosave.title')}
              className="text-muted-foreground text-sm"
            >
              {draftStatus}
            </p>
          )}
        </>
      }
    />
  );
}
