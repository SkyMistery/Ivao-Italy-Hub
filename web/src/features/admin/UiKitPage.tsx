import { useTranslation } from 'react-i18next';

import type { Bootstrap } from '../../shared/api/bootstrap';
import { EmptyState, PageShell, SectionHeader } from '../../shared/ui';

import { UI_KIT_BLOCKS, UI_KIT_SECTIONS, type UiKitSection } from './uiKitSections';

/**
 * Every custom component of M0 and every block of the registry, mounted with example props.
 *
 * It is a page a coordinator can be pointed at, and it is also the thing that keeps the closed list
 * of design §7.1 honest: a component added without a section fails a test, and so does a block added
 * to the registry without one. In F8 that test grows a third side and becomes
 * "server ⇄ manifest ⇄ ui-kit".
 */
export function UiKitPage({ bootstrap }: { bootstrap: Bootstrap }) {
  const { t } = useTranslation();

  return (
    <PageShell title={t('uiKit.title')} description={t('uiKit.description')}>
      <div className="flex flex-col gap-10">
        <Group title={t('uiKit.components')} sections={UI_KIT_SECTIONS} bootstrap={bootstrap} />
        <Group title={t('uiKit.blocks')} sections={UI_KIT_BLOCKS} bootstrap={bootstrap} />
      </div>
    </PageShell>
  );
}

function Group({
  title,
  sections,
  bootstrap,
}: {
  title: string;
  sections: readonly UiKitSection[];
  bootstrap: Bootstrap;
}) {
  const { t } = useTranslation();

  return (
    <section className="flex flex-col gap-6">
      <SectionHeader title={title} />
      {sections.length === 0 ? (
        <EmptyState title={t('uiKit.noBlocks')} />
      ) : (
        sections.map((section) => (
          <article key={section.name} data-ui-kit={section.name} className="flex flex-col gap-2">
            <h4 className="text-muted-foreground font-mono text-sm">{section.name}</h4>
            <div className="border-border rounded-lg border p-4">{section.render(bootstrap)}</div>
          </article>
        ))
      )}
    </section>
  );
}
