# IVAO Division Hub

The website and back office of an IVAO division: one ASP.NET Core process that serves a React
single page application, exposes the API and hosts the department modules.

The project is built to be **forked**: nothing about a particular division lives in the code.
The behaviour of a division comes from `config/division.json`, its airspace from the IVAO API
snapshots, and every piece of editorial content from the database.

> **Status: M0, phase F0.** Only the repository skeleton exists: it builds, it is tested and the
> CI is green. Configuration, database, authentication and the generic backbone arrive in the
> phases that follow.

## Requirements

| Tool | Version |
| --- | --- |
| .NET SDK | 10.0.x (pinned by `global.json`) |
| Node.js | 22 LTS or newer |
| pnpm | 10.x (pinned by `web/package.json`) |
| Docker | any recent version, for MariaDB and the integration tests |

## Running it locally

```bash
# 1. Start MariaDB 11.4.10 and the fake SMTP server.
docker compose up -d

# 2. Start the API on http://localhost:5000
dotnet run --project src/IvaoHub.Web

# 3. In another shell, start the SPA on http://localhost:5173
cd web
pnpm install
pnpm dev
```

Open <http://localhost:5173>. Vite proxies `/api`, `/auth` and `/health` to Kestrel, so the SPA and
the API behave exactly as they do in the single published process.

## Checks

```bash
dotnet build IvaoHub.sln
dotnet test --solution IvaoHub.sln     # unit tests plus integration tests, Docker must be running

cd web
pnpm lint
pnpm typecheck
pnpm test
pnpm i18n:check
pnpm build
```

## Packaging

```bash
dotnet publish src/IvaoHub.Web -c Release -r linux-x64 --self-contained
```

The publish target builds the SPA and places it, together with the language files, under
`wwwroot/` inside the published package.

## Repository layout

| Path | What it holds |
| --- | --- |
| `src/IvaoHub.Core` | Domain, EF Core, the IVAO API client, content |
| `src/IvaoHub.Web` | Host: endpoints, `/api/me`, SPA fallback, `wwwroot` |
| `src/IvaoHub.Modules.*` | One project per department module; each references only the core |
| `web/` | The single page application |
| `locales/{lang}/*.json` | The only set of language files, read by the SPA and by the backend |
| `config/` | `division.json` and the OAuth client configuration |
| `tests/` | Unit tests and integration tests (Testcontainers, real MariaDB) |
| `docs/` | Public documentation: forking guide, UI guidelines |

## Forking

See [docs/FORKING.md](docs/FORKING.md).

## Licence

**To be decided.** The `LICENSE` file is a placeholder; until a licence is chosen the code is
published for review only.
