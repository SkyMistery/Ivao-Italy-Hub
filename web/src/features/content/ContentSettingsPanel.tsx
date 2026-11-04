import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { Settings } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { SchemaForm } from '../../shared/forms';
import { useLocalized } from '../../shared/i18n/useLocalized';
import { Dialog, useNotice } from '../../shared/ui';

import {
  contentSettingsQuery,
  contentSettingsSchema,
  toSettingsForm,
  useSaveContentSettings,
} from './contentSettings';
import { siteAddressesQuery } from './queries';

/**
 * «Settings» on the screen of the content (`decisions/2026-11-04-la-pagina-dello-stile.md`): the settings that hold for the
 * whole division, for whoever holds `Content.ManageTemplates` and reaches every department (Carmine, 4 November 2026,
 * «Solo chi ha tutti»). One today — the page of the
 * division's style, which the instructions «Download for Claude» end with.
 *
 * A `Dialog` of the closed list with the generated form, like «New collection». The pages offered are the published pages
 * the whole staff reads: a page of one department is refused by the server too (`errors.content.settings.stylePageTooNarrow`),
 * because whoever downloads may be of another one.
 */
export function ContentSettingsPanel({
  locales,
  division,
}: {
  locales: readonly string[];
  division: { defaultLocale: string; timezone: string };
}) {
  const { t } = useTranslation();
  const read = useLocalized();
  const notice = useNotice();
  const [open, setOpen] = useState(false);

  const settings = useQuery({ ...contentSettingsQuery, enabled: open });
  const pages = useQuery({ ...siteAddressesQuery(), enabled: open });
  const save = useSaveContentSettings();

  // The pages online the whole staff reads, of every department (`GET /api/content/addresses`, published only).
  const choices = (pages.data ?? [])
    .filter((page) => page.kind === 'Page' && page.visibility !== 'Department')
    .map((page) => ({ value: String(page.id), label: `${read(page.title) || page.url} — ${page.url}` }));

  return (
    <Dialog
      open={open}
      onOpenChange={setOpen}
      trigger={
        <Button type="button" variant="ghost">
          <Settings aria-hidden className="mr-2 size-4" />
          {t('content.settings.open')}
        </Button>
      }
      title={t('content.settings.title')}
      description={t('content.settings.description')}
    >
      {/* Mounted once both reads are in, so the form starts from the value that is stored and not from "none". */}
      {open && settings.data !== undefined && pages.data !== undefined ? (
        <SchemaForm
          schema={contentSettingsSchema(choices, t('content.settings.none'))}
          defaults={toSettingsForm(settings.data)}
          locales={locales}
          labels="content.settings"
          division={division}
          submitLabel={t('common.save')}
          onSubmit={async (values) => {
            await save.mutateAsync(values);
            notice({ tone: 'success', title: t('content.settings.saved') });
            setOpen(false);
          }}
        />
      ) : null}
    </Dialog>
  );
}
