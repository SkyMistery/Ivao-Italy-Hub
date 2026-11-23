/**
 * A title turned into the address it proposes.
 *
 * The rule it aims at is the server's — `^[a-z0-9]+(?:-[a-z0-9]+)*$`, in `ContentWriteDtoValidator`
 * — and the comment there has said from the start that "the editor proposes one from the title".
 * This is that half, arriving late: until the demo of M1, an address had to be typed from scratch
 * next to a title that had just been written.
 *
 * It is a **proposal** and not a validation: nothing here refuses anything, the server does that
 * and answers, and what it answers reaches the field through `useProblemDetails` like every other
 * refusal (design M0 §7.5). The length limit of the column is not a rule of this function: it is handed
 * in by the field that knows it (`slugMaxLength`), because since design 13 §4.1 a slug may be proposed in
 * a field the writer cannot see, and a proposal too long would then be a refusal nobody can answer.
 *
 * Accents are folded rather than dropped: "Città" gives `citta`, which is what somebody typing the
 * address by hand would write, while dropping the letter would give `citt`.
 */
export function slugify(title: string, maxLength?: number): string {
  const slug = title
    .normalize('NFD')
    // Everything Unicode files as a combining mark: the accents that NFD has just separated.
    .replace(/\p{Mn}/gu, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');

  // Cut where the column ends, and never on a dash: `a-b-` is not an address the server takes.
  return maxLength === undefined ? slug : slug.slice(0, maxLength).replace(/-+$/, '');
}
