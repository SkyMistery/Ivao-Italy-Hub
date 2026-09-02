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
