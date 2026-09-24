import { Button, H3, Input } from '@ivao/atmosphere-react';
import { createFileRoute } from '@tanstack/react-router';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { useCreateToken, useRevokeToken } from '../../features/tokens/mutations';
import { tokenColumns } from '../../features/tokens/list';
import { tokensListQuery, type PersonalTokenIssuedDto } from '../../features/tokens/queries';
import { audienceWordKey, emptyToken, tokenSchema, type TokenFormValues } from '../../features/tokens/schema';
import { SchemaForm } from '../../shared/forms';
import { DataList, listSearchSchema } from '../../shared/list';
import { ConfirmDialog, Notice, PageShell } from '../../shared/ui';

/**
 * The member's personal tokens (M2, T19a, note 2026-09-15-token-personali-e-agente-del-validatore §3.1): a program of
 * theirs — the validator's agent — uses one instead of the cookie, on the endpoints of its audience only. Created here,
 * shown once, revoked here. Recipe 2 (design M0 §7.3) for the list, the generated form for the new one.
 */
export const Route = createFileRoute('/_member/me_/tokens')({
  validateSearch: listSearchSchema,
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps }) => context.queryClient.ensureQueryData(tokensListQuery(deps)),
  component: MyTokensPage,
});

function MyTokensPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  const create = useCreateToken();
  const revoke = useRevokeToken();
  const [issued, setIssued] = useState<PersonalTokenIssuedDto | null>(null);

  const audiences = useMemo(() => bootstrap.user?.tokenAudiences ?? [], [bootstrap.user]);
  const schema = useMemo(
    () =>
      tokenSchema(
        audiences.map((audience) => ({
          value: audience,
          label: t(audienceWordKey(audience), { defaultValue: audience }),
        })),
      ),
    [audiences, t],
  );

  return (
    <PageShell
      title={t('tokens.title')}
      description={t('tokens.description')}
      breadcrumb={[{ label: t('tokens.title') }]}
    >
      <div className="flex flex-col gap-8">
        {issued !== null && <IssuedToken issued={issued} />}

        <DataList
          columns={tokenColumns}
          query={tokensListQuery(search)}
          labels="tokens"
          locale={i18n.language}
          defaultLocale={bootstrap.division.defaultLocale}
          timezone={bootstrap.division.timezone}
          search={search}
          onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
          actions={(row) => (
            <ConfirmDialog
              triggerText={t('tokens.revoke.action')}
              title={t('tokens.revoke.title')}
              description={t('tokens.revoke.description', { name: row.name })}
              confirmText={t('tokens.revoke.action')}
              disabled={revoke.isPending}
              onConfirm={() => revoke.mutate(row.id)}
            />
          )}
        />

        <section className="flex flex-col gap-4">
          <H3>{t('tokens.new')}</H3>
          {audiences.length === 0 ? (
            <Notice tone="info" title={t('tokens.noAudience')} />
          ) : (
            <SchemaForm
              schema={schema}
              defaults={emptyToken(audiences)}
              locales={bootstrap.division.locales}
              labels="tokens"
              onSubmit={async (values: TokenFormValues) => {
                setIssued(await create.mutateAsync(values));
              }}
              submitLabel={t('tokens.create')}
            />
          )}
        </section>
      </div>
    </PageShell>
  );
}

/** The token just created, the one time it is shown: copy it now, or revoke it and make another. */
function IssuedToken({ issued }: { issued: PersonalTokenIssuedDto }) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);

  return (
    <Notice
      tone="success"
      title={t('tokens.issued.title', { name: issued.row.name })}
      description={
        <div className="flex flex-col gap-2">
          <p>{t('tokens.issued.description')}</p>
          <div className="flex gap-2">
            <Input
              readOnly
              value={issued.token}
              aria-label={t('tokens.issued.label')}
              className="font-mono"
            />
            <Button
              type="button"
              variant="secondary"
              onClick={() =>
                void navigator.clipboard.writeText(issued.token).then(() => {
                  setCopied(true);
                })
              }
            >
              {copied ? t('tokens.issued.copied') : t('tokens.issued.copy')}
            </Button>
          </div>
        </div>
      }
    />
  );
}
