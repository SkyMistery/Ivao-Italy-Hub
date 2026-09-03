# IVAO Division Hub

The website and back office of an IVAO division: one ASP.NET Core process that serves a React
single page application, exposes the API and hosts the department modules.

The project is built to be **forked**: nothing about a particular division lives in the code.
The behaviour of a division comes from `config/division.json`, its airspace from the IVAO API
snapshots, and every piece of editorial content from the database.

> **Status: M0, phase F5 of nine.** The application validates its configuration and migrates its own
> database, signs a member in with IVAO and computes what they are allowed to do, keeps a snapshot
> of the airspace of the division, and carries the backbone everything else is built on: audit
> columns, a per-department write guard, the visibility filter, the search projections and a single
> authorization handler, all covered by tests against a real MariaDB. On top of it there is now one
> generic CRUD engine: a resource of the back office — paging, sorting, searching, the department
> filter, validation, optimistic concurrency — costs a configuration object, and `/api/links` is the
> first one. The API describes itself in an OpenAPI document written at build time, from which the
> typed client of the front end is generated. What is not there yet is everything a visitor would
> see: the back office screens and the editorial content arrive in the phases that follow.

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

Signing in needs `LoginUrl` and `RedirectUri` in `config/ivao-oauth.json` to match, character for
character, what IVAO has registered for that client — locally `http://localhost:5173/auth/login`
and `http://localhost:5173/auth/callback`. Without credentials the reference data of the division
can still be read from the fixtures in `tests/fixtures/ivao/`, with `Ivao:UseFixtures=true` in
development.

The application refuses to start when its configuration is incomplete, and says which field is
wrong. `diagnostics/startup.txt` records what started, which migrations it applied and which modules
are on; it never contains a secret.

### What production needs on top of development

Two settings have no sensible default, so the application refuses to start in production without
them. Both belong in `secrets/*.json` or in environment variables, never in `division.json`: they
describe the installation, not the division.

| Setting | Why |
| --- | --- |
| `AllowedHosts` | The real host names, `;` separated, without `*`. Host filtering is what stops a request carrying a forged `Host` from being served at all. |
| `ForwardedHeaders:TrustedNetworks` | The CIDR networks of the proxies in front, whose `X-Forwarded-For` may be believed — the ranges Cloudflare publishes, or `127.0.0.1/32` for a local reverse proxy. Believing that header from anybody means the caller chooses its own address, which turns the rate limiting on the login and the addresses in the audit log into decoration. |

Production also sends HSTS and redirects plain http to https. Set `Https:Redirect` to `false` if the
proxy in front already refuses http itself.

```json
{
  "AllowedHosts": "it.ivao.aero;www.it.ivao.aero",
  "ForwardedHeaders": { "TrustedNetworks": ["173.245.48.0/20", "2400:cb00::/32"] }
}
```

In development none of this applies: hosts are not filtered, no HSTS or redirection is added, and
with no trusted network the forwarded headers are not read at all, so the address is the one the
connection really came from.

### Configuration files

| File | In the repository | What it is |
| --- | --- | --- |
| `config/division.json` | yes | Behaviour of the division: code, languages, time zone, optional modules. Not a secret. |
| `config/ivao-oauth.json` | **no** | The OAuth client of the division. Copy the example and fill it in, or use `Ivao__*` environment variables. |
| `secrets/*.json` | **no** | Connection string, SMTP, `AllowedHosts`, the trusted proxies, and anything else that must not be read from the web. |
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
pnpm format:check
pnpm typecheck
pnpm test
pnpm i18n:check
pnpm build

# The typed API client. `dotnet build` writes artifacts/openapi/IvaoHub.Web.json; this turns it into
# web/src/shared/api/schema.d.ts, which is committed. The CI regenerates it and fails on a diff, so
# run it after changing an endpoint or a payload.
pnpm gen:api
```

## Packaging

```bash
dotnet publish src/IvaoHub.Web -c Release -r linux-x64 --self-contained
```

The publish target builds the SPA and places it, together with the language files, under
`wwwroot/` inside the published package. Next to the binaries the package also carries
`locales/{lang}/*.json` — the copy the server itself reads — the `config/*.example.json` files to
copy, and `LICENSE` and `NOTICE`. Everything an installation owns — `config/division.json`,
`config/ivao-oauth.json`, `secrets/`, `hub-keys/` — stays outside the package and next to it on the
server, so a deployment never overwrites the configuration or the keys.

## Repository layout

| Path | What it holds |
| --- | --- |
| `src/IvaoHub.Core` | Domain, EF Core, the IVAO API client, content |
| `src/IvaoHub.Web` | Host: endpoints, `/api/me`, SPA fallback, `wwwroot` |
| `src/IvaoHub.Modules.*` | One project per department module; each references only the core |
| `web/` | The single page application |
| `locales/{lang}/*.json` | The only set of language files; nothing else holds a string a user can read |
| `config/` | `division.json` and the OAuth client configuration |
| `tests/` | Unit tests and integration tests (Testcontainers, a real MariaDB of the production version) |
| `docs/` | Public documentation: the forking guide, and the UI guidelines once the front end has them |

## Forking

See [docs/FORKING.md](docs/FORKING.md).

## Licence

[Apache License 2.0](LICENSE), copyright 2026 Carmine Granato. Fork it, run it for your division,
change what you need: the licence asks you to keep the notice, to state the files you changed, and
it grants you the patent rights that come with the code. [`NOTICE`](NOTICE) carries the attribution
that travels with every copy; a fork keeps what is there and adds its own below it.
