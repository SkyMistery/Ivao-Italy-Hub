/**
 * The address a file of the library is read at, built from its identifier alone.
 *
 * The server builds the same address in `MediaUrl`, and a row of a list carries it ready made. A
 * block cannot use that one: what a published page holds is the identifier, and the public renderer
 * is handed the body and nothing else — no row, no metadata, no second request per picture.
 *
 * The last segment is the file name, which the server treats as decoration: the identifier is what
 * decides which row is served, so `file` is a name that always works. It is what a browser would
 * call the file if a reader saved it, which is the only thing it changes.
 */
export function mediaFileUrl(id: number): string {
  return `/media/${id}/file`;
}
