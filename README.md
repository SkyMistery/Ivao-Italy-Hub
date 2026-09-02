# IVAO Division Hub

The website and back office of an IVAO division: one ASP.NET Core process that serves a React
single page application, exposes the API and hosts the department modules.

The project is built to be **forked**: nothing about a particular division lives in the code.
The behaviour of a division comes from `config/division.json`, its airspace from the IVAO API
snapshots, and every piece of editorial content from the database.

> **Status: M0, phase F1.** The application validates its configuration, migrates its own database
> and reports what is running. Authentication and the generic backbone arrive in the phases that
> follow.

## Requirements

| Tool | Version |
| --- | --- |
| .NET SDK | 10.0.x (pinned by `global.json`) |
| Node.js | 22 LTS or newer |
| pnpm | 10.x (pinned by `web/package.json`) |
| Docker | any recent version, for MariaDB and the integration tests |

## Running it locally

```bash
# 1. Tell the application which division it is and which OAuth client to use.
cp config/ivao-oauth.example.json config/ivao-oauth.json   # then fill it in; it is never committed

# 2. Start MariaDB 11.4.10 and the fake SMTP server.
docker compose up -d

# 3. Start the API on http://localhost:5000; it applies its migrations on the way up
dotnet run --project src/IvaoHub.Web

# 4. In another shell, start the SPA on http://localhost:5173
cd web
pnpm install
pnpm dev
```

Open <http://localhost:5173>. Vite proxies `/api`, `/auth` and `/health` to Kestrel, so the SPA and
the API behave exactly as they do in the single published process.

The application refuses to start when its configuration is incomplete, and says which field is
wrong. `diagnostics/startup.txt` records what started, which migrations it applied and which modules
are on; it never contains a secret.

### Configuration files

| File | In the repository | What it is |
| --- | --- | --- |
| `config/division.json` | yes | Behaviour of the division: code, languages, time zone, optional modules. Not a secret. |
| `config/ivao-oauth.json` | **no** | The OAuth client of the division. Copy the example and fill it in, or use `Ivao__*` environment variables. |
| `secrets/*.json` | **no** | Connection string, SMTP and anything else that must not be read from the web. |
| `hub-keys/` | **no** | Data Protection keys. Persistent: never delete them, or everybody is logged out. |

## Checks

```bash
dotnet build IvaoHub.sln
dotnet test --solution IvaoHub.sln     # unit tests plus integration tests, Docker must be running

# Adding a migration (additive only, never edit one that is already merged)
dotnet tool restore
dotnet dotnet-ef migrations add <Name> --project src/IvaoHub.Core --startup-project src/IvaoHub.Core

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
