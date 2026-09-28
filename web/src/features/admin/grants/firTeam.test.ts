import { describe, expect, it } from 'vitest';

import { emptyGrant, toFormValues, toWriteDto } from './mutations';
import type { GrantDetailDto } from './queries';

/**
 * The third subject of a grant (M3, A11a, note 2026-09-27-i-capi-fir-sul-loro-fir): the team of a FIR, at some levels, with no
 * department and no VID. Its levels leave the browser with it, as a department's do; the rest is the server's to judge.
 */
describe('a grant to the team of a FIR', () => {
  it('sends the team and its levels, and no member nor department', () => {
    const dto = toWriteDto({
      ...emptyGrant(),
      positionFirTeam: true,
      positionLevels: ['Coordinator', 'Assistant'],
      value: 'Links.Edit',
    });

    expect(dto.positionFirTeam).toBe(true);
    expect(dto.positionLevels).toEqual(['Coordinator', 'Assistant']);
    expect(dto.vid).toBeNull();
    expect(dto.positionDepartment).toBeNull();
  });

  it('starts unticked, and reads back as it was written', () => {
    expect(emptyGrant().positionFirTeam).toBe(false);

    const grant: GrantDetailDto = {
      id: 1,
      vid: null,
      positionDepartment: null,
      positionLevels: ['Coordinator'],
      positionFirTeam: true,
      kind: 'Permission',
      value: 'Links.View',
      department: 'WD',
      resourceScope: null,
      effect: 'Grant',
      expiresAt: null,
      suspendedAt: null,
      reason: null,
      createdAt: '2026-09-28T00:00:00Z',
      createdBy: 0,
      updatedAt: '2026-09-28T00:00:00Z',
      updatedBy: 0,
      rowVersion: '2026-09-28T00:00:00Z',
    };

    const values = toFormValues(grant);
    expect(values.positionFirTeam).toBe(true);
    expect(values.positionLevels).toEqual(['Coordinator']);
  });
});
