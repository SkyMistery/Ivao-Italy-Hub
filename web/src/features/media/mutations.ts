import { useMutation, useQueryClient } from '@tanstack/react-query';

import type { Department, LocalizedString } from '../../shared/api/bootstrap';
import { api, unwrap, unwrapEmpty } from '../../shared/api/client';

import { mediaItemKey, mediaKey, type MediaDetailDto, type MediaWriteDto } from './queries';
import type { MediaFormValues } from './schema';

/**
 * Writing a media. The metadata are an ordinary payload; the upload is the one call of this hub
 * that is not JSON, and this is the one place that knows it (design M1 §2).
 */

/** A translated field with nothing written in it is absent, not an object full of empty strings. */
function trimLocalized(value: Record<string, string>): LocalizedString | null {
  const written = Object.entries(value).filter(([, text]) => text.trim().length > 0);
  return written.length === 0 ? null : Object.fromEntries(written);
}

export function toWriteDto(values: MediaFormValues): MediaWriteDto {
  return {
    ownerDepartment: values.ownerDepartment,
    visibility: values.visibility,
    // Sent as it stands, so the server can say which language is missing rather than being handed
    // a field that quietly became null.
    alt: values.alt,
    title: trimLocalized(values.title),
    category: values.category.trim() === '' ? null : values.category.trim(),
    rowVersion: values.rowVersion,
  };
}

/** The form as an existing file fills it, with every language of the division present as a tab. */
export function toFormValues(media: MediaDetailDto, locales: readonly string[]): MediaFormValues {
  const spread = (value: LocalizedString | null | undefined): Record<string, string> =>
    Object.fromEntries(locales.map((locale) => [locale, value?.[locale] ?? '']));

  return {
    ownerDepartment: media.ownerDepartment,
    visibility: media.visibility,
    alt: spread(media.alt),
    title: spread(media.title),
    category: media.category ?? '',
    rowVersion: media.rowVersion,
  };
}

/** What the upload form carries: the bytes, and the two things a row cannot be born without. */
export interface MediaUpload {
  file: File;
  ownerDepartment: Department;
}

/**
 * The upload. It goes to the same address as every other create — `POST /api/media` — because a
 * second address for "make a media" would be a second way of doing the same thing; what makes it
 * different is the body, which is multipart because a file is not a JSON field.
 *
 * `bodySerializer` is how the typed client is told to hand the request a `FormData` and let the
 * browser write the boundary itself. Setting `Content-Type` by hand here is the classic way of
 * producing a multipart request nobody can parse.
 */
export function useUploadMedia() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ file, ownerDepartment }: MediaUpload): Promise<MediaDetailDto> => {
      const body = new FormData();
      body.set('file', file);
      body.set('ownerDepartment', ownerDepartment);

      return unwrap(
        await api.POST('/api/media', {
          // The contract types a multipart part as a string, which a `File` is not; the serializer
          // below is what actually leaves the browser, and it is handed the values unchanged.
          body: body as unknown as { file: string; ownerDepartment: Department },
          bodySerializer: () => body,
        }),
      );
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: mediaKey });
    },
  });
}

export function useUpdateMedia(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: MediaFormValues): Promise<MediaDetailDto> =>
      unwrap(
        await api.PUT('/api/media/{id}', {
          params: { path: { id: String(id) } },
          body: toWriteDto(values),
        }),
      ),
    onSuccess: async (media) => {
      queryClient.setQueryData(mediaItemKey(id), media);
      await queryClient.invalidateQueries({ queryKey: mediaKey });
    },
  });
}

export function useDeleteMedia() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/media/{id}', { params: { path: { id: String(id) } } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: mediaKey });
    },
  });
}
