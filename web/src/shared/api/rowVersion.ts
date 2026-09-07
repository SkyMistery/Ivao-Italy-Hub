/**
 * The version a row that does not exist yet carries.
 *
 * Optimistic concurrency is a `DateTime` on the wire (`row_version`, a MariaDB `timestamp(6)`), and
 * a create has nothing to send for it. What every form used to send was the empty string, which is
 * not a date: the server refused the payload before any validator ran, so **creating a row from an
 * empty form was answered 400 by the real API** — for links, categories, pages and the menu alike.
 *
 * ⚠️ It survived M0 and five phases of M1 because nothing had ever done it against a real server in
 * a browser: the round of G0 creates a page from a template, which is a different endpoint, and the
 * back office smoke suite stubs the API. G8 was the first phase to submit an empty form to the
 * bench, and this is what it found (handoff §11: what nothing mounts, nothing tests).
 *
 * The value is the default of a `DateTime`, which is what "no version yet" means to the server and
 * what the integration tests have always sent. It lives here, once, because five copies of a
 * sentinel are five places for it to drift.
 */
export const NEW_ROW_VERSION = '0001-01-01T00:00:00';
