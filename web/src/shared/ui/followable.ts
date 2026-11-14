/** The schemes an address written into the content may name: the web, a mail, a phone. */
const SCHEMES: readonly string[] = ['http:', 'https:', 'mailto:', 'tel:'];

/**
 * Whether an address written into the content may be drawn as a link: nothing yet, a path of this site, an anchor of the
 * page, or a whole address of the web, a mail or a phone (#321, point 5). The browser's half of `SiteDestinations.IsFollowable`,
 * which refuses the rest when a page is saved and published; this half is for what a pattern on the server cannot see, a link
 * of Markdown written in a form CommonMark allows and the pattern does not read. The two halves agree by the cases in
 * `tests/fixtures/content-addresses.json`, which the tests of both read.
 *
 * A path that starts with `//` or `/\`, or that holds a tab or a line break, leaves the site while looking like a path: the
 * browser reads `\` as `/` and drops the other two.
 */
export function isFollowable(address: string): boolean {
  if (address.trim() === '') {
    return true;
  }

  // eslint-disable-next-line no-control-regex -- the control characters are what is being looked for
  if (/[\u0000-\u001f\u007f-\u009f]/.test(address)) {
    return false;
  }

  if (address.startsWith('/')) {
    return address.length === 1 || (address[1] !== '/' && address[1] !== '\\');
  }

  if (address.startsWith('#')) {
    return true;
  }

  try {
    return SCHEMES.includes(new URL(address).protocol.toLowerCase());
  } catch {
    return false;
  }
}
