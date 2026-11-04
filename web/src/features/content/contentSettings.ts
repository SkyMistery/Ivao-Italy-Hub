import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';

import { allSections, type Body } from '../../blocks';
import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import type { ChoiceOption } from '../../shared/forms';

/**
 * The settings of the content (`decisions/2026-11-04-la-pagina-dello-stile.md`, decided by Carmine in chat to the master,
 * 4 November 2026): one key per setting in `hub_division_settings`, read and written at `/api/content/settings`. One today,
 * the page of the division's style that the instructions «Download for Claude» end with.
 */

export type ContentSettingsDto = components['schemas']['ContentSettingsDto'];

const settingsKey = ['content-settings'] as const;

/**
 * What the settings say, **for whoever asks**: the path of the page of the style is there only when they can read it
 * online, so a reader who cannot is handed instructions without it rather than a page they will never open.
 */
export const contentSettingsQuery = queryOptions({
  queryKey: settingsKey,
  queryFn: async (): Promise<ContentSettingsDto> => unwrap(await api.GET('/api/content/settings')),
});

/** The value of the select that means "no page". A select of Radix takes no empty value. */
export const NO_STYLE_PAGE = 'none';

/** The form of the settings panel: one select, the published pages the whole staff reads, or none. */
export function contentSettingsSchema(pages: readonly ChoiceOption[], none: string) {
  return z.object({
    stylePageId: z.string().meta({ choices: [{ value: NO_STYLE_PAGE, label: none }, ...pages] }),
  });
}

export type ContentSettingsFormValues = z.output<ReturnType<typeof contentSettingsSchema>>;

export function toSettingsForm(settings: ContentSettingsDto | undefined): ContentSettingsFormValues {
  return { stylePageId: settings?.stylePageId == null ? NO_STYLE_PAGE : String(settings.stylePageId) };
}

export function useSaveContentSettings() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: ContentSettingsFormValues): Promise<ContentSettingsDto> =>
      unwrap(
        await api.PUT('/api/content/settings', {
          body: { stylePageId: values.stylePageId === NO_STYLE_PAGE ? null : Number(values.stylePageId) },
        }),
      ),
    onSuccess: (settings) => {
      queryClient.setQueryData(settingsKey, settings);
    },
  });
}

/** Whether a value is a translated text: an object whose members are all strings, keyed by languages of the division. */
function isLocalized(value: unknown, locales: readonly string[]): value is Record<string, string> {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    return false;
  }

  const entries = Object.entries(value);
  return (
    entries.length > 0 &&
    entries.every(([key, member]) => locales.includes(key) && typeof member === 'string')
  );
}

/** Every translated text inside some props, in the order they were written, in one language. */
function proseOf(
  value: unknown,
  locales: readonly string[],
  read: (text: Record<string, string>) => string,
): string[] {
  if (isLocalized(value, locales)) {
    const text = read(value).trim();
    return text === '' ? [] : [text];
  }

  if (Array.isArray(value)) {
    return value.flatMap((entry) => proseOf(entry, locales, read));
  }

  if (value !== null && typeof value === 'object') {
    return Object.values(value).flatMap((member) => proseOf(member, locales, read));
  }

  return [];
}

/**
 * The page of the style as text, for the end of the instructions (note §2). Read in the browser, because the server never
 * reads `props`: a heading stays a heading, one level below the instructions' own section; a text is its Markdown; every
 * other translated text of a block is a paragraph. Pictures, links and data are not words about how to write.
 */
export function styleText(
  body: Body,
  locales: readonly string[],
  read: (text: Record<string, string>) => string,
): string {
  const parts: string[] = [];

  for (const section of allSections(body)) {
    for (const block of section.blocks) {
      if (block.type === 'heading') {
        const text = isLocalized(block.props.text, locales) ? read(block.props.text).trim() : '';
        const level = typeof block.props.level === 'number' ? block.props.level : 2;
        if (text !== '') {
          parts.push(`${'#'.repeat(Math.min(level + 1, 6))} ${text}`);
        }
        continue;
      }

      parts.push(...proseOf(block.props, locales, read));
    }
  }

  return parts.join('\n\n');
}
