import { Label, Select } from '@ivao/atmosphere-react';
import { useTranslation } from 'react-i18next';

import { registry } from '../../app/registry';
import {
  columnsOf,
  type Background,
  type BlockEnvelope,
  type Layout,
  type SectionEnvelope,
} from '../../blocks';
import { SchemaForm, writtenValues } from '../../shared/forms';
import { emptyLocalized } from '../../shared/i18n/localized';
import type { MediaLibraryQuery } from '../../shared/ui';

import { defaultProps } from './body';
import { SectionFrame } from './SectionFrame';
import { sectionSettingsSchema, type SectionFormValues } from './schema';
import type { SectionRule } from './templateRules';

/**
 * The right panel of the editor: whatever is selected, as a form. There is no field written by
 * hand in this file — a block's properties are drawn by `SchemaForm` from the schema the block
 * registered, which is the same generator the entity screens use (design M0 §7.5).
 *
 * Since G15 (11 September 2026) the form **applies as it is written**: the block on the page
 * changes a moment after the last keystroke, and there is no "apply" button any more. What used to
 * be write, press, look, correct, press is now write and look — which is what an editor is for.
 *
 * What is *not* a property sits outside the form, because it is not part of `props`: whether a data
 * block shows a capture or asks the provider, and which column it stands in. Both are the envelope.
 */

export function SectionProperties({
  section,
  rule,
  isTemplate,
  locales,
  division,
  mediaLibrary,
  onApply,
  onFrame,
}: {
  section: SectionEnvelope;
  rule: SectionRule;
  /**
   * Whether the row being edited is a template. Four more fields if it is — what this section
   * imposes on the pages made from it — and none of them on a page, where the server refuses three
   * of them outright (design M1 §9.1).
   */
  isTemplate: boolean;
  locales: readonly string[];
  division: { defaultLocale: string; timezone: string };
  /** The library the picture behind a section is chosen from — this department's. */
  mediaLibrary: MediaLibraryQuery;
  /**
   * The settings, applied as they are written — with one exception, the `key` of a template's
   * section, which comes through here only when the button under the form is pressed. A key is
   * written once and then fixed (decision `2026-09-07-scrivere-un-template.md`), so it must not be
   * fixed at the first pause in typing it: "in" would be the key of a section meant to be "intro".
   */
  onApply: (values: SectionFormValues) => void;
  /**
   * The two the strip changes, chosen while looking at the page rather than written and read back
   * (`SectionFrame`).
   */
  onFrame: (patch: { background?: Background; layout?: Layout }) => void;
}) {
  const { t } = useTranslation();

  if (rule.locked) {
    // A locked section shows its fields and not its structure (docs/UI-GUIDELINES.md): what is
    // missing here is the *settings* of a section the template fixed, and the line above says
    // which template fixed it.
    return <p className="text-muted-foreground text-sm">{t('content.editor.lockedSection')}</p>;
  }

  const named = typeof section.key === 'string' && section.key.length > 0;

  // The block types this section may allow, labelled the way the palette labels them: the generator
  // draws the labels it is handed and never translates a value of its own.
  const blocks = registry.blocks.map((block) => ({
    value: block.type,
    label: t(block.editorLabelKey),
  }));

  const defaults: SectionFormValues = {
    title: { ...emptyLocalized(locales), ...(section.title ?? {}) },
    ...(typeof section.mediaId === 'number' ? { mediaId: section.mediaId } : {}),
    padding: section.padding,
    width: section.width,
    ...(isTemplate
      ? {
          ...(named ? {} : { key: '' }),
          required: section.required === true,
          locked: section.locked === true,
          allowedBlocks: [...(section.allowedBlocks ?? [])],
        }
      : {}),
  };

  return (
    <div className="flex flex-col gap-4">
      <SectionFrame
        background={section.background}
        layout={section.layout}
        onBackground={(background) => onFrame({ background })}
        onLayout={(layout) => onFrame({ layout })}
      />

      {isTemplate && named ? (
        // Written once, then shown. Changing it would break the match with every page already made
        // from this template, silently (decision `2026-09-07-scrivere-un-template.md`).
        <p className="text-muted-foreground text-sm">{t('content.section.keyIs', { key: section.key })}</p>
      ) : null}

      <SchemaForm
        // Remounted when the section changes shape under it — a key written for the first time
        // takes the field away — so the form is never one field out of date with its own schema.
        key={`${section.id}:${String(named)}`}
        schema={sectionSettingsSchema(isTemplate ? { blocks, unnamed: !named } : null)}
        defaults={defaults}
        locales={locales}
        labels="content.section"
        division={division}
        mediaLibrary={mediaLibrary}
        // Everything but the key, as it is written. The key is emptied rather than dropped so the
        // values keep their shape, and an empty key is what `onApply` reads as "none".
        onChange={(values) => onApply({ ...values, key: '' })}
        // The key, and only while there is one to write: the button goes with the field.
        {...(isTemplate && !named
          ? {
              onSubmit: (values: SectionFormValues) => {
                onApply(values);
                return Promise.resolve();
              },
              submitLabel: t('content.editor.setKey'),
            }
          : {})}
      />
    </div>
  );
}

export function BlockProperties({
  block,
  section,
  sections,
  locales,
  division,
  mediaLibrary,
  onApplyProps,
  onEnvelope,
  onMoveTo,
}: {
  block: BlockEnvelope;
  section: SectionEnvelope;
  /**
   * Every section of the page a block may be moved into, named as the outline names them. The
   * keyboard's road between sections (Carmine, 11 September 2026): dragging on the page is the
   * pointer's, and a select here is the same move without one.
   */
  sections: readonly { value: string; label: string }[];
  locales: readonly string[];
  /** What an instant needs to say where the division is, and a media field to name its language. */
  division: { defaultLocale: string; timezone: string };
  /**
   * The library a `.meta({ media: true })` property chooses from. Eight of the blocks name a file,
   * so from G3 on this is not an occasional prop: without it those forms throw, and say why.
   */
  mediaLibrary: MediaLibraryQuery;
  onApplyProps: (props: Record<string, unknown>) => void;
  onEnvelope: (patch: Partial<BlockEnvelope>) => void;
  /** Moves the block to the end of the first column of that section. */
  onMoveTo: (sectionId: string) => void;
}) {
  const { t } = useTranslation();
  const registration = registry.blocks.find((candidate) => candidate.type === block.type);

  if (registration === undefined) {
    return <p className="text-muted-foreground text-sm">{t('blocks.unknown', { type: block.type })}</p>;
  }

  const columns = columnsOf(section.layout);

  return (
    <div className="flex flex-col gap-6">
      {sections.length > 1 ? (
        <div className="flex flex-col gap-1">
          <Label htmlFor="section">{t('content.editor.section')}</Label>
          <Select
            value={section.id}
            onValueChange={(sectionId) => {
              if (sectionId !== section.id) {
                onMoveTo(sectionId);
              }
            }}
            items={[...sections]}
          />
        </div>
      ) : null}

      {registration.kind === 'Data' && registration.alwaysLive !== true ? (
        <div className="flex flex-col gap-1">
          <Label htmlFor="renderMode">{t('content.editor.renderMode')}</Label>
          <Select
            value={block.renderMode ?? 'live'}
            onValueChange={(mode) => onEnvelope({ renderMode: mode as 'live' | 'frozen' })}
            items={[
              { value: 'live', label: t('content.editor.renderModes.live') },
              { value: 'frozen', label: t('content.editor.renderModes.frozen') },
            ]}
          />
          <p className="text-muted-foreground text-sm">{t('content.editor.renderModeHint')}</p>
        </div>
      ) : null}

      {columns > 1 ? (
        <div className="flex flex-col gap-1">
          <Label htmlFor="column">{t('content.editor.column')}</Label>
          <Select
            value={String(block.column ?? 0)}
            onValueChange={(column) => onEnvelope({ column: Number(column) })}
            items={Array.from({ length: columns }, (_, index) => ({
              value: String(index),
              label: t('content.editor.columnNumber', { number: index + 1 }),
            }))}
          />
        </div>
      ) : null}

      <SchemaForm
        schema={registration.schema}
        defaults={withDefaults(registration.schema, block.props, locales)}
        locales={locales}
        labels={`blocks.${block.type}`}
        division={division}
        mediaLibrary={mediaLibrary}
        // What is stored is what was written. An optional translated property left empty in every
        // language would otherwise travel as `{ en: "", it: "" }`, and publication — which reads
        // the body without knowing what a block means — would read that as a translation hole and
        // refuse the page (`writtenValues`).
        onChange={(values) => onApplyProps(writtenValues(registration.schema, values))}
      />
    </div>
  );
}

/**
 * A block written before its schema grew a property has no value for it, and a form field with no
 * value is an uncontrolled input that React complains about and a coordinator cannot use. So the
 * stored properties are laid over what the schema says a fresh block holds.
 */
function withDefaults(
  schema: (typeof registry.blocks)[number]['schema'],
  props: Record<string, unknown>,
  locales: readonly string[],
): Record<string, unknown> {
  // "What does a fresh block hold" is read off the schema, in `body.ts`, and nowhere else.
  return { ...defaultProps(schema, locales), ...props };
}
