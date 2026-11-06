import i18next from 'i18next';
import { describe, expect, it } from 'vitest';

import english from '../../../../../locales/en/common.json';
import italian from '../../../../../locales/it/common.json';
import type { Bootstrap } from '../../../shared/api/bootstrap';
import { DEPARTMENTS } from '../../../shared/api/department';
import { readFields, type FieldNode } from '../../../shared/forms';

import { grantSchema, STAFF_LEVELS } from './schema';

/**
 * The words of the form of a grant. `SchemaForm` builds these keys at run time from the path of a field and the value of a
 * choice, so `pnpm i18n:check` cannot see them, and a missing one does not throw: i18next draws the key itself. The two selects
 * of a department offered `grants.options.department.FOD` that way to whoever opened the form.
 */
const schema = grantSchema({ registries: { blocks: [], permissions: [] } } as unknown as Bootstrap);

const drawn = readFields(schema).filter((node) => node.meta.hidden !== true);

/** What the form asks the language files for a field: its label and, for a select of an enum, one word per choice. */
function keysOf(node: FieldNode): string[] {
  return [
    `grants.fields.${node.path}`,
    ...(node.kind === 'enum'
      ? [
          ...node.options.map((option) => `grants.options.${node.path}.${option}`),
          ...(node.optional ? [`grants.options.${node.path}.none`] : []),
        ]
      : []),
  ];
}

describe.each([
  ['en', english],
  ['it', italian],
])('the form of a grant in %s', (language, words) => {
  const i18n = i18next.createInstance();
  void i18n.init({ lng: language, initAsync: false, resources: { [language]: { translation: words } } });

  const missing = (keys: readonly string[]) =>
    keys.filter((key) => !i18n.exists(key) || typeof i18n.t(key) !== 'string');

  it('has a word for every field and every choice it draws', () => {
    expect(missing(drawn.flatMap(keysOf))).toEqual([]);
  });

  // By name as well as by walking the form: if one of the two stopped being an enum of the departments, the walk above would
  // just have nothing to ask for.
  it.each(['department', 'positionDepartment'])('names every department in %s', (field) => {
    expect(missing(DEPARTMENTS.map((department) => `grants.options.${field}.${department}`))).toEqual([]);
  });

  // The route hands these over already translated, from a key of the same family.
  it('names every level of a position', () => {
    expect(missing(STAFF_LEVELS.map((level) => `grants.options.positionLevels.${level}`))).toEqual([]);
  });
});
