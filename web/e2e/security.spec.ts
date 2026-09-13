import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type Page } from '@playwright/test';

import { siteStaffBootstrap, stubTheApi, stubTheApiAsStaff } from './fixtures';

/**
 * The headers every response carries, and whether the application can live under them.
 *
 * ⚠️ Why this is an end to end test and not a unit one. A policy is only real in a browser: what
 * breaks under it is a style the design system applies at run time, a script a bundler inlined, a
 * font from somewhere nobody remembered. None of that is visible in a string comparison, and — this
 * is the part that matters — **a blocked stylesheet fails no assertion**: the palette still opens,
 * the test still passes, and the page is quietly wrong. So this watches the console and fails on a
 * violation, which is the only way that particular fault reports itself.
 *
 * The preview server sends the policy out of the same `config/security.json` the backend reads
 * (`vite.config.ts`), so every other test of this suite runs under it too. This one is what says so.
 */

interface SecurityConfiguration {
  readonly headers: Record<string, string>;
  readonly contentSecurityPolicy: {
    readonly enabled: boolean;
    readonly directives: Record<string, readonly string[]>;
  };
}

const security = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../config/security.json', import.meta.url)), 'utf8'),
) as SecurityConfiguration;

/** Collects what the browser refused, so a violation can fail a test instead of scrolling past. */
function watchForViolations(page: Page): string[] {
  const refused: string[] = [];

  page.on('console', (message) => {
    const text = message.text();
    if (/content security policy/i.test(text)) {
      const where = message.location();
      refused.push(`${text.slice(0, 160)} (${where.url}:${where.lineNumber})`);
    }
  });

  return refused;
}

test('every response carries the headers the configuration names', async ({ request }) => {
  const answer = await request.get('/');

  for (const [name, value] of Object.entries(security.headers)) {
    expect(answer.headers()[name.toLowerCase()], `${name} is missing`).toBe(value);
  }

  const policy = answer.headers()['content-security-policy'];
  expect(policy, 'no content security policy on the page').toBeTruthy();

  // Directive by directive rather than as one string: the two halves compose the same policy and
  // neither is obliged to write it in the same order.
  for (const [directive, sources] of Object.entries(security.contentSecurityPolicy.directives)) {
    expect(policy, `${directive} is missing`).toContain(`${directive} ${sources.join(' ')}`);
  }
});

test('the policy refuses what it should refuse', () => {
  const directives = security.contentSecurityPolicy.directives;

  // The one that would make the rest decoration. `'unsafe-inline'` on styles is measured and
  // accepted (the note of 12 September says why: React and Atmosphere apply styles at run time);
  // on scripts it would mean an injected string can run, which is the whole thing being prevented.
  expect(directives['script-src']).not.toContain("'unsafe-inline'");
  expect(directives['script-src']).not.toContain("'unsafe-eval'");
  expect(directives['object-src']).toEqual(["'none'"]);
  expect(directives['base-uri']).toEqual(["'none'"]);
  expect(directives['frame-ancestors']).toEqual(["'none'"]);
});

test('the public site draws itself without a single refusal', async ({ page }) => {
  const refused = watchForViolations(page);
  await stubTheApi(page);

  for (const path of ['/', '/about', '/news', '/documents', '/calendar', '/search?q=hangar']) {
    await page.goto(path);
    await page.locator('main').first().waitFor({ state: 'visible' });
  }

  expect(refused, refused.join('\n')).toEqual([]);
});

test('the back office and the editor draw themselves without a single refusal', async ({ page }) => {
  const refused = watchForViolations(page);
  await stubTheApiAsStaff(page, siteStaffBootstrap);

  for (const path of ['/staff/wd', '/staff/wd/menu', '/staff/links', '/staff/content']) {
    await page.goto(path);
    await page.locator('h1').first().waitFor({ state: 'visible' });
  }

  // A portal, because a component that draws itself somewhere else in the document is where a
  // run time style comes from: the select whose popup this hub already had to correct once.
  const select = page.locator('[role="combobox"]').first();
  if (await select.isVisible().catch(() => false)) {
    await select.click();
    await page.waitForTimeout(300);
  }

  expect(refused, refused.join('\n')).toEqual([]);
});
