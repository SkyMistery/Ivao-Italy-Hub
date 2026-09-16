import { Button } from '@ivao/atmosphere-react';
import { Plus } from 'lucide-react';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';

/** The button that opens a new row of a list of the tours. */
export function NewButton({ href, label, variant }: { href: string; label: string; variant?: 'outline' }) {
  return (
    <Button asChild {...(variant === undefined ? {} : { variant })}>
      <RouterAnchor href={href}>
        <Plus aria-hidden className="mr-2 size-4" />
        {label}
      </RouterAnchor>
    </Button>
  );
}
