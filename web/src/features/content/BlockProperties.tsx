import { Label, Select } from '@ivao/atmosphere-react';
import { useTranslation } from 'react-i18next';

import { registry } from '../../app/registry';
import { columnsOf, type BlockEnvelope, type SectionEnvelope } from '../../blocks';
import { SchemaForm } from '../../shared/forms';
import { emptyLocalized } from '../../shared/i18n/localized';

import { defaultProps } from './body';
import { sectionSettingsSchema, type SectionFormValues } from './schema';
import type { SectionRule } from './templateRules';

/**
 * The right panel of the editor: whatever is selected, as a form. There is no field written by
 * hand in this file — a block's properties are drawn by `SchemaForm` from the schema the block
 * registered, which is the same generator the entity screens use (design M0 §7.5).
 *
 * What is *not* a property sits outside the form, because it is not part of `props`: whether a data
 * block shows a capture or asks the provider, and which column it stands in. Both are the envelope,
 * and both take effect straight away rather than on a submit.
 */

export function SectionProperties({
  section,
  rule,
  locales,
  onApply,
}: {
  section: SectionEnvelope;
  rule: SectionRule;
  locales: readonly string[];
  onApply: (values: SectionFormValues) => void;
}) {
  const { t } = useTranslation();

  if (rule.locked) {
    return <p className="text-muted-foreground text-sm">{t('content.editor.lockedSection')}</p>;
  }

  const defaults: SectionFormValues = {
    title: { ...emptyLocalized(locales), ...(section.title ?? {}) },
    layout: section.layout,
    background: section.background,
    padding: section.padding,
    width: section.width,
  };

  return (
    <SchemaForm
      schema={sectionSettingsSchema}
      defaults={defaults}
      locales={locales}
      labels="content.section"
      onSubmit={(values) => {
        onApply(values);
        return Promise.resolve();
      }}
      submitLabel={t('content.editor.applySection')}
    />
  );
}

export function BlockProperties({
  block,
  section,
  locales,
  onApplyProps,
  onEnvelope,
}: {
  block: BlockEnvelope;
  section: SectionEnvelope;
  locales: readonly string[];
  onApplyProps: (props: Record<string, unknown>) => void;
  onEnvelope: (patch: Partial<BlockEnvelope>) => void;
}) {
  const { t } = useTranslation();
  const registration = registry.blocks.find((candidate) => candidate.type === block.type);

  if (registration === undefined) {
    return <p className="text-muted-foreground text-sm">{t('blocks.unknown', { type: block.type })}</p>;
  }

  const columns = columnsOf(section.layout);

  return (
    <div className="flex flex-col gap-6">
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
        onSubmit={(values) => {
          onApplyProps(values);
          return Promise.resolve();
        }}
        submitLabel={t('content.editor.applyBlock')}
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
