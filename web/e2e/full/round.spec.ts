import { expect, test } from '@playwright/test';

import { englishCommon, englishSeed } from '../locales';
import {
  addBlock,
  choose,
  department,
  metadata,
  properties,
  readInEnglish,
  selectBlock,
  signIn,
  whileWaitingFor,
  writeInBothLanguages,
} from './bench';

/**
 * The round the product exists for, in a browser, against the real API: a member of staff creates a
 * page from a template, adds blocks of three different families, publishes, and a visitor who is
 * nobody reads what was published. Then the draft moves on and the visitor's page does not.
 *
 * It is the first debt of handoff §10 and it is here on purpose *early* in M1: from here on every
 * new screen leans on this net, and a net written after twenty screens is a net nobody writes.
 *
 * Two traps, both already known (design M1 §11.2): the badge on a data block is drawn for staff
 * only, so its absence in the anonymous window is correct; and once the draft has been edited past
 * the published version the two renderings **must** differ, which is the last act here.
 */

const content = englishCommon.content;
const blocks = englishCommon.blocks;

/** A page of its own per run: the bench database is not thrown away between runs. */
const slug = `bench-round-${Date.now().toString(36)}`;

const heading = { en: 'What this bench published', it: 'Che cosa ha pubblicato il banco' };
const paragraph = { en: 'Published from the bench.', it: 'Pubblicato dal banco.' };
const callout = { en: 'Read this before flying', it: 'Da leggere prima di volare' };
const edited = { en: 'The draft moved on', it: 'La bozza è andata avanti' };

test('from a template to a page a visitor can read, and a draft that stays private', async ({
  page,
  context,
  browser,
}) => {
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  // ---------------------------------------------------------------- create from a template
  await page.goto(`/staff/${department}/content`);

  await choose(page, content.fields.template, englishSeed.seed.templates.sectionPage!.title);
  await page.getByPlaceholder(content.slugPlaceholder).fill(slug);

  // The picker's own create is a button; the two others on this screen with the same words are
  // links to the empty editor, which is a different thing entirely.
  await whileWaitingFor(page, 'POST', '/api/content/from-template/', async () => {
    await page.getByRole('button', { name: content.create, exact: true }).click();
  });

  await expect(page).toHaveURL(new RegExp(`/staff/${department}/content/\\d+$`));

  // ---------------------------------------------------------------- make it public
  // A page born from a template is visible to staff only until somebody decides otherwise, which
  // is the right default and the reason a visitor sees nothing until this line has run.
  await choose(page, content.fields.visibility, content.options.visibility.Public, metadata(page));

  // ---------------------------------------------------------------- three families of block
  await addBlock(page, content.editor.addBlock, blocks.heading.label);
  await choose(page, blocks.heading.fields.level, blocks.heading.options.level['2']!, properties(page));
  await writeInBothLanguages(properties(page), blocks.heading.fields.text, 'text', heading);
  await properties(page).getByRole('button', { name: content.editor.applyBlock }).click();

  await addBlock(page, content.editor.addBlock, blocks.text.label);
  await writeInBothLanguages(properties(page), blocks.text.fields.markdown, 'markdown', paragraph);
  await properties(page).getByRole('button', { name: content.editor.applyBlock }).click();

  await addBlock(page, content.editor.addBlock, blocks.callout.label);
  await choose(page, blocks.callout.fields.tone, blocks.callout.options.tone.info!, properties(page));
  await writeInBothLanguages(properties(page), blocks.callout.fields.title, 'title', callout);
  await writeInBothLanguages(properties(page), blocks.callout.fields.text, 'text', paragraph);
  await properties(page).getByRole('button', { name: content.editor.applyBlock }).click();

  // ---------------------------------------------------------------- save, then publish
  await whileWaitingFor(page, 'PUT', '/api/content/', async () => {
    await metadata(page).getByRole('button', { name: content.editor.saveDraft }).click();
  });

  const publish = page.getByRole('button', { name: content.editor.publish });
  await expect(publish).toBeEnabled();
  await whileWaitingFor(page, 'POST', '/publish', async () => {
    await publish.click();
  });

  // ---------------------------------------------------------------- a visitor reads it
  const visitor = await browser.newContext();
  await readInEnglish(visitor);
  const publicPage = await visitor.newPage();

  await publicPage.goto(`/${slug}`);
  await expect(publicPage.getByRole('heading', { name: heading.en })).toBeVisible();
  await expect(publicPage.getByText(callout.en)).toBeVisible();

  // Nobody is signed in here, so the words of the back office have no business being on screen.
  await expect(publicPage.getByRole('button', { name: content.editor.publish })).toHaveCount(0);

  // ---------------------------------------------------------------- the draft moves on
  // Back to the page, which is what a coordinator does when they return to change something, and
  // what this suite has to do for a reason worth knowing: publishing moves the row's version on,
  // and the editor picks the new one up when React next renders. Editing in the same millisecond
  // the publish call returns saves against the version from before it and is answered 409 —
  // correctly. A person cannot type that fast; a test can, and would be reporting its own speed.
  await page.reload();
  await selectBlock(page, blocks.callout.label);

  await writeInBothLanguages(properties(page), blocks.callout.fields.title, 'title', edited);
  await properties(page).getByRole('button', { name: content.editor.applyBlock }).click();
  await whileWaitingFor(page, 'PUT', '/api/content/', async () => {
    await metadata(page).getByRole('button', { name: content.editor.saveDraft }).click();
  });

  // And the visitor's page does not, because publishing is a separate act. This is the assertion
  // the whole phase is for: it is what "the public reads the published version" means.
  await publicPage.reload();
  await expect(publicPage.getByText(callout.en)).toBeVisible();
  await expect(publicPage.getByText(edited.en)).toHaveCount(0);

  await visitor.close();
});

test('a draft nobody published is not there for a visitor', async ({ page, context, browser }) => {
  await readInEnglish(context);
  await signIn(context);

  const draftSlug = `bench-draft-${Date.now().toString(36)}`;

  await page.goto(`/staff/${department}/content`);
  await choose(page, content.fields.template, englishSeed.seed.templates.sectionPage!.title);
  await page.getByPlaceholder(content.slugPlaceholder).fill(draftSlug);
  await whileWaitingFor(page, 'POST', '/api/content/from-template/', async () => {
    await page.getByRole('button', { name: content.create, exact: true }).click();
  });
  await expect(page).toHaveURL(new RegExp(`/staff/${department}/content/\\d+$`));

  await choose(page, content.fields.visibility, content.options.visibility.Public, metadata(page));
  await whileWaitingFor(page, 'PUT', '/api/content/', async () => {
    await metadata(page).getByRole('button', { name: content.editor.saveDraft }).click();
  });

  // Public visibility and never published: the two are different questions and only one of them
  // has been answered.
  const visitor = await browser.newContext();
  await readInEnglish(visitor);
  const publicPage = await visitor.newPage();
  const response = await publicPage.goto(`/${draftSlug}`);

  // The shell is served — that is the SPA fallback doing its job — and the page inside it says the
  // address does not exist, which is what a draft is to a visitor.
  expect(response?.status()).toBe(200);
  await expect(publicPage.getByRole('heading', { name: englishCommon.notFound.title })).toBeVisible();

  // And the words the template would have put on screen are nowhere, which is the half that
  // actually fails when a draft leaks: the first version of this test asserted the absence of a
  // heading that is not on a public page in either case, and stayed green while the draft was
  // published on purpose to check it.
  await expect(publicPage.getByText(englishSeed.seed.templates.sectionPage!.hero.heading)).toHaveCount(0);

  await visitor.close();
});

test('the application serves its own deep addresses, which no static server does', async ({ request }) => {
  // The check that says at once which side a failure is on. Serving the published package with
  // something that only knows files answers 404 here, and every back office test then fails for a
  // reason that has nothing to do with the build (handoff, "Il tag").
  const deep = await request.get(`/staff/${department}/content`);

  expect(deep.status()).toBe(200);
  expect(deep.headers()['content-type']).toContain('text/html');
});
