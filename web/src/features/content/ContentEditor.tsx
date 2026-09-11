import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { ChevronLeft, Eye, List, Send, Undo2 } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { registry } from '../../app/registry';
import { columnsOf, readBody, PickingContext, type Body } from '../../blocks';
import type { Department } from '../../shared/api/bootstrap';
import { SchemaForm, writtenValues, type ChoiceOption } from '../../shared/forms';
import { useLocalized } from '../../shared/i18n/useLocalized';
import type { MediaLibraryQuery } from '../../shared/ui';
import { ConfirmDialog, PageActions, SectionHeader } from '../../shared/ui';

import { BlockPalette } from './BlockPalette';
import { BlockProperties, SectionProperties } from './BlockProperties';
import {
  addBlock,
  addSection,
  clampColumns,
  defaultProps,
  duplicateBlock,
  findBlock,
  findSection,
  moveBlock,
  moveSection,
  removeBlock,
  removeSection,
  reorderBlocks,
  reorderSections,
  updateBlock,
  updateSection,
} from './body';
import { emptyContent, toFormValues } from './mutations';
import { PreviewFrame } from './PreviewFrame';
import { PublishProblems } from './publishProblems';
import {
  contentQuery,
  type ContentDetailDto,
  type ContentKind,
  type ContentPublishProblemsDto,
} from './queries';
import { contentMetadataSchema, type ContentFormValues } from './schema';
import { SectionTree, type Selection } from './SectionTree';
import { useBodyHistory } from './useBodyHistory';
import { applyDifference, templateDiff } from './templateDiff';
import { LockedByTemplate, TemplateDifferences } from './TemplatePanel';
import { NO_RULES, ruleFor, templateRules } from './templateRules';

/**
 * The list editor of M0 (design M0 §7.7). Metadata at the top, what the page is made of on the
 * left, whatever is selected on the right, and a preview that is the very same renderer a visitor
 * gets — because "what will this look like" and "what does this look like" must not be two pieces
 * of code that can disagree.
 *
 * The draft lives in state and is saved whole. Publishing is a separate action on the *saved* row,
 * and is refused while there are unsaved changes: publishing what is on screen rather than what is
 * stored would be a page that says something nobody saved.
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
  department,
  locales,
  division,
  mediaLibrary,
  canManageTemplates,
  onSave,
  onPublish,
  onDelete,
  publishProblems,
  busy,
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
  department: Department;
  locales: readonly string[];
  /** The two facts the `seo` field needs: which language is the fallback, and where the division is. */
  division: { defaultLocale: string; timezone: string };
  /** The library the picture of `seo` is chosen from — this department's. */
  mediaLibrary: MediaLibraryQuery;
  /**
   * Whether this member may change a template of that department. Asked as a question rather than
   * handed the bootstrap, because the answer is about the template's department and not this page's
   * — and which department that is, only the loaded template knows.
   */
  canManageTemplates: (department: Department) => boolean;
  onSave: (values: ContentFormValues, body: Body) => Promise<unknown>;
  /** Null for a row that does not exist yet: there is nothing to publish until it is saved once. */
  onPublish: (() => void) | null;
  onDelete: (() => void) | null;
  /**
   * What the server says stands between this row and the public, asked before anybody presses
   * publish rather than after being refused (`publishProblemsQuery`).
   */
  publishProblems: ContentPublishProblemsDto | undefined;
  busy: boolean;
}) {
  const { t } = useTranslation();
  const read = useLocalized();

  // The body, and the way back from the last thing that happened to it. A section moved by mistake
  // was one of the frictions the hand copy of `/about` recorded (HANDOFF §27).
  const history = useBodyHistory(readBody(content?.body));
  const body = history.body;

  const [selection, setSelection] = useState<Selection | null>(null);
  // The column an empty "add here" on the page chose. Only meaningful while its section is the one
  // selected: select anything else and it is simply not read (see `targetColumn`), which is why no
  // effect has to clear it.
  const [chosenColumn, setChosenColumn] = useState<{ section: string; column: number } | null>(null);
  // ⚠️ The page, not the outline, is what the middle column shows to begin with (Carmine, 10
  // September 2026: "the visual editor in the middle"). Composing by clicking the page itself was
  // decided on 9 September and then reached only by pressing a button, which made the road that was
  // chosen the one nobody took. The outline is one press away and is still the keyboard road.
  const [preview, setPreview] = useState(true);
  const [unsaved, setUnsaved] = useState(false);

  // What the template still says about this page: which sections are fixed, which are locked, and
  // which blocks may go in them. The page itself does not carry any of it (see `templateRules`).
  const template = useQuery({
    ...contentQuery(content?.templateId ?? 0),
    enabled: typeof content?.templateId === 'number',
  });

  const templateBody = template.data === undefined ? null : readBody(template.data.body);
  const rules = templateBody === null ? NO_RULES : templateRules(templateBody);

  // And what it says that this page does not. Nothing here changes anything by itself: the list is
  // read out, and one line at a time is acted on (design M1 §9.1).
  const differences = templateDiff(body, templateBody);

  const change = (next: Body) => {
    history.change(next);
    setUnsaved(true);
  };

  const section = selection?.kind === 'section' ? findSection(body, selection.id) : undefined;
  const block = selection?.kind === 'block' ? findBlock(body, selection.id) : undefined;

  // Adding a block, written once: the palette on the left and the one inside the outline do the
  // very same thing, and a block added from either has to start out identical -- same blank
  // properties, same render mode. Two copies of this would be two ways of being born.
  const addBlockTo = (sectionId: string, type: string, column = 0) => {
    const registration = registry.blocks.find((candidate) => candidate.type === type);
    if (registration === undefined) {
      return;
    }

    const added = addBlock(
      body,
      sectionId,
      type,
      // The blank properties, minus the optional ones nobody has written into: a block
      // added and never opened must not carry an empty translated value, which
      // publication would read as a page translated into one language only.
      writtenValues(registration.schema, defaultProps(registration.schema, locales)),
      // A data block starts live: capturing is a decision somebody makes, and one that
      // only means anything once the page is published.
      registration.kind === 'Data' ? 'live' : null,
      column,
    );

    change(added.body);
    setSelection({ kind: 'block', id: added.id });
  };

  // Which section the palette on the left adds to. A block selected means the section it is in:
  // clicking a paragraph and then `Image` should put the image where you are looking, not ask you
  // to go and select the section first.
  const targetSection = section ?? block?.section;

  // And which column of it (11 September 2026). A block selected means its own column — the image
  // goes next to the paragraph you clicked, not to the top of the first column. An empty column
  // clicked on the page means that column. Anything else means the first, which is where a stacked
  // section keeps its blocks anyway.
  const targetColumn =
    block !== undefined
      ? (block.block.column ?? 0)
      : chosenColumn !== null && chosenColumn.section === targetSection?.id
        ? chosenColumn.column
        : 0;

  const targetRule = ruleFor(rules, targetSection?.key);

  const paletteRule =
    // A locked section is one whose blocks a page may edit and whose shape it may not, so nothing
    // can be added to it. Said with the same words as a block the template forbids, because to
    // whoever is writing it is the same sentence: not here.
    targetRule.locked ? { ...targetRule, allowedBlocks: [] } : targetRule;

  const paletteTarget =
    targetSection === undefined
      ? null
      : {
          id: targetSection.id,
          name: [
            read(targetSection.title) || targetSection.key || t('content.editor.untitledSection'),
            // Which column, said only where there is a choice: "column 1" of a stacked section
            // tells nobody anything.
            ...(columnsOf(targetSection.layout) > 1
              ? [t('content.editor.columnNumber', { number: targetColumn + 1 })]
              : []),
          ].join(' · '),
        };

  // ⚠️ Written once and drawn in both ways of composing: beside the outline, and beside the page
  // itself. Two copies of this would be two panels that can disagree about what a block offers,
  // which is the same argument that keeps one renderer for the public and for the preview.
  //
  // ⚠️ Sticky, from `lg` up. Whatever is on the left is as long as the page is, and this used to
  // scroll away with it: to change the block you were looking at you had to scroll back up, which
  // is the friction of an editor rather than a defect of one. `self-start` is what makes a sticky
  // child of a grid work at all — a stretched cell has nothing to stick inside — and it scrolls on
  // its own when it is taller than the window.
  const properties = (
    <div className="flex flex-col gap-4 xl:sticky xl:top-20 xl:max-h-[calc(100vh-6rem)] xl:self-start xl:overflow-y-auto">
      <SectionHeader
        title={t('content.editor.properties')}
        {...(selection === null
          ? {}
          : {
              // The way back to the page, and the only one needed: the outline has a row for it too,
              // but in the preview there is no outline — and the panel is where you already are.
              actions: (
                <Button type="button" variant="ghost" size="sm" onClick={() => setSelection(null)}>
                  <ChevronLeft aria-hidden className="mr-1 size-4" />
                  {t('content.editor.page')}
                </Button>
              ),
            })}
      />

      {/* ⚠️ Always mounted and merely hidden, never unmounted: it is **one** form and it owns the
          values, the validation and the mapping of what the server refuses back onto the fields.
          Its submit button lives in the toolbar at the top (`form="content-metadata"`), and a
          button cannot submit a form that is not in the document. */}
      <div {...(selection === null ? {} : { hidden: true })}>
        <SchemaForm
          // Remounted whenever the stored row moves on, so the version the form carries is the one
          // the server last returned; keeping a stale one would answer 409 on the next save.
          key={content?.rowVersion ?? 'new'}
          id={METADATA_FORM}
          actionsElsewhere
          schema={contentMetadataSchema(kind, categories)}
          defaults={
            content === null
              ? emptyContent(department, locales, kind, startsAsTemplate)
              : toFormValues(content, locales)
          }
          locales={locales}
          labels="content"
          division={division}
          mediaLibrary={mediaLibrary}
          onSubmit={async (values) => {
            await onSave(values, body);
            setUnsaved(false);
          }}
          submitLabel={t('content.editor.saveDraft')}
        />
      </div>

      {section !== undefined ? (
        <>
          {ruleFor(rules, section.key).locked ? (
            <LockedByTemplate
              template={
                template.data === undefined
                  ? null
                  : { title: template.data.title, department: template.data.ownerDepartment }
              }
              canManage={template.data !== undefined && canManageTemplates(template.data.ownerDepartment)}
            />
          ) : null}

          <SectionProperties
            key={section.id}
            section={section}
            rule={ruleFor(rules, section.key)}
            isTemplate={content?.isTemplate ?? startsAsTemplate}
            locales={locales}
            division={division}
            mediaLibrary={mediaLibrary}
            // The two the strip owns, applied the moment they are clicked. Narrowing the layout has
            // to pull the blocks back into a column that still exists, or the server refuses the
            // save and the editor cannot say why.
            onFrame={(patch) => {
              const withFrame = updateSection(body, section.id, patch);

              change(
                patch.layout === undefined
                  ? withFrame
                  : clampColumns(withFrame, section.id, patch.layout, columnsOf(patch.layout)),
              );
            }}
            onApply={(values) => {
              const withSettings = updateSection(body, section.id, {
                title: values.title,
                mediaId: values.mediaId ?? null,
                padding: values.padding,
                width: values.width,
                // What a template imposes, and only on a template: on a page the form does
                // not draw these, and writing them would be a 400 from the envelope
                // validator — a page carrying them could lift its own restrictions.
                ...((content?.isTemplate ?? startsAsTemplate)
                  ? {
                      ...(typeof values.key === 'string' && values.key.trim() !== ''
                        ? { key: values.key.trim() }
                        : {}),
                      required: values.required === true,
                      locked: values.locked === true,
                      // Nothing ticked means "any block", which is the absence of the key
                      // and not an empty list: an empty one would allow nothing at all.
                      allowedBlocks:
                        values.allowedBlocks === undefined || values.allowedBlocks.length === 0
                          ? null
                          : [...values.allowedBlocks],
                    }
                  : {}),
              });

              change(withSettings);
            }}
          />
        </>
      ) : block !== undefined ? (
        <BlockProperties
          key={block.block.id}
          block={block.block}
          section={block.section}
          locales={locales}
          division={division}
          mediaLibrary={mediaLibrary}
          onApplyProps={(props) => change(updateBlock(body, block.block.id, { props }))}
          onEnvelope={(patch) => change(updateBlock(body, block.block.id, patch))}
        />
      ) : null}
    </div>
  );

  // What the page needs to be composed in: what is selected, and what to do about a click. The
  // renderer reads it from a context that is `null` everywhere else, so a visitor's page has no
  // handler to remove (`blocks/picking.ts`).
  const picking = useMemo(
    () => ({
      selected: selection?.id ?? null,
      onPick: (kind: 'section' | 'block', id: string) => setSelection({ kind, id }),
      // The column the palette will fill, drawn as chosen on the page. Only a selected section has
      // one to show: with a block selected, the destination is that block's column, and the block's
      // own ring already says where that is.
      target: selection?.kind === 'section' ? { section: selection.id, column: targetColumn } : null,
      onPickColumn: (sectionId: string, column: number) => {
        setSelection({ kind: 'section', id: sectionId });
        setChosenColumn({ section: sectionId, column });
      },
      // A section a template locks takes no new block, so its empty columns must not offer one.
      accepts: (sectionId: string) => !ruleFor(rules, findSection(body, sectionId)?.key).locked,
    }),
    [selection, targetColumn, rules, body],
  );

  return (
    <div className="flex flex-col gap-8">
      {/* ⚠️ At the top and sticky, and it used to sit at the **bottom of the metadata form** — which
          measured 1182 pixels in a window of 950, so the page being composed and the buttons that
          save it were both below the fold. Measured, not guessed (road A1 of
          `decisions/2026-09-09-comporre-una-pagina-guardandola.md`).

          `Save draft` submits by `form=`, which is how HTML has always let a button live outside the
          form it belongs to: the form is in the panel on the right, where the page's own properties
          are edited. */}
      {/* ⚠️ And since 11 September 2026 on the frame's own line, beside the title, rather than on a
          line of its own under it (Carmine: stop wasting the space at the top). `PageActions` draws
          it up there while its state stays here, and that line is the sticky one now. */}
      <PageActions>
        <div className="flex flex-wrap items-center gap-2">
          <Button type="submit" form={METADATA_FORM} disabled={busy}>
            {t('content.editor.saveDraft')}
          </Button>

          <Button type="button" variant="ghost" onClick={() => setPreview((shown) => !shown)}>
            {preview ? (
              <List aria-hidden className="mr-2 size-4" />
            ) : (
              <Eye aria-hidden className="mr-2 size-4" />
            )}
            {preview ? t('content.editor.outline') : t('content.editor.onThePage')}
          </Button>

          <Button
            type="button"
            variant="ghost"
            disabled={!history.canUndo}
            onClick={() => {
              history.undo();
              setUnsaved(true);
              // What was selected may not exist in the body that comes back, and the page's own
              // properties are always there to fall back on.
              setSelection(null);
            }}
          >
            <Undo2 aria-hidden className="mr-2 size-4" />
            {t('content.editor.undo')}
          </Button>

          {onPublish === null ? null : (
            <Button
              type="button"
              variant="secondary"
              disabled={unsaved || busy}
              onClick={onPublish}
              title={unsaved ? t('content.editor.saveBeforePublishing') : undefined}
            >
              <Send aria-hidden className="mr-2 size-4" />
              {t('content.editor.publish')}
            </Button>
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

      <PublishProblems body={body} problems={publishProblems} />

      {unsaved ? (
        <p className="text-muted-foreground text-sm">{t('content.editor.saveBeforePublishing')}</p>
      ) : null}

      <TemplateDifferences
        body={body}
        differences={differences}
        onAlign={(difference) => change(applyDifference(body, templateBody, difference))}
      />

      {/* ⚠️ Three columns, asked for by Carmine on 10 September 2026: the components on the left,
          the page in the middle, the properties of whatever is selected on the right. What used to
          be a two-column screen that swapped its left half between an outline and a preview is now
          a fixed frame whose **middle** swaps — so the palette and the properties stay exactly
          where they were while you go from composing on the page to composing in the outline.

          The outline is not a mode you leave behind: it is the keyboard road (`blocks/picking.ts`),
          and clicking the page is the pointer one. Both put the same thing in the panel on the
          right, which is the property that made road (A) work in the first place. */}
      <div className="grid grid-cols-1 gap-6 xl:grid-cols-[13rem_minmax(0,1fr)_19rem]">
        <BlockPalette
          target={paletteTarget}
          rule={paletteRule}
          onAdd={(type) => {
            if (paletteTarget !== null) {
              addBlockTo(paletteTarget.id, type, targetColumn);
            }
          }}
        />

        {preview ? (
          // ⚠️ The preview is not a place you go to and come back from any more. It is one of the two
          // ways of composing — the page itself — and it keeps the same panel beside it, so a block
          // clicked here and the same block clicked in the outline lead to exactly the same fields
          // (decided 9 Sep 2026, `decisions/2026-09-09-comporre-una-pagina-guardandola.md`).
          <PickingContext.Provider value={picking}>
            <PreviewFrame body={body} />
          </PickingContext.Provider>
        ) : (
          <div className="flex flex-col gap-4">
            <SectionHeader title={t('content.editor.structure')} />
            <SectionTree
              body={body}
              rules={rules}
              selection={selection}
              onSelect={setSelection}
              onAddSection={(parentId) => {
                const added = addSection(body, locales, parentId);
                change(added.body);
                setSelection({ kind: 'section', id: added.id });
              }}
              onMoveSection={(id, delta) => change(moveSection(body, id, delta))}
              onMoveBlock={(id, delta) => change(moveBlock(body, id, delta))}
              onReorderSections={(activeId, overId) => change(reorderSections(body, activeId, overId))}
              onReorderBlocks={(activeId, overId) => change(reorderBlocks(body, activeId, overId))}
              onDuplicateBlock={(id) => {
                const copy = duplicateBlock(body, id);
                change(copy.body);
                setSelection({ kind: 'block', id: copy.id });
              }}
              onRemoveSection={(id) => {
                change(removeSection(body, id));
                setSelection(null);
              }}
              onRemoveBlock={(id) => {
                change(removeBlock(body, id));
                setSelection(null);
              }}
            />
          </div>
        )}

        {properties}
      </div>
    </div>
  );
}
