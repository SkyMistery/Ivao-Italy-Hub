import { useTranslation } from 'react-i18next';

import type { LocalizedString } from '../../../shared/api/bootstrap';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import type { BlockComponentProps } from '../../../shared/modules';
import { MarkdownContent } from '../../../shared/ui';

/**
 * `flightops.errorCatalog` (design M2 §5.3, §8.2): the errors the division made public, by weight, each with its
 * description, its examples and the general rules it belongs to. Always live, and with no property: the catalogue is the
 * division's, whole. Its other half is `ErrorCatalogProvider` on the server, and the descriptor in `FlightOpsModule`.
 */

/** What `ErrorCatalogProvider` answers with. */
export interface ErrorCatalogData {
  items?: {
    id: number;
    name: LocalizedString | null;
    description: LocalizedString | null;
    examples: LocalizedString | null;
    category: 'Info' | 'Warning' | 'Dangerous';
    yearlyMax: number | null;
    rules: { code: string; title: LocalizedString | null }[];
  }[];
}

export function ErrorCatalogBlock({ data }: BlockComponentProps) {
  const { t } = useTranslation();
  const read = useLocalized();
  const items = (data as ErrorCatalogData | null | undefined)?.items;

  if (items === undefined || items.length === 0) {
    return (
      <p className="text-muted-foreground text-sm">
        {items === undefined ? t('common.loading') : t('flightops:blocks.errorCatalog.empty')}
      </p>
    );
  }

  return (
    <ul className="flex flex-col divide-y">
      {items.map((error) => {
        const examples = error.examples === null ? '' : read(error.examples);

        return (
          <li key={error.id} className="flex flex-col gap-2 py-4">
            <div className="flex flex-wrap items-baseline gap-x-3">
              <span className="font-semibold">{error.name === null ? '' : read(error.name)}</span>
              <span className="text-muted-foreground text-sm">
                {error.category === 'Warning' && error.yearlyMax !== null
                  ? t('flightops:blocks.errorCatalog.warningMax', { count: error.yearlyMax })
                  : t(`flightops:tourErrors.options.category.${error.category}`)}
              </span>
            </div>
            {error.description === null ? null : <MarkdownContent source={read(error.description)} />}
            {examples === '' ? null : (
              <div className="flex flex-col gap-1">
                <span className="text-sm font-semibold">{t('flightops:blocks.errorCatalog.examples')}</span>
                <MarkdownContent source={examples} />
              </div>
            )}
            {error.rules.length === 0 ? null : (
              <p className="text-muted-foreground text-sm">
                {t('flightops:blocks.errorCatalog.rules', {
                  rules: error.rules
                    .map((rule) => `${rule.code}${rule.title === null ? '' : ` ${read(rule.title)}`}`)
                    .join(' · '),
                })}
              </p>
            )}
          </li>
        );
      })}
    </ul>
  );
}
