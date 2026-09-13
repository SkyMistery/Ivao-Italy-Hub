import { createContext, useCallback, useContext } from 'react';

/**
 * The address a file of the library is read at, built from its identifier.
 *
 * The server builds the same address in `MediaUrl`, and a row of a list carries it ready made. A
 * block cannot use that one: what a published page holds is the identifier, and the public renderer
 * is handed the body and nothing else — no row, no metadata, no second request per picture.
 *
 * With the fingerprint of the file (G20) the address is `/media/{id}/{fingerprint}/file`, which the
 * server lets a cache keep for a year: the fingerprint changes when the file is replaced on the same
 * row. Without one it is `/media/{id}/file`, which the server serves asking the cache to check again
 * — never stale, only slower. The last segment is the file name, which the server treats as
 * decoration.
 */
export function mediaFileUrl(id: number, fingerprint?: string): string {
  return fingerprint === undefined || fingerprint === ''
    ? `/media/${id}/file`
    : `/media/${id}/${fingerprint}/file`;
}

/**
 * The fingerprints of the files a published page shows, by identifier, as the public answer carries
 * them (`PublicContentDto.media`). Empty everywhere else — the editor, a draft — where the address
 * of the identifier alone is the right one.
 */
export const MediaFingerprints = createContext<Readonly<Record<string, string>>>({});

/** `mediaFileUrl` with the fingerprint of the page being drawn, when it has one. */
export function useMediaFileUrl(): (id: number) => string {
  const fingerprints = useContext(MediaFingerprints);
  return useCallback((id: number) => mediaFileUrl(id, fingerprints[String(id)]), [fingerprints]);
}
