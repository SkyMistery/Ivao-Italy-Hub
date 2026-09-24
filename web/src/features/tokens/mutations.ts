import { useMutation, useQueryClient } from '@tanstack/react-query';

import { api, unwrap, unwrapEmpty } from '../../shared/api/client';

import { tokensKey, type PersonalTokenIssuedDto } from './queries';
import type { TokenFormValues } from './schema';

/** Creating a token. The answer carries its text: the one time the hub ever shows it. */
export function useCreateToken() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: TokenFormValues): Promise<PersonalTokenIssuedDto> =>
      unwrap(await api.POST('/api/me/tokens', { body: { ...values, name: values.name.trim() } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: tokensKey });
    },
  });
}

/** Revoking one. It stops working on the program's next request. */
export function useRevokeToken() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.POST('/api/me/tokens/{id}/revoke', { params: { path: { id } } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: tokensKey });
    },
  });
}
