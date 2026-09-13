import { describe, expect, it } from 'vitest';

import { emptyGrant, toWriteDto } from './mutations';

/**
 * A grant is for a member **or** a position (M2). The form has both halves; what leaves the browser
 * carries one of them and nothing of the other, so the server's "exactly one subject" answers the
 * person who filled in both rather than a leftover of an empty field.
 */
describe('toWriteDto', () => {
  it('sends a grant to a member without a position', () => {
    const dto = toWriteDto({
      ...emptyGrant(),
      vid: 704798,
      positionLevels: ['Advisor'],
      value: 'Links.Edit',
    });

    expect(dto.vid).toBe(704798);
    expect(dto.positionDepartment).toBeNull();
    expect(dto.positionLevels).toEqual([]);
  });

  it('sends a grant to a position without a member', () => {
    const dto = toWriteDto({
      ...emptyGrant(),
      positionDepartment: 'AOD',
      positionLevels: ['Coordinator', 'Assistant'],
      value: 'Links.Edit',
    });

    expect(dto.vid).toBeNull();
    expect(dto.positionDepartment).toBe('AOD');
    expect(dto.positionLevels).toEqual(['Coordinator', 'Assistant']);
  });
});
