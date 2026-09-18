import { Button, Input, Label } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useBlocker } from '@tanstack/react-router';
import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { frameAddress, readBody, type Body } from '../../blocks';
import type { Department } from '../../shared/api/bootstrap';
import { ApiError } from '../../shared/api/problem';
import { SchemaForm, type ChoiceOption } from '../../shared/forms';
import { useMoment } from '../../shared/i18n/useMoment';
import type { MediaLibraryQuery } from '../../shared/ui';
import { ConfirmDialog, PageActions } from '../../shared/ui';

import { AddressPreview } from './AddressPreview';
import { BodyEditor } from './BodyEditor';
import { emptyContent, toFormValues, type PublishRequest } from './mutations';
import type { PublishedView } from './PreviewFrame';
import { PublishProblems } from './publishProblems';
import {
  contentQuery,
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
import { useAutosave, type SaveOutcome } from './useAutosave';

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
  uploadMedia?: ((file: File) => Promise<number>) | undefined;
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
  /** `autosave` marks a save the editor made by itself: no toast, and audited without the body. */
  onSave: (values: ContentFormValues, body: Body, options?: { autosave?: boolean }) => Promise<unknown>;
  /** Null for a row that does not exist yet: there is nothing to publish until it is saved once. */
  onPublish: ((request: PublishRequest) => void) | null;
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
   * that marking a page ready never sends for approval a draft that was not saved.
   */
  review?: (flush: () => Promise<boolean>) => ReactNode;
}) {
  const { t } = useTranslation();
  const moment = useMoment();

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

  // What the form of a document offers beyond its own row.
  const documentChoices: DocumentChoices = { successors };

  // What the publish dialog is told, kept across its openings: a changelog half written and a
  // dialog closed by mistake should not be a changelog written twice.
  const [publishRequest, setPublishRequest] = useState<PublishRequest>({ changelog: '' });

  // The template this page was made from: its rules and its differences are the editor's to draw.
  const template = useQuery({
    ...contentQuery(content?.templateId ?? 0),
    enabled: typeof content?.templateId === 'number',
  });

  // ⚠️ The version of the row is the **row's**, read at the moment of saving, and not a field of the
  // form any more. It used to be, and the form was remounted at every save to refresh it — which a
  // save made by itself would do under somebody's fingers, taking the cursor out of the title. A
  // row that does not exist yet has the version a new row carries, from the form.
  const withVersion = (values: ContentFormValues): ContentFormValues => ({
    ...values,
    rowVersion: content?.rowVersion ?? values.rowVersion,
  });

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
        await onSave(withVersion(metadata), body, { autosave: true });
        return 'saved';
      } catch (error) {
        // 409 is somebody else's save, and the one refusal that must not be retried.
        return error instanceof ApiError && error.status === 409 ? 'conflict' : 'failed';
      }
    },
  });

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
        id={METADATA_FORM}
        actionsElsewhere
        schema={contentMetadataSchema(kind, categories, documentChoices, pageChoices)}
        defaults={
          content === null
            ? emptyContent(department, locales, kind, startsAsTemplate)
            : toFormValues(content, locales)
        }
        locales={locales}
        labels="content"
        division={division}
        mediaLibrary={mediaLibrary}
        uploadMedia={uploadMedia}
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

          try {
            await onSave(withVersion(values), body);
          } catch (error) {
            if (content === null) {
              autosave.settle('');
            }
            throw error;
          }

          // Stored by a press, so the draft as it stands is not dirty any more.
          autosave.settle(stored);
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
    </>
  );

  return (
    <BodyEditor
      initial={body}
      onChange={setBody}
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
      pageProperties={pageProperties}
      toolbar={(tools) => (
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
                onConfirm={() => {
                  void (async () => {
                    if (autosave.dirty && !(await autosave.flush())) {
                      return;
                    }
                    onPublish(publishRequest);
                  })();
                }}
              >
                <div className="flex flex-col gap-1">
                  <Label htmlFor="publish-changelog">{t('content.editor.publishDialog.changelog')}</Label>
                  <Input
                    id="publish-changelog"
                    maxLength={512}
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
          {review?.(async () => !autosave.dirty || (await autosave.flush()))}

          <PublishProblems body={body} problems={publishProblems} />

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
