import { useTranslation } from 'react-i18next';

import { registry } from '../../app/registry';
import type { Bootstrap } from '../../shared/api/bootstrap';
import { EmptyState, PageShell, SectionHeader } from '../../shared/ui';

import { compareRegistries, registriesAgree } from './registryDiff';
import { UI_KIT_BLOCKS, UI_KIT_SECTIONS, type UiKitSection } from './uiKitSections';

/**
 * Every custom component of M0 and every block of the registry, mounted with example props.
 *
 * It is a page a coordinator can be pointed at, and it is also the thing that keeps the closed list
 * of design §7.1 honest: a component added without a section fails a test, and so does a block added
 * to the registry without one.
 *
 * The third side is at the top: what the server declares against what this build registered. The
 * two halves of a module are compiled separately, so they can disagree, and every other way of
 * finding that out involves a coordinator staring at a page with a hole in it (design M0 §6.5).
 */
export function UiKitPage({ bootstrap }: { bootstrap: Bootstrap }) {
  const { t } = useTranslation();

  return (
    <PageShell title={t('uiKit.title')} description={t('uiKit.description')}>
      <div className="flex flex-col gap-10">
        <RegistrySection bootstrap={bootstrap} />
        <Group title={t('uiKit.components')} sections={UI_KIT_SECTIONS} bootstrap={bootstrap} />
        <Group title={t('uiKit.blocks')} sections={UI_KIT_BLOCKS} bootstrap={bootstrap} />
      </div>
    </PageShell>
  );
}

/** What the server says it knows, next to what this build can draw. */
function RegistrySection({ bootstrap }: { bootstrap: Bootstrap }) {
  const { t } = useTranslation();
  const difference = compareRegistries(bootstrap, registry.blocks, registry.widgets);

  const lines: string[] = [
    ...difference.blocksMissingInBrowser.map((type) => t('uiKit.registry.blockMissingHere', { type })),
    ...difference.blocksMissingOnServer.map((type) => t('uiKit.registry.blockMissingOnServer', { type })),
    ...difference.blockVersionMismatches.map((detail) => t('uiKit.registry.blockVersion', { detail })),
    ...difference.widgetsMissingInBrowser.map((key) => t('uiKit.registry.widgetMissingHere', { key })),
    ...difference.widgetsMissingOnServer.map((key) => t('uiKit.registry.widgetMissingOnServer', { key })),
  ];

  return (
    <section className="flex flex-col gap-3">
      <SectionHeader title={t('uiKit.registry.title')} description={t('uiKit.registry.description')} />
      {registriesAgree(difference) ? (
        <p className="text-muted-foreground text-sm">
          {t('uiKit.registry.agree', {
            blocks: bootstrap.registries.blocks.length,
            widgets: bootstrap.registries.widgets.length,
          })}
        </p>
      ) : (
        <ul className="border-border flex flex-col gap-1 rounded-md border border-dashed p-4 text-sm">
          {lines.map((line) => (
            <li key={line}>{line}</li>
          ))}
        </ul>
      )}
    </section>
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
