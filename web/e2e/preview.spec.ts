import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

import { expect, test, type Page } from '@playwright/test';

/**
 * The local preview of an interactive block, opened the way it is meant to be opened: **from a
 * disk**, with no server behind it (12 September 2026, `decisions/2026-09-12-il-blocco-interattivo.md`).
 *
 * It exists because an assistant writing a fragment could not see it — `HUB` belongs to the shell,
 * so a fragment opened on its own stops at its first line — and built itself a standalone page that
 * copied the shell by hand. This file is the one copy that cannot drift, because the hub generates
 * it from the shell it serves; what is asserted here is that it works as that assistant's did, and a
 * little more: the three grounds, the two languages, reduced movement, and the refusals listed.
 *
 * ⚠️ Composed in the test the way `EmbedEndpoints` composes it — the shell as a JSON string with `<`,
 * `>` and `&` escaped, which is what `JsonSerializer` does by default. Written raw, the shell's own
 * `</script>` would end the preview's script halfway, and `EmbedFrameTests` asserts the server does
 * not do that.
 */

function readRepositoryFile(path: string): string {
  return readFileSync(fileURLToPath(new URL(`../../${path}`, import.meta.url)), 'utf8');
}

function composedPreview(): string {
  const shell = readRepositoryFile('src/IvaoHub.Core/Content/EmbedShell.html');
  const escaped = JSON.stringify(shell)
    .replace(/</g, '\\u003C')
    .replace(/>/g, '\\u003E')
    .replace(/&/g, '\\u0026');

  return readRepositoryFile('src/IvaoHub.Core/Content/EmbedPreview.html').replace('{{shellJson}}', escaped);
}

/** Written once per worker into a folder of its own, and opened as a file — no server anywhere. */
function previewFile(): string {
  const folder = mkdtempSync(join(tmpdir(), 'hub-preview-'));
  const file = join(folder, 'interactive-preview.html');
  writeFileSync(file, composedPreview(), 'utf8');
  return pathToFileURL(file).href;
}

/** A fragment that says, in words, everything the preview claims to switch. */
const PROBE = `
<svg viewBox="0 0 200 60" width="100%"><rect x="20" y="25" width="160" height="10" fill="var(--ink-quiet)"></rect></svg>
<button type="button" id="choice"></button>
<p id="ground"></p>
<p id="motion"></p>
<script>
  function draw() {
    document.getElementById('choice').textContent = HUB.t({ en: 'Left hand circuit', it: 'Circuito sinistro' });
    document.getElementById('ground').textContent = HUB.dark ? 'dark' : 'light';
    document.getElementById('motion').textContent = HUB.reducedMotion ? 'reduced' : 'full';
  }
  window.addEventListener('hub:theme', draw);
  draw();
</script>
`;

async function showFragment(page: Page, fragment: string) {
  await page.getByLabel('The fragment').fill(fragment);
  await page.getByRole('button', { name: 'Show' }).click();
}

test('the preview runs a fragment the way the hub does, and switches what the hub would switch', async ({
  page,
}) => {
  await page.goto(previewFile());
  await showFragment(page, PROBE);

  const frame = page.frameLocator('#frame');

  // Italian first, because this division's default language is — and the word comes from `HUB.t`,
  // which is the proof that the shell is really around the fragment.
  await expect(frame.locator('svg')).toBeVisible();
  await expect(frame.getByRole('button', { name: 'Circuito sinistro' })).toBeVisible();

  await page.getByRole('button', { name: 'English' }).click();
  await expect(frame.getByRole('button', { name: 'Left hand circuit' })).toBeVisible();

  // The ground reaches inside by the same message the hub sends.
  await page.getByRole('button', { name: 'Dark section' }).click();
  await expect(frame.locator('#ground')).toHaveText('dark');

  await page.getByRole('button', { name: 'Reduced' }).click();
  await expect(frame.locator('#motion')).toHaveText('reduced');

  // The sandbox is the real one: this document cannot read the page it is shown in.
  const reach = await page.frames()[1]!.evaluate(() => {
    try {
      return String(parent.document.title);
    } catch {
      return 'refused';
    }
  });
  expect(reach).toBe('refused');
});

test('what the hub would refuse, the preview says out loud', async ({ page }) => {
  await page.goto(previewFile());

  // The network, which the shell's own policy closes here exactly as it does in the hub.
  await showFragment(
    page,
    `<p>tries</p><script>fetch('https://example.org/nothing').catch(function () {});</script>`,
  );
  await expect(page.locator('#refusals')).toContainText('refused something the fragment asked for');

  // And a whole page instead of a fragment, which the editor refuses before it gets anywhere.
  await showFragment(page, '<!doctype html><html><body><svg /></body></html>');
  await expect(page.locator('#refusals')).toContainText('whole page, not a fragment');
});
