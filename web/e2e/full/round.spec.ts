import { expect, test } from '@playwright/test';

import { englishCommon, englishSeed } from '../locales';
import {
  addBlock,
  choose,
  createContent,
  department,
  metadata,
  openOutline,
  properties,
  readContent,
  readInEnglish,
  saveDraft,
  selectBlock,
  selectSection,
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
  // Where they land: the components are a bar on the left since 10 September 2026, and what they
  // are added to is whatever is selected. So the free section of the template is picked once, in
  // the outline; after that each block added is itself selected, and its section is still the one.
  await openOutline(page, content.editor.outline);
  await selectSection(page, englishSeed.seed.templates.sectionPage!.body!.section);

  // Back to the page, which is where what is written shows up: since G15 the properties apply as
  // they are typed, and there is no button to press. Reading the words on the page is how the
  // round knows they were applied before it saves — a wait on the thing itself, not on a clock.
  await page.getByRole('button', { name: content.editor.onThePage, exact: true }).click();
  const onThePage = page.getByRole('region', { name: content.editor.preview });

  await addBlock(page, blocks.subgroups.text, blocks.heading.label);
  await choose(page, blocks.heading.fields.level, blocks.heading.options.level['2']!, properties(page));
  await writeInBothLanguages(properties(page), blocks.heading.fields.text, 'text', heading);
  await expect(onThePage.getByRole('heading', { name: heading.en })).toBeVisible();

  await addBlock(page, blocks.subgroups.text, blocks.text.label);
  await writeInBothLanguages(properties(page), blocks.text.fields.markdown, 'markdown', paragraph);
  await expect(onThePage.getByText(paragraph.en)).toBeVisible();

  await addBlock(page, blocks.subgroups.text, blocks.callout.label);
  await choose(page, blocks.callout.fields.tone, blocks.callout.options.tone.info!, properties(page));
  await writeInBothLanguages(properties(page), blocks.callout.fields.title, 'title', callout);
  await writeInBothLanguages(properties(page), blocks.callout.fields.text, 'text', paragraph);
  await expect(onThePage.getByText(callout.en)).toBeVisible();

  // ---------------------------------------------------------------- save, then publish
  await whileWaitingFor(page, 'PUT', '/api/content/', async () => {
    await saveDraft(page, content.editor.saveDraft).click();
  });

  // Exact, since the frame has a "Published" toggle beside the draft, and "Publish" is in it.
  const publish = page.getByRole('button', { name: content.editor.publish, exact: true });
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
  // Back to the page, which is what a coordinator does when they return to change something.
  // ⚠️ This reload used to be explained as a race with the publish call, and that explanation was
  // wrong: until G12 the screen read its row from the route's loader, which runs on navigation and
  // never again, so **every** second save of a page load was answered 409. The reload was hiding
  // it. It stays because coming back to a page is what a person does, and the test below is what
  // actually guards the thing this comment used to claim.
  await page.reload();

  // And a reload puts the middle column back on the page, which is where it opens. The outline is
  // what `selectBlock` reads.
  await openOutline(page, content.editor.outline);
  await selectBlock(page, blocks.callout.label);
  await page.getByRole('button', { name: content.editor.onThePage, exact: true }).click();

  await writeInBothLanguages(properties(page), blocks.callout.fields.title, 'title', edited);
  await expect(onThePage.getByText(edited.en)).toBeVisible();
  await whileWaitingFor(page, 'PUT', '/api/content/', async () => {
    await saveDraft(page, content.editor.saveDraft).click();
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
    await saveDraft(page, content.editor.saveDraft).click();
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
  await expect(publicPage.getByText(englishSeed.seed.templates.sectionPage!.hero!.heading)).toHaveCount(0);

  await visitor.close();
});

test('the draft saves itself after a pause, and on the way out', async ({ page, context }) => {
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  const first = { en: 'Written before the pause', it: 'Scritto prima della pausa' };
  const paused = { en: 'Stored by the pause', it: 'Salvato dalla pausa' };
  const left = { en: 'Stored on the way out', it: 'Salvato uscendo' };

  const row = await createContent(context, {
    slug: `bench-autosave-${Date.now().toString(36)}`,
    title: { en: 'Saves itself', it: 'Si salva da sola' },
    body: {
      schemaVersion: 1,
      sections: [
        {
          id: 's_only',
          key: null,
          title: null,
          layout: 'stacked',
          background: 'none',
          padding: 'md',
          width: 'default',
          blocks: [{ id: 'b_only', type: 'heading', version: 1, props: { level: 2, text: first } }],
          sections: [],
        },
      ],
    },
  });

  await page.goto(`/staff/${department}/content/${row.id}`);
  const onThePage = page.getByRole('region', { name: content.editor.preview });

  // Picking the heading on the page opens its fields; writing in them applies at once (session 1)
  // and, ten seconds later, stores (session 2). The line under the toolbar says which of the two
  // has happened, and the round waits on the call and on the line, never on a clock of its own.
  await onThePage.getByRole('heading', { name: first.en }).click();
  await writeInBothLanguages(properties(page), blocks.heading.fields.text, 'text', paused);
  await expect(onThePage.getByRole('heading', { name: paused.en })).toBeVisible();
  // Named: the drag and drop context has a live region of its own on this screen.
  const status = page.getByRole('status', { name: content.editor.autosave.title });
  await expect(status).toHaveText(content.editor.autosave.unsaved);

  await whileWaitingFor(page, 'PUT', '/api/content/', async () => {
    await expect(status).toHaveText(
      new RegExp(`^${content.editor.autosave.saved.replace('{{time}}', '\\d\\d:\\d\\d')}$`, 'u'),
      { timeout: 15_000 },
    );
  });

  expect(JSON.stringify((await readContent(context, row.id)).body)).toContain(paused.en);

  // Then the way out: written again, and the page left through the application before any pause.
  // The blocker stores first and lets the navigation through; nothing is asked.
  await writeInBothLanguages(properties(page), blocks.heading.fields.text, 'text', left);
  await expect(onThePage.getByRole('heading', { name: left.en })).toBeVisible();

  await whileWaitingFor(page, 'PUT', '/api/content/', async () => {
    await page.getByRole('link', { name: content.title, exact: true }).first().click();
  });

  await expect(page).toHaveURL(new RegExp(`/staff/${department}/content(\\?.*)?$`));
  expect(JSON.stringify((await readContent(context, row.id)).body)).toContain(left.en);
});

test('the application serves its own deep addresses, which no static server does', async ({ request }) => {
  // The check that says at once which side a failure is on. Serving the published package with
  // something that only knows files answers 404 here, and every back office test then fails for a
  // reason that has nothing to do with the build (handoff, "Il tag").
  const deep = await request.get(`/staff/${department}/content`);

  expect(deep.status()).toBe(200);
  expect(deep.headers()['content-type']).toContain('text/html');
});
test('a page is saved twice from one page load, with no reload in between', async ({ page, context }) => {
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  const row = await createContent(context, {
    slug: `bench-twice-${Date.now().toString(36)}`,
    title: { en: 'Saved once', it: 'Salvata una volta' },
    body: { schemaVersion: 1, sections: [] },
  });

  await page.goto(`/staff/${department}/content/${row.id}`);
  const save = saveDraft(page, content.editor.saveDraft);

  // The slug rather than a translated field: one plain input, no language tabs, and still a real
  // change that the server has to store.
  for (const slug of [`${row.slug}-a`, `${row.slug}-b`]) {
    await metadata(page).locator('#slug').fill(slug);

    // `whileWaitingFor` asserts the status, so a 409 fails here and says which call it was. That is
    // the whole test: until G12 the second save carried the `rowVersion` from when the page opened,
    // and the server was right to refuse it (decision `2026-09-07-il-loader-non-e-la-riga.md`).
    await whileWaitingFor(page, 'PUT', '/api/content/', async () => {
      await save.click();
    });
  }

  // And the row really moved twice, rather than the screen merely not complaining.
  expect((await readContent(context, row.id)).slug).toBe(`${row.slug}-b`);
});
