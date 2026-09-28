# Deploying the hub

How one installation of the hub goes onto a server, written for the hardest hosting it is known to run on:
**Plesk with Phusion Passenger, FTP only, no shell, Cloudflare in front, a shared MariaDB**. A server where you
have a shell and systemd is simpler and uses the same folders and the same settings.

Every claim below marked **measured** was run on the package of 27 September 2026 (the `linux-x64`
self-contained publish, Linux containers, MariaDB 11.4.10); the rest is how the hosting is known to work, and says
so. What could not be measured from outside a real server is listed at the end.

## What you deploy

The package is the zip that `.github/workflows/release.yml` attaches to a GitHub release when a `v*` tag is pushed:
`ivao-division-hub-v<version>.zip`, built by CI from a clean checkout with

```bash
dotnet publish src/IvaoHub.Web -c Release -r linux-x64 --self-contained
```

**It is never a publish of your own machine.** How that zip is fetched, checked against the tag's commit, cut down to
the files that changed and handed to whoever uploads is [Delivering a release](DELIVERING.md), with
`tools/prepare-delivery.ps1`; this page starts where that one ends, on the server. (The measures below were taken on a
local publish of the same commit, which is fine for measuring and never for delivering.)

It is **self-contained**: the .NET 10 runtime travels inside it, so the server does not need .NET at all. About
600 files and 140 MB unpacked (measured).

What the package brings, and what belongs to the installation and must never be overwritten by a deploy:

| Path | From | What it is |
| --- | --- | --- |
| `IvaoHub.Web`, `IvaoHub.Web.dll`, `*.dll`, `*.so`, `*.json` at the root | package | the application and its runtime |
| `wwwroot/` | package | the single page application: `index.html`, `assets/`, `branding/`, `locales/` |
| `locales/`, `seed/`, `LICENSE`, `NOTICE` | package | the language files the server reads, the page templates of a fresh installation |
| `config/security.json`, `config/*.example.json` | package | the security headers, and the examples to copy |
| `config/division.json` | **installation** | the division: copy `config/division.example.json`, or take your fork's file |
| `secrets/<unguessable>.json` | **installation** | connection string, OAuth client, hosts, proxies, installation settings |
| `config/ivao-oauth.json` | **installation**, optional | the OAuth client, if you prefer it out of `secrets/` (it then wins over `secrets/`) |
| `hub-keys/` | written at the first start | Data Protection keys. **Persistent: never delete it**, or everybody is signed out and the stored IVAO tokens become unreadable |
| `media/` | written by uploads | the media library, on disk. **Persistent: never delete it** |
| `tiles/basemap.pmtiles` | **installation**, optional | the base map of the tours (`docs/FORKING.md`); without it maps draw on a neutral ground |
| `logs/`, `diagnostics/` | written by the application | the log of the day; `diagnostics/startup.txt` after a start, `diagnostics/startup-error.txt` after a start that failed |
| `tmp/restart.txt` | **installation** | Passenger restarts the application when this file's date changes |

The application finds its folders by looking for `config/division.json` from its working directory upwards, then from
its own folder (the one holding `IvaoHub.Web.dll`) upwards, so it does not matter from which directory the host starts
the process. Found from its own folder, that folder is also where it serves `wwwroot/` from. `diagnostics/startup.txt`
says where the root is and how it was found. `IVAOHUB_ROOT` overrides both, for an installation that keeps its folders
somewhere else.

## What the server needs

- **Linux x64** with glibc, **ICU** (`libicu`) and OpenSSL — the native pieces every .NET application on Linux uses.
  Measured: without ICU the process stops at once with "Couldn't find a valid ICU package". No .NET is needed.
- Something that starts the process and proxies to it: Phusion Passenger here.
- **MariaDB 11.4**; CI applies every migration on 11.4.10.
- HTTPS in front. Behind Cloudflare the application does not redirect plain http by itself (measured: without a
  forwarded scheme it answers 200 on http, because it has no https port to send the browser to), so turn on
  Cloudflare's "Always Use HTTPS". It does send HSTS on every request that arrived over https.

## The start command

Two commands start the package, and both were **measured**:

| Command | Works when | Execute bit |
| --- | --- | --- |
| `./IvaoHub.Web` | always — the server needs no .NET at all (measured on an image with no `dotnet`) | **required** on `IvaoHub.Web`: FTP does not carry it, so set `755` after every upload of that file. At `644` the shell answers `Permission denied` |
| `dotnet IvaoHub.Web.dll` | a `dotnet` host of **any** version is installed (measured with 8.0.31 only) | not needed (measured with every file at `644`) |

The sheet of every delivery still asks for `755` on `IvaoHub.Web` after the upload ([Delivering a
release](DELIVERING.md), step 5): with `dotnet …dll` it is harmless, and it keeps the other command possible.

`dotnet IvaoHub.Web.dll` does **not** need the .NET 10 runtime on the server: the installed `dotnet` reads
`IvaoHub.Web.runtimeconfig.json`, sees a self-contained application and hands over to the runtime inside the
package (the host trace says "Executing as a self-contained app as per config file" and loads `libhostpolicy`
10.0.12 from the application folder). A server that already runs another .NET application through Passenger with
`dotnet …dll` can start the hub the same way.

Passenger chooses the port. ASP.NET Core does not read Passenger's `PORT` variable, so the start command has to
pass it on, for example `dotnet IvaoHub.Web.dll --urls http://127.0.0.1:$PORT`. How the port reaches the command
depends on how the host wired Passenger: copy what it does for its other .NET application. *Not measured here.*

`ASPNETCORE_ENVIRONMENT` may be left unset: ASP.NET Core's default is `Production`, which is what the hub expects.

## The configuration of the installation

Precedence, lowest first: `appsettings.json` (package) < `secrets/*.json` < `config/ivao-oauth.json` < environment
variables. Everything below goes in **one file under `secrets/`, whose name nobody can guess**
(`secrets/k7f3a91c4e8b2.json`, not `secrets.json`): the web server denies the folder, and the name is the second
lock if a deny rule is ever lost. Never put the file in a zip or a mail.

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Port=3306;Database=<database>;User ID=<user>;Password=<password>;MaximumPoolSize=15;ConnectionIdleTimeout=60;DefaultCommandTimeout=30"
  },
  "Ivao": {
    "Authority": "https://api.ivao.aero",
    "ClientId": "<client id>",
    "ClientSecret": "<client secret>",
    "LoginUrl": "https://<host>/auth/login",
    "RedirectUri": "https://<host>/auth/callback",
    "PostLogoutRedirectUri": "https://<host>/",
    "Scopes": ["openid", "profile", "email", "discord"],
    "ApiScopes": []
  },
  "AllowedHosts": "<host>",
  "ForwardedHeaders": {
    "TrustedNetworks": ["127.0.0.1/32", "::1/128", "<the Cloudflare ranges>"]
  },
  "Installation": { "Domain": "<host>", "Preview": true }
}
```

| Key | What to put there |
| --- | --- |
| `ConnectionStrings:Default` | the installation's own database and user. **`MaximumPoolSize` at most 15**: on shared hosting the connections per user are capped for everybody together |
| `Ivao` | the installation's own OAuth client. The three URLs must match **character for character** what is registered with IVAO for that client: `https`, the host, no trailing slash on the first two. The application refuses to start while a field is missing |
| `AllowedHosts` | the host names this installation answers to, `;` separated, never `*`. Required in production (measured: a request with another `Host` gets 400) |
| `ForwardedHeaders:TrustedNetworks` | the networks whose `X-Forwarded-For` and `X-Forwarded-Proto` are believed. Required in production. Behind Passenger the application's peer is the local machine, so the loopback addresses belong here, with the ranges Cloudflare publishes at <https://www.cloudflare.com/ips/> |
| `Diagnostics:RequestHeaders` | optional: more header names whose value `/api/admin/diagnostics/request` shows, when the visitor's address may arrive in a header the hub does not know. `Cookie` and `Authorization` are never shown |
| `Installation:Domain` | the host every absolute link is built on — mails, sitemap, robots.txt — when it is not `division.json → domain`, as on a test installation. A host name only |
| `Installation:Preview` | `true` for a private installation: not indexed (robots.txt, no sitemap, `X-Robots-Tag` on every response) and **open to the staff of the division and the super administrators only**; anybody else is turned away at the end of the IVAO round trip, before anything about them is written |
| `Smtp` | optional. Without it nothing is sent and nothing is lost: notifications queue up. On a test installation, leaving it out keeps test data from mailing real people |

`diagnostics/startup.txt` shows, after every start, the version, the commit, the environment, the `domain`, the
`access` (`public` or `private: staff only, not indexed`) and the migrations applied. It never contains a secret.

## The web server in front

**Make `wwwroot/` the document root**, and the application folder Passenger's application root. Then the web server
can only ever hand out the files of the single page application, and everything else reaches the application,
which serves nothing outside `wwwroot/` (measured: `/appsettings.json`, `/secrets/<name>.json`,
`/config/division.json`, `/IvaoHub.Web.dll`, `/diagnostics/startup.txt` all answer 404 from the application
itself).

If the document root has to be the application folder, add these **additional nginx directives** — and add them
anyway, as a second lock:

```nginx
location ~ ^/(secrets|hub-keys|config|diagnostics|logs|seed|tmp)(/|$) { deny all; }
location ~ ^/[^/]+\.(json|dll|pdb|so)$ { deny all; }
location ~ ^/IvaoHub\.[^/]+\.xml$ { deny all; }
location ~ ^/(IvaoHub\.Web|createdump)$ { deny all; }
```

⚠️ **Never deny `/media/` or `/tiles/`**: those are addresses of the application (the media library, the base map),
not only folder names, and a deny there breaks them. The files in `media/` have random names and are not reachable
by their address. Never deny `*.json` below the root either — the language files of the SPA are
`/locales/…/*.json` — nor every `*.xml`: `/sitemap.xml` is an address of the application.

`Cache-Control: no-store` on `/api/*` and `/health` is **already sent by the application** (measured), so no
directive is needed for it; check that the host does not strip it.

After every change of hosting, **check from outside** with `curl -I` that each of these is 403 or 404, or the page
of the site — never the file: `/secrets/<your file name>.json`, `/config/division.json`, `/appsettings.json`,
`/IvaoHub.Web.dll`, `/hub-keys/`, `/diagnostics/startup.txt`.

## The database

- **An empty database** and a user of its own, created in the panel, with **`GRANT ALL` on that database only**.
  Measured: that grant is enough for every migration, including the `ALTER DATABASE … CHARACTER SET utf8mb4` the
  first one starts with.
- `utf8mb4`.
- **Its own database**, even on a server that already hosts another application of the division, and a separate one
  for a test installation and for production.
- The migrations run **at start-up**, every time, and apply whatever is missing: there is no shell and no
  `dotnet ef`. They are additive only (a column is dropped only a release after the code stopped using it), so a
  start never destroys data. `diagnostics/startup.txt` lists what the start applied.
- `max_allowed_packet`: the default is enough; uploads go to disk, never into the database.

## The first installation

1. Create the database and its user (above).
2. Create the host (the subdomain) with **its document root on `<app>/wwwroot`** and Passenger on `<app>`, with the
   start command above. Point DNS at it through Cloudflare.
3. Register an OAuth client with IVAO for this host, with the three URLs of the `Ivao` block.
4. Unpack the zip into `<app>` — on the server if the file manager can extract, otherwise upload the folder with FTP
   **in binary mode**, keeping the sub-folders. If you start with `./IvaoHub.Web`, set it to `755`.
5. Put `config/division.json` and `secrets/<unguessable>.json` in place, and create `tmp/`.
6. Open the site once: Passenger starts the process on the first request.
7. Run the checks below.

## After every deploy: the checks

Not in the minute of the restart: give it the time to apply its migrations.

| Check | Expected |
| --- | --- |
| `diagnostics/startup.txt`, read over FTP | `started at` is now; the `version` and `commit` of the release; `domain` and `access` as intended |
| `diagnostics/startup-error.txt` | **absent**: a start that succeeds deletes it. If it is there, the start failed, and the file says why |
| `curl -s https://<host>/api/version` | the same version and commit, and `.NET 10…` |
| `curl -s https://<host>/health` | `Healthy` |
| `curl -s https://<host>/robots.txt` | a private installation: `Disallow: /`; a public one: its disallowed paths and the sitemap |
| `curl -sI https://<host>/` | a private installation carries `X-Robots-Tag: noindex, nofollow` |
| Sign in with IVAO as a member of staff | your name in the bar |
| A private installation: sign in as somebody who is not staff | the page "The sign in did not complete" with the sentence about a private copy, and no row for them |
| The deny checks of the section above | 403, 404, or the page of the site |
| As super administrator, change something harmless, then read the audit log | the address recorded is **yours**, not `127.0.0.1` nor a Cloudflare address: that is how you know `TrustedNetworks` is right |
| As super administrator, open `https://<host>/api/admin/diagnostics/request` in the browser | how the hub sees your request: `believed.address` is yours and `scheme` is `https`. If not, the same answer says why: the neighbour and its family, the forwarding headers as they arrived and how many entries each holds, the names of every header, and the settings of the forwarded headers. Nothing of it is stored |

## Updating

- **Never overwrite a file of a running application.** A process keeps its `.dll` files mapped; FTP truncates a file
  to rewrite it, and the process dies on the spot without a line in any log. Upload under another name (or into a
  new folder) and **rename** into place: renaming does not touch the mapped file. Keep the old files next to them
  until the new version has proved itself: they are the rollback.
- **Never delete** `hub-keys/`, `media/`, `secrets/`, `config/division.json`, `tiles/`, `tmp/`.
- Then change the date of `tmp/restart.txt` (upload an empty file over it) **and open the site once**: Passenger
  notices on the next request.
- Read the first line of `diagnostics/startup.txt` again: a start time that did not change means the old process is
  still the one answering.

## Backups

The database is not the whole installation. Back up, together: the database, **`hub-keys/`**, **`media/`**, the file
under `secrets/`, and `config/division.json`. A restore is proven only once it has been done.

## Known limits

- **A start that fails writes why to `diagnostics/startup-error.txt`**, next to where `startup.txt` would be, as well
  as to standard output: the time, the version and commit, the environment, the root and how it was found, the
  working directory, and the exception with its cause and stack. It never quotes a value of the configuration that
  could be a secret (the files of `secrets/`, the OAuth file, any key naming a connection string, a password, a
  secret, a token or a key): those are replaced by `[redacted]`, when they are six characters or longer — shorter
  values are flags and numbers, so a password that short would not be hidden. The next start that succeeds deletes
  the file.
  Measured on the linux-x64 package started from another directory: a missing division file, a missing OAuth
  field, a database that cannot be reached. What stops the process before .NET code runs, or kills it without an
  exception (no ICU, a truncated `.dll`, out of memory), still says so only on standard output, which is in
  Passenger's log.
- **Passenger stops an idle application** and starts it again on the next request. The scheduled jobs (the mail
  queue, the reference data, the release of the tours) only run while the process is alive. `passenger_min_instances
  1` keeps one alive, where the host allows it.
- **Forwarded headers** are processed one hop deep (the ASP.NET Core default). Whether the address the application
  believes is the visitor's depends on the headers Passenger passes on: the audit log check above is how to know, and
  `/api/admin/diagnostics/request` is how to know why.
- The runtime uses the server garbage collector, which reserves more memory on a machine with many cores. On a host
  that caps memory, `DOTNET_gcServer=0` in the environment of the process turns it off. *Not measured on a server.*

## Not measured

- Passenger itself: how the port reaches the process, the restart through `tmp/restart.txt`, and what it passes in
  `X-Forwarded-For`. The package was run in Linux containers, behind no proxy but a trusted neighbour.
- A real IVAO sign in on a deployed host.
- Whether the zip, unpacked by a hosting panel, keeps the execute bit that `zip` records on Linux.
