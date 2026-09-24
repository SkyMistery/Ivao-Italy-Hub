## What this pull request does

<!-- One paragraph. Link the phase or the design section it implements. -->

## Checklist

Answer honestly: a "yes" is not a rejection, it is a decision that has to be justified.

- [ ] **Did I add a `*_translations` table?** Translated fields are a JSON column mapped to
      `Localized<T>`. If yes, say why and which section of the plan allows it.
- [ ] **Did I add an authorization handler?** There is exactly one,
      `DepartmentAuthorizationHandler`. A new permission is a name in the catalogue, not a handler.
- [ ] **Did I write a `fetch` by hand?** All calls go through the generated client in
      `web/src/shared/api`.
- [ ] **Did I write a list or a form that is not generated?** Back office screens come from
      `DataList` and `SchemaForm` on the client and from `MapCrud` on the server.
- [ ] **Did I add a UI component outside the closed list?** See `docs/UI-GUIDELINES.md`.
- [ ] **Did I add a foreign key between two `DbContext`s?** Contexts only share unconstrained
      `vid` / `icao` columns.
- [ ] **Did I call SMTP directly from a module?** Modules publish notification intents.
- [ ] **Did I reference one module from another?** Modules talk to each other through the core.
- [ ] **Do the backbone tests still pass?** Interceptor, authorization handler, `IProjectable`,
      `Localized<T>`.
- [ ] **Is there a user visible string in the code?** Every one of them is an i18n key.
- [ ] **Did this change require a decision?** Then the plan has a new version and a changelog line,
      and there is a note under `docs/internal/decisions/`.

## How it was verified

<!-- Commands, tests, manual checks. -->

## For the reviewer

<!-- Required on every pull request that is not the maintainer's (CLAUDE.md sections 0 and 9). The reviewer reads
     the branch with this in hand; a pull request without it goes back. Be precise: file paths, not adjectives. -->

- **Phase and design**: <!-- e.g. "L3 of 08-piano-implementazione-m3.md, implements 07-design-m3.md §4.2–§4.4" -->
- **Decisions**: <!-- each new note under docs/internal/decisions/, and the link to the maintainer's comment that
  answered it. "None" is a valid answer. A decision no maintainer answered is not a decision: say so. -->
- **Core touched**: <!-- every file outside the module (the core-guard check lists them), and for each one which
  mechanism it extends and why the module could not do without. "No" is the answer we hope for. -->
- **Mechanisms used**: <!-- which generic mechanisms the module relies on (MapCrud, IProjectable, grants,
  Data blocks, notification intents…) and where. -->
- **Deviations from the design**: <!-- and where "Com'è andata" records them. -->
- **Commands run and their result**: <!-- the exact commands: build, unit tests, the whole integration suite with no
  filter, pnpm lint/typecheck/test, pnpm e2e:full. Paste the counts. -->
- **Not verified**: <!-- what you did not run, could not run, or only inferred. Nothing is a bad answer here. -->
- **HANDOFF-M3.md**: <!-- updated with "Che cosa ha lasciato <phase>": yes / no, why -->

