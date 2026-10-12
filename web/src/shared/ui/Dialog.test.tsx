import { readdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import italianCommon from '../../../../locales/it/common.json';
import { createTestI18n, renderWithProviders } from '../../test/harness';

import { Dialog } from './Dialog';

/**
 * The «X» of a dialog says its word in the language on screen (#224). Atmosphere's `Dialog` names it "Close", written
 * into the library, under the Italian interface too.
 */

async function open(language: 'en' | 'it', closeButton = true) {
  const i18n = createTestI18n();
  await i18n.changeLanguage(language);

  renderWithProviders(
    <Dialog
      trigger="Open"
      title="A slot"
      description="Departure from the first airport"
      closeButton={closeButton}
    >
      <p>What the dialog shows.</p>
    </Dialog>,
    { i18n },
  );

  await userEvent.setup().click(screen.getByRole('button', { name: 'Open' }));
  return screen.getByRole('dialog', { name: 'A slot' });
}

test('in Italian the «X» is named in Italian, and no English word is left in the dialog', async () => {
  const dialog = await open('it');

  expect(within(dialog).getByRole('button', { name: italianCommon.common.close })).toBeInTheDocument();
  expect(within(dialog).queryByRole('button', { name: /close/i })).not.toBeInTheDocument();
  expect(dialog).not.toHaveTextContent(/close/i);
});

test('in English the «X» says the word of the language file, and closes the dialog', async () => {
  const dialog = await open('en');

  await userEvent.setup().click(within(dialog).getByRole('button', { name: englishCommon.common.close }));

  expect(dialog).not.toBeInTheDocument();
});

test('the title, the description and what the dialog shows are drawn as Atmosphere’s did', async () => {
  const dialog = await open('en');

  expect(dialog).toHaveAccessibleDescription('Departure from the first airport');
  expect(within(dialog).getByText('What the dialog shows.')).toBeInTheDocument();
});

test('a dialog without its «X» draws none', async () => {
  const dialog = await open('it', false);

  expect(within(dialog).queryByRole('button')).not.toBeInTheDocument();
});

/**
 * Atmosphere's components that draw an «X» named "Close" in the library: its `Dialog` molecule, the «X» itself, and its
 * `Sheet` (whose content draws the same one). A screen of the hub draws the core's `Dialog` instead.
 */
const ATMOSPHERE_CLOSE = ['Dialog', 'DialogTopRightClose', 'Sheet', 'SheetContent'];

/**
 * The screens that may still draw Atmosphere's: the events', which move onto the core's `Dialog` in a phase of their own
 * after this one (rule 6: a core change and its uses in a module are two pull requests). The list only shrinks.
 *
 * ⚠️ `PrivateSlots.tsx` is not in `main` yet: it is E7's (#246), open beside this pull request, and draws Atmosphere's
 * `Dialog` too. It is named here so that neither pull request turns the other red, whichever is merged first — which is
 * also why the test asks for no screen outside the list, and not for the list itself.
 */
const NOT_MOVED_YET = ['modules/events/screens/EventSlots.tsx', 'modules/events/screens/PrivateSlots.tsx'];

test('no screen draws an «X» of Atmosphere’s, except the ones waiting to move', () => {
  const root = resolve(process.cwd(), 'src');
  const importsFromAtmosphere = /import\s*\{([^}]*)\}\s*from\s*'@ivao\/atmosphere-react'/g;

  const offenders = readdirSync(root, { withFileTypes: true, recursive: true })
    .filter((entry) => entry.isFile() && /\.tsx?$/.test(entry.name))
    .map((entry) => resolve(entry.parentPath, entry.name))
    .filter((file) =>
      [...readFileSync(file, 'utf8').matchAll(importsFromAtmosphere)].some((match) =>
        (match[1] ?? '')
          .split(',')
          // `type Dialog` and `Dialog as Something` are still Atmosphere's `Dialog`.
          .map((name) =>
            name
              .trim()
              .replace(/^type\s+/, '')
              .replace(/\s+as\s+\w+$/, ''),
          )
          .some((name) => ATMOSPHERE_CLOSE.includes(name)),
      ),
    )
    .map((file) =>
      file
        .slice(root.length + 1)
        .split('\\')
        .join('/'),
    );

  expect(offenders.filter((file) => !NOT_MOVED_YET.includes(file))).toEqual([]);
});
