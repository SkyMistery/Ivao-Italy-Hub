import {
  DndContext,
  DragOverlay,
  MeasuringStrategy,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
  type CollisionDetection,
  type DragEndEvent,
} from '@dnd-kit/core';
import { Button } from '@ivao/atmosphere-react';
import { ChevronLeft, Eye, List, Redo2, Undo2 } from 'lucide-react';
import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { registry } from '../../app/registry';
import {
  allSections,
  columnsOf,
  EmbeddingContext,
  PickingContext,
  type BlockEnvelope,
  type Body,
  type Span,
} from '../../blocks';
import type { Department, LocalizedString } from '../../shared/api/bootstrap';
import { isBlank, writtenValues } from '../../shared/forms';
import { PreviewLocaleContext } from '../../shared/i18n/previewLocale';
import { useLocalized } from '../../shared/i18n/useLocalized';
import type { MediaLibraryQuery } from '../../shared/ui';
import { SectionHeader } from '../../shared/ui';

import { BlockPalette } from './BlockPalette';
import { BlockProperties, SectionProperties } from './BlockProperties';
import { BlockDraggable } from './BlockDraggable';
import { DropZone, type BlockDrag, type PaletteDrag, type SlotDrop } from './DropZone';
import { SectionSortable, SectionSortableGroup, type SectionDrag } from './SectionSortable';
import {
  addBlock,
  asTiles,
  addSection,
  clampColumns,
  defaultProps,
  depthOf,
  duplicateBlock,
  duplicateSection,
  findBlock,
  findSection,
  moveBlock,
  moveBlockTo,
  moveSection,
  removeBlock,
  removeSection,
  setSpan,
  reorderBlocks,
  reorderSections,
  updateBlock,
  updateSection,
} from './body';
import { PreviewFrame, type PublishedView } from './PreviewFrame';
import { MAX_ROW_DEPTH, SectionTree, type Selection } from './SectionTree';
import { useBodyHistory, useHistoryShortcuts, useSelectionShortcuts } from './useBodyHistory';
import { applyDifference, templateDiff } from './templateDiff';
import { LockedByTemplate, TemplateDifferences } from './TemplatePanel';
import { NO_RULES, ruleFor, templateRules } from './templateRules';

/**
 * The editor of one `BlockDocument` (design M0 §7.7), and nothing else: a body goes in, every change to it comes out
 * through `onChange`. The components on the left, the page in the middle — drawn by the very renderer a visitor gets,
 * or as an outline — and the properties of whatever is picked on the right, with the way back from the last change.
 *
 * ⚠️ Taken out of `ContentEditor` in T6b (M2, plan 0.84) so that a tour's briefing is written with **this** editor and
 * not a second one (`CLAUDE.md` §2: rich text inside a module is the same `BlockDocument`, same editor, same renderer).
 * What a row of `cms_contents` adds around it — its metadata, its address, its template, saving, publishing, the review
 * — stays in `ContentEditor` and reaches here only as slots and answers.
 */

/** The panel on the right, by id: where a double click on the page sends the cursor. */
const PROPERTIES_PANEL = 'content-properties';

/** What the template a row was made from still says about it: its body, and whose it is. */
export interface BodyTemplate {
  readonly body: Body;
  readonly title: LocalizedString;
  readonly department: Department;
  /** Whether this member may change that template, for the note on a locked section. */
  readonly canManage: boolean;
}

export function BodyEditor({
  initial,
  onChange,
  toolbar,
  header,
  pageProperties,
  locales,
  division,
  mediaLibrary,
  uploadMedia,
  holds,
  dashboard = false,
  isTemplate = false,
  template = null,
  frameUrl,
  published,
  comparing = false,
  onCompare,
  locked = false,
}: {
  /** The body the editor opens on. Read once: from then on the editor owns it and says so through `onChange`. */
  initial: Body;
  /** Every change, undo and redo included. The owner keeps the latest to save it. */
  onChange: (body: Body) => void;
  /** Where the editor's own buttons go — outline, undo, redo — among the owner's (save, publish…). */
  toolbar: (tools: ReactNode) => ReactNode;
  /** What the owner says between its toolbar and the page: problems, the state of the draft, a review. */
  header?: ReactNode;
  /** What the panel on the right shows while nothing is picked: the row's own fields, or nothing. */
  pageProperties?: ReactNode;
  locales: readonly string[];
  division: { defaultLocale: string; timezone: string };
  mediaLibrary: MediaLibraryQuery;
  uploadMedia?: ((file: File) => Promise<number>) | undefined;
  /** Whether this member holds a permission where a block would be added: the palette leaves out what they may not. */
  holds: (permission: string) => boolean;
  /** A dashboard is composed as a grid of tiles (D2). */
  dashboard?: boolean;
  /** A template's sections carry `key`, `required`, `locked` and `allowedBlocks`; nothing else's do. */
  isTemplate?: boolean;
  /** The template this body was made from, once loaded; null for a body with none, or while it is asked for. */
  template?: BodyTemplate | null;
  /**
   * Where the frame of an interactive block of this body is served, in a language; null while there is none — a row
   * never saved, or an owner the server builds no frame for. The block says so rather than drawing an empty box.
   */
  frameUrl?: ((blockId: string, locale: string) => string | null) | undefined;
  /** The version visitors read now, when there is one to compare with. */
  published?: PublishedView | undefined;
  comparing?: boolean;
  onCompare?: ((comparing: boolean) => void) | undefined;
  /** Read, not written: every field and every button of the frame is switched off. */
  locked?: boolean;
}) {
  const { t, i18n } = useTranslation();
  const read = useLocalized();

  // The body, and the way back from the last thing that happened to it. A section moved by mistake
  // was one of the frictions the hand copy of `/about` recorded (HANDOFF §27).
  const history = useBodyHistory(initial);
  const body = history.body;

  // The language the page is drawn in, and the tab every translated field opens on: the site's to
  // begin with, changed from the frame (`PreviewLocaleContext`). Before this the form opened on
  // the division's first language while the page showed the site's, and what was typed in one did
  // not show in the other.
  const [previewLocale, setPreviewLocale] = useState<string>(
    () => (locales.includes(i18n.language) ? i18n.language : locales[0]) ?? i18n.language,
  );

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

  // What the template still says about this page: which sections are fixed, which are locked, and
  // which blocks may go in them. The page itself does not carry any of it (see `templateRules`).
  const templateBody = template?.body ?? null;
  const rules = templateBody === null ? NO_RULES : templateRules(templateBody);

  // And what it says that this page does not. Nothing here changes anything by itself: the list is
  // read out, and one line at a time is acted on (design M1 §9.1).
  const differences = templateDiff(body, templateBody);

  const change = (next: Body, options?: { coalesce?: string }) => {
    history.change(next, options);
    onChange(next);
  };

  // The way back, and forth. What was selected may not exist in the body that comes back — a block
  // added and then undone — and then the page's own properties are what is left to show. When it
  // does exist it stays selected, so undoing a word typed into a block does not also close it.
  const restore = (restored: Body | null): boolean => {
    if (restored === null) {
      return false;
    }

    onChange(restored);

    if (
      selection !== null &&
      (selection.kind === 'block'
        ? findBlock(restored, selection.id)
        : findSection(restored, selection.id)) === undefined
    ) {
      setSelection(null);
    }

    return true;
  };

  const undo = () => restore(history.undo());
  const redo = () => restore(history.redo());
  useHistoryShortcuts(undo, redo);

  // A double click on the page: picked, and the cursor in the first field of the panel — the
  // first one that is *shown*, since the page's own form is always there and merely hidden. When
  // the pick changes, the panel is drawn first and the cursor follows (the ref, read after the
  // render); when it does not, the panel is already there and the cursor goes at once.
  const focusWanted = useRef(false);
  const focusFirstField = () => {
    const fields = document
      .getElementById(PROPERTIES_PANEL)
      ?.querySelectorAll<HTMLElement>('input:not([type="hidden"]), textarea, [role="combobox"]');
    [...(fields ?? [])].find((field) => field.offsetParent !== null)?.focus();
  };
  useEffect(() => {
    if (focusWanted.current) {
      focusWanted.current = false;
      focusFirstField();
    }
  }, [selection]);

  // What is picked, brought into view on the page: a pick made in the outline, or a block just
  // added at the bottom of a long section, would otherwise be picked off screen. `nearest` moves
  // nothing when it is already in view.
  useEffect(() => {
    if (!preview) {
      return;
    }

    const picked = document.querySelector('[data-picked]');
    if (picked !== null && typeof picked.scrollIntoView === 'function') {
      picked.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
    }
  }, [selection, preview]);

  // Where the frame of an interactive block lives while a page is being written, in the language it
  // is drawn in. ⚠️ It is the draft as **saved** — the frame is a document the server builds, so what
  // it shows is the last save and not the keystroke before last.
  const embedding = useMemo(
    () => ({ frameUrl: (blockId: string) => frameUrl?.(blockId, previewLocale) ?? null }),
    [frameUrl, previewLocale],
  );

  const section = selection?.kind === 'section' ? findSection(body, selection.id) : undefined;
  const block = selection?.kind === 'block' ? findBlock(body, selection.id) : undefined;

  // Adding a block, written once: the palette on the left and the one inside the outline do the
  // very same thing, and a block added from either has to start out identical -- same blank
  // properties, same render mode. Two copies of this would be two ways of being born.
  const addBlockTo = (sectionId: string, type: string, column = 0, at?: number) => {
    const registration = registry.blocks.find((candidate) => candidate.type === type);
    if (registration === undefined) {
      return;
    }

    // On a dashboard the section is first written as its tiles, so that where the component lands is
    // a place in the grid (D2).
    const added = addBlock(
      dashboard ? asTiles(body, sectionId) : body,
      sectionId,
      type,
      // The blank properties, minus the optional ones nobody has written into: a block
      // added and never opened must not carry an empty translated value, which
      // publication would read as a page translated into one language only.
      writtenValues(registration.schema, defaultProps(registration.schema, locales)),
      // A data block starts live: capturing is a decision somebody makes, and one that
      // only means anything once the page is published.
      registration.kind === 'Data' ? 'live' : null,
      dashboard ? 0 : column,
      at,
    );

    change(added.body);
    setSelection({ kind: 'block', id: added.id });
  };

  // Dragging a component from the palette onto the page (G15, session 3). One context around the
  // palette and the page; the outline, when it is in the middle, has a context of its own for its
  // rows, and the palette entries are not draggable then — so the two never handle one gesture.
  // A drag has to start further than a click, or a click on the palette would be a lottery.
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 4 } }));
  const [dragging, setDragging] = useState<PaletteDrag | BlockDrag | null>(null);

  // Three things are dragged in this one context, and each only ever lands on its own kind: a
  // component from the palette and a block of the page on a slot, a section on a section. Told
  // apart here, so that a block carried over a section is not "over" it, and a section carried
  // over a slot neither.
  const collisions: CollisionDetection = (args) => {
    const wanted =
      (args.active.data.current as PaletteDrag | BlockDrag | SectionDrag | undefined)?.kind === 'section'
        ? 'section'
        : 'slot';

    return closestCenter({
      ...args,
      droppableContainers: args.droppableContainers.filter(
        (container) => (container.data.current as { kind?: string } | undefined)?.kind === wanted,
      ),
    });
  };

  const onDragEnd = ({ active, over }: DragEndEvent) => {
    setDragging(null);

    const dragged = active.data.current as PaletteDrag | BlockDrag | SectionDrag | undefined;
    const target = over?.data.current as SlotDrop | SectionDrag | undefined;

    if (dragged?.kind === 'palette' && target?.kind === 'slot') {
      addBlockTo(target.section, dragged.type, target.column, target.index);
      return;
    }

    // A block of the page, dropped on a slot anywhere on it: its own column, another, another
    // section's. It stays picked, so the panel keeps showing it wherever it landed.
    if (dragged?.kind === 'block' && target?.kind === 'slot') {
      change(
        dashboard
          ? moveBlockTo(asTiles(body, target.section), dragged.id, target.section, 0, target.index)
          : moveBlockTo(body, dragged.id, target.section, target.column, target.index),
      );
      return;
    }

    // A section dropped onto another of its siblings. Crossing into another parent is refused by
    // the body helper, as it is in the outline: a drop that moved nothing is a drop that moved nothing.
    if (dragged?.kind === 'section' && target?.kind === 'section' && over !== null && active.id !== over.id) {
      change(reorderSections(body, String(active.id), String(over.id)));
    }
  };

  const draggedRegistration =
    dragging === null ? undefined : registry.blocks.find((candidate) => candidate.type === dragging.type);

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
    <div
      id={PROPERTIES_PANEL}
      className="scroll-thin flex flex-col gap-4 xl:sticky xl:top-20 xl:max-h-[calc(100vh-6rem)] xl:self-start xl:overflow-y-auto"
    >
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

      {/* ⚠️ Always mounted and merely hidden, never unmounted: the owner's fields may be **one** form that owns
          its values, its validation and the mapping of what the server refuses back onto the fields, and whose
          submit button lives in the toolbar — a button cannot submit a form that is not in the document. */}
      <div {...(selection === null ? {} : { hidden: true })}>
        {pageProperties ?? <p className="text-muted-foreground text-sm">{t('content.editor.pickHint')}</p>}
      </div>

      {section !== undefined ? (
        <>
          {ruleFor(rules, section.key).locked ? (
            <LockedByTemplate
              template={template === null ? null : { title: template.title, department: template.department }}
              canManage={template?.canManage ?? false}
            />
          ) : null}

          <SectionProperties
            key={section.id}
            section={section}
            rule={ruleFor(rules, section.key)}
            isTemplate={isTemplate}
            locales={locales}
            division={division}
            mediaLibrary={mediaLibrary}
            uploadMedia={uploadMedia}
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
                ...(isTemplate
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

              // A run of typing into one section's settings is one step to undo.
              change(withSettings, { coalesce: `section:${section.id}` });
            }}
          />
        </>
      ) : block !== undefined ? (
        <BlockProperties
          key={block.block.id}
          dashboard={dashboard}
          block={block.block}
          section={block.section}
          // Where it may be moved to from the keyboard: every section the template does not lock,
          // named as the outline names them — and nowhere at all from a locked section, whose
          // blocks stay where the template put them (a select that listed everywhere but here
          // drew itself empty).
          sections={
            ruleFor(rules, block.section.key).locked
              ? []
              : allSections(body)
                  .filter((candidate) => !ruleFor(rules, candidate.key).locked)
                  .map((candidate) => ({
                    value: candidate.id,
                    label: read(candidate.title) || candidate.key || t('content.editor.untitledSection'),
                  }))
          }
          onMoveTo={(sectionId) =>
            change(moveBlockTo(body, block.block.id, sectionId, 0, Number.MAX_SAFE_INTEGER))
          }
          locales={locales}
          division={division}
          mediaLibrary={mediaLibrary}
          uploadMedia={uploadMedia}
          // Applied at every pause in typing, and remembered as one step: a sentence written into
          // a block is one thing to undo, not one per pause (`useBodyHistory`).
          onApplyProps={(props) =>
            change(updateBlock(body, block.block.id, { props }), { coalesce: `props:${block.block.id}` })
          }
          onEnvelope={(patch) => change(updateBlock(body, block.block.id, patch))}
        />
      ) : null}
    </div>
  );

  // What may be done to a section or a block, written once and reached from two places: the rows
  // of the outline, and the bar on the picked thing itself on the page (Carmine, 11 September
  // 2026). The rules of the template are read here, so the page and the outline offer the same.
  const addSectionAt = (parentId?: string) => {
    const added = addSection(body, locales, parentId);
    change(added.body);
    setSelection({ kind: 'section', id: added.id });
  };
  const duplicateBlockById = (id: string) => {
    const copy = duplicateBlock(body, id);
    change(copy.body);
    setSelection({ kind: 'block', id: copy.id });
  };
  const removeSectionById = (id: string) => {
    change(removeSection(body, id));
    setSelection(null);
  };
  const duplicateSectionById = (id: string) => {
    const copy = duplicateSection(body, id);
    change(copy.body);
    setSelection({ kind: 'section', id: copy.id });
  };
  const removeBlockById = (id: string) => {
    change(removeBlock(body, id));
    setSelection(null);
  };

  // What the page needs to be composed in: what is selected, and what to do about a click. The
  // renderer reads it from a context that is `null` everywhere else, so a visitor's page has no
  // handler to remove (`blocks/picking.ts`).
  // Not memoized by hand: the compiler does it, and refused to keep a manual memo whose inputs it
  // could not prove untouched once the drop handler read the body.
  // The bar on the picked thing: what the outline's row offers, with the template's rules. A row
  // is offered down to the level the server still accepts, as in the outline. Written once, and
  // read by the page, by the keys below, and by nothing else.
  const actionsFor = ({ kind, id }: { kind: 'section' | 'block'; id: string }) => {
    if (kind === 'block') {
      const found = findBlock(body, id);
      if (found === undefined || ruleFor(rules, found.section.key).locked) {
        return [];
      }

      return [
        { key: 'moveUp' as const, run: () => change(moveBlock(body, id, -1)) },
        { key: 'moveDown' as const, run: () => change(moveBlock(body, id, 1)) },
        { key: 'duplicate' as const, run: () => duplicateBlockById(id) },
        { key: 'remove' as const, run: () => removeBlockById(id) },
      ];
    }

    // A section moves among its siblings — the page's sections, or the rows of one section
    // (Carmine, 11 September 2026: "the sections already placed, I want to move them by hand on
    // the page"). The same arrows the outline has, on the thing itself.
    const rule = ruleFor(rules, findSection(body, id)?.key);
    return [
      ...(rule.locked
        ? []
        : [
            { key: 'moveUp' as const, run: () => change(moveSection(body, id, -1)) },
            { key: 'moveDown' as const, run: () => change(moveSection(body, id, 1)) },
          ]),
      ...(rule.locked || (depthOf(body, id) ?? MAX_ROW_DEPTH) >= MAX_ROW_DEPTH
        ? []
        : [{ key: 'addRow' as const, run: () => addSectionAt(id) }]),
      ...(rule.locked ? [] : [{ key: 'duplicate' as const, run: () => duplicateSectionById(id) }]),
      ...(rule.locked || rule.required ? [] : [{ key: 'remove' as const, run: () => removeSectionById(id) }]),
    ];
  };

  // Delete, ⌘D and Escape on what is picked: the same commands the bar offers, so a key can never
  // do what the bar would refuse.
  const runOnPicked = (key: 'remove' | 'duplicate'): boolean => {
    if (selection === null) {
      return false;
    }

    const action = actionsFor(selection).find((candidate) => candidate.key === key);
    if (action === undefined) {
      return false;
    }

    action.run();
    return true;
  };

  useSelectionShortcuts({
    remove: () => runOnPicked('remove'),
    duplicate: () => runOnPicked('duplicate'),
    release: () => {
      if (selection === null) {
        return false;
      }

      setSelection(null);
      return true;
    },
  });

  const picking = {
    actions: actionsFor,
    onOpen: (kind: 'section' | 'block', id: string) => {
      if (selection?.kind === kind && selection.id === id) {
        focusFirstField();
        return;
      }

      focusWanted.current = true;
      setSelection({ kind, id });
    },
    onAddSection: () => addSectionAt(),
    // What to draw a block by while nothing is written in it. A data block is never blank: it
    // draws an answer, or its own empty state.
    blank: (candidate: BlockEnvelope) => {
      const registration = registry.blocks.find((known) => known.type === candidate.type);
      if (registration === undefined || registration.kind === 'Data') {
        return null;
      }

      return isBlank(registration.schema, candidate.props) ? t(registration.editorLabelKey) : null;
    },
    // What lets a section be dragged among its siblings on the page, and a block onto any slot of
    // it; the grip is on the bar of the picked one.
    SortableGroup: SectionSortableGroup,
    Sortable: SectionSortable,
    BlockDraggable,
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
    // Where a dragged component may land, drawn by the renderer, known to dnd-kit only here.
    DropZone,
    // The width of a tile of a dashboard, from its handle; one step of the history per gesture.
    ...(dashboard
      ? { onSpan: (id: string, span: Span) => change(setSpan(body, id, span), { coalesce: `span:${id}` }) }
      : {}),
  };

  // The editor's own buttons, handed to the owner to place among its own.
  const tools = (
    <>
      <Button type="button" variant="ghost" onClick={() => setPreview((shown) => !shown)}>
        {preview ? <List aria-hidden className="mr-2 size-4" /> : <Eye aria-hidden className="mr-2 size-4" />}
        {preview ? t('content.editor.outline') : t('content.editor.onThePage')}
      </Button>

      {/* Also ⌘Z and ⌘⇧Z, outside a field (`useHistoryShortcuts`); the buttons are what says
          the two exist, and the road for anybody who does not know the keys. */}
      <Button type="button" variant="ghost" disabled={!history.canUndo} onClick={undo}>
        <Undo2 aria-hidden className="mr-2 size-4" />
        {t('content.editor.undo')}
      </Button>

      <Button type="button" variant="ghost" disabled={!history.canRedo} onClick={redo}>
        <Redo2 aria-hidden className="mr-2 size-4" />
        {t('content.editor.redo')}
      </Button>
    </>
  );

  return (
    // The language the page is drawn in, for every translated value read inside the editor and for
    // the tab every translated field opens on — the owner's fields in the panel too.
    <PreviewLocaleContext.Provider value={previewLocale}>
      <EmbeddingContext.Provider value={embedding}>
        <div className="flex flex-col gap-8">
          {toolbar(tools)}

          {header}

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
          {/* Read, not written: every field and every button of the frame is switched off at once,
              which is what a disabled fieldset is for. */}
          <fieldset disabled={locked} className="m-0 min-w-0 border-0 p-0">
            <DndContext
              sensors={sensors}
              // The slots are hidden until a drag begins, so they have to be measured once it has.
              measuring={{ droppable: { strategy: MeasuringStrategy.Always } }}
              collisionDetection={collisions}
              onDragStart={({ active }) => {
                const started = active.data.current as PaletteDrag | BlockDrag | SectionDrag | undefined;
                setDragging(started?.kind === 'palette' || started?.kind === 'block' ? started : null);
              }}
              onDragCancel={() => setDragging(null)}
              onDragEnd={onDragEnd}
            >
              <div className="grid grid-cols-1 gap-6 xl:grid-cols-[13rem_minmax(0,1fr)_19rem]">
                <BlockPalette
                  target={paletteTarget}
                  rule={paletteRule}
                  draggable={preview}
                  holds={holds}
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
                    <PreviewFrame
                      dashboard={dashboard}
                      body={body}
                      locales={locales}
                      locale={previewLocale}
                      onLocale={setPreviewLocale}
                      published={published}
                      comparing={comparing}
                      onCompare={onCompare}
                    />
                  </PickingContext.Provider>
                ) : (
                  <div className="flex flex-col gap-4">
                    <SectionHeader title={t('content.editor.structure')} />
                    <SectionTree
                      body={body}
                      rules={rules}
                      selection={selection}
                      onSelect={setSelection}
                      onAddSection={addSectionAt}
                      onMoveSection={(id, delta) => change(moveSection(body, id, delta))}
                      onMoveBlock={(id, delta) => change(moveBlock(body, id, delta))}
                      onReorderSections={(activeId, overId) =>
                        change(reorderSections(body, activeId, overId))
                      }
                      onReorderBlocks={(activeId, overId) => change(reorderBlocks(body, activeId, overId))}
                      onDuplicateBlock={duplicateBlockById}
                      onDuplicateSection={duplicateSectionById}
                      onRemoveSection={removeSectionById}
                      onRemoveBlock={removeBlockById}
                    />
                  </div>
                )}

                {properties}
              </div>

              {/* What travels under the pointer: a copy of the entry, not the entry itself, which sits in a
          panel that scrolls and would clip it. */}
              <DragOverlay dropAnimation={null}>
                {draggedRegistration === undefined ? null : (
                  <div className="bg-body text-foreground border-border flex items-center gap-2 rounded-md border px-3 py-1.5 text-sm shadow-md">
                    <draggedRegistration.icon aria-hidden className="size-4 shrink-0" />
                    {t(draggedRegistration.editorLabelKey)}
                  </div>
                )}
              </DragOverlay>
            </DndContext>
          </fieldset>
        </div>
      </EmbeddingContext.Provider>
    </PreviewLocaleContext.Provider>
  );
}
