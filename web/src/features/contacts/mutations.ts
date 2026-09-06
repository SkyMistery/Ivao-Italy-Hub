import { useMutation, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../shared/api/client';
import type { Department } from '../../shared/api/bootstrap';
import type { ContactFormValues } from '../../shared/ui';

import { contactKey, contactsKey, type ContactDetailDto } from './queries';
import type { ContactStatusFormValues } from './schema';

/**
 * Writing a contact message: sending one, and moving one along. Two mutations, and they belong to
 * two different people — the member who writes and the department that answers — which is why
 * neither of them can do the other's half.
 */

/** A member sends a message. The sender is the session, so the payload does not carry one. */
export function useSubmitContact() {
  return useMutation({
    mutationFn: async (values: ContactFormValues): Promise<{ id: number }> =>
      unwrap(
        await api.POST('/api/contacts', {
          body: {
            department: values.department as Department,
            subject: values.subject.trim(),
            body: values.body.trim(),
          },
        }),
      ),
  });
}

/** The department moves a message to its next state. Nothing else about it can be written. */
export function useUpdateContactStatus(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: ContactStatusFormValues): Promise<ContactDetailDto> =>
      unwrap(
        await api.PUT('/api/contacts/{id}', {
          params: { path: { id: String(id) } },
          body: { status: values.status, rowVersion: values.rowVersion },
        }),
      ),
    onSuccess: async (message) => {
      queryClient.setQueryData(contactKey(id), message);
      await queryClient.invalidateQueries({ queryKey: contactsKey });
    },
  });
}
