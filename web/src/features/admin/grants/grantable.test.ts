import { describe, expect, it } from 'vitest';

import type { Bootstrap } from '../../../shared/api/bootstrap';

import { grantSchema } from './schema';

/**
 * Which permissions the form of a grant offers (M4, E10f, note 2026-10-01-chi-assegna-gli-award-con-un-grant): those of a
 * department, and a global one only when a grant may confer it — who assigns the awards. Any other global permission is refused
 * by the server, and a select that offered it would be offering a refusal.
 */
function bootstrapWith(permissions: Bootstrap['registries']['permissions']): Bootstrap {
  return { registries: { blocks: [], permissions } } as unknown as Bootstrap;
}

function offered(bootstrap: Bootstrap): string[] {
  return (grantSchema(bootstrap).shape.value.meta() as { choices: string[] }).choices;
}

describe('the permissions a grant may name', () => {
  it('are those of a department and Awards.Assign, never another global one', () => {
    const bootstrap = bootstrapWith([
      { name: 'Links.View', isGlobal: false, grantableAlthoughGlobal: false },
      { name: 'Links.Edit', isGlobal: false, grantableAlthoughGlobal: false },
      { name: 'Permissions.Manage', isGlobal: true, grantableAlthoughGlobal: false },
      { name: 'Awards.Assign', isGlobal: true, grantableAlthoughGlobal: true },
      { name: 'Admin.Access', isGlobal: true, grantableAlthoughGlobal: false },
    ]);

    expect(offered(bootstrap)).toEqual(['Links.View', 'Links.Edit', 'Awards.Assign']);
  });

  it('reads a list that does not say it as before: the ones of a department only', () => {
    // As a bootstrap stubbed before E10f carries it (web/e2e/permissions-fir-team.spec.ts).
    const bootstrap = bootstrapWith([
      { name: 'Links.View', isGlobal: false },
      { name: 'Permissions.Manage', isGlobal: true },
    ] as unknown as Bootstrap['registries']['permissions']);

    expect(offered(bootstrap)).toEqual(['Links.View']);
  });
});
