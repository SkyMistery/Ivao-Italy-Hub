import type { TFunction } from 'i18next';

/**
 * A person as the pages of the hub name them: the VID that always is, and the name the hub has for them — `null` when they
 * never signed in. It is the shape of every module's member (the tours' `MemberDto`, the training's `TrainingMemberDto`), so a
 * page hands one over just as the server sent it.
 */
export interface NamedPerson {
  readonly vid: number;
  readonly name: string | null;
}

/**
 * Whether a VID is somebody whose data was erased: the core writes a negative number in their place, new for every erasure and
 * tied to nobody (note `2026-09-25-la-cancellazione-dei-dati-di-una-persona`, answer 1). There is nobody left to link to, so a
 * page asks this before it draws a link to a person — the path of a trainee, the page of a pilot (note
 * `2026-09-29-la-persona-cancellata-nel-nucleo`).
 */
export function isErased(vid: number): boolean {
  return vid < 0;
}

/**
 * A person as a page or a list writes them: "Deleted person" for a pseudonym, never its number; the name with the VID when the
 * hub has a name; the VID alone when it has none. One sentence for every module, so no page writes its own.
 * <br />The word is `people.deleted`: `people.erased` is the tours' copy (`flightops:people.erased`), and a key the core declares
 * may not be declared again by a module (`LocaleCatalog`).
 */
export function personName(person: NamedPerson, t: TFunction): string {
  if (isErased(person.vid)) {
    return t('people.deleted');
  }

  return person.name === null || person.name === ''
    ? String(person.vid)
    : `${person.name} (${String(person.vid)})`;
}
