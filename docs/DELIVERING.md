# Delivering a release to an FTP-only server

The hub runs on a host that has no shell: whoever administers it uploads files by FTP and restarts the
application by dropping a file into `tmp/`. This is how a release becomes the zip they are handed.

**The package is never built on your machine.** It is the zip that `.github/workflows/release.yml`
attaches to the GitHub release of a tag, after the whole CI has run, from a clean checkout. Two things
follow from that, and they are the reason for the rule:

- **the stamp is exact.** `/api/version` and `diagnostics/startup.txt` say `<version>+<commit>`, and the
  commit is the tag's — never the working folder of whoever happened to publish, with or without
  uncommitted changes;
- **no file of your machine can be inside it.** Secrets, a local `division.json`, a key ring: the CI runner
  never had them.

What happens on your machine is only taking that zip apart and putting together what is handed over.
`tools/prepare-delivery.ps1` does the mechanical steps; this page gives their order and the decisions no
script can take for you. Why it is done this way: `docs/internal/decisions/2026-09-27-la-consegna-del-pacchetto.md`.

## The folders

Everything lives under `artifacts/`, which git ignores.

| Path | What it holds |
| --- | --- |
| `artifacts/publish/` | **the current delivery only** |
| `publish/full-<version>/` | the release, unpacked. `RELEASE.txt` lists every file it held with its sha256, plus the tag, the commit and the hash of the zip |
| `publish/ivao-division-hub-v<version>.zip` | the release asset as downloaded — the thing to roll back to |
| `publish/candidates-<version>.txt` | the list proposed by Diff, which you edit |
| `publish/only-<N>-files-<version>/` | what is uploaded, when it is not the full package |
| `publish/docs/` | the sheets of this delivery |
| `publish/delivery-<package>.zip` | what is handed over, with its `.sha256` |
| `publish/restart.txt` | an empty file: uploaded into `tmp/` last, it restarts the application |
| `artifacts/publish_old/<version>/` | one folder per past delivery, with **the sheets of the time** |

The sheets in `publish_old/` are never updated. They are what whoever uploaded was told that day, and the
only way to answer, six months later, "but what did we tell them to do?".

## Before you start

- **The version.** The tag is `v` followed by `<Version>` of `Directory.Build.props`, and the rule for
  raising it is written next to the number. The question it answers is "is FTP enough, or does the
  database have to change too?" — read it before tagging.
- **The release exists.** The tag is pushed by the maintainer, and `release.yml` has finished green: the
  release page shows `ivao-division-hub-v<version>.zip`.
- **`gh` is signed in** and the repository's `origin` is the one the release belongs to.

Every action takes `-WhatIf`, which says what it would do and writes nothing. Use it first.

## 1. Fetch the release

```powershell
.\tools\prepare-delivery.ps1 -Action Fetch -Tag v0.2.0 -WhatIf
```

Then, without `-WhatIf`, the same command:

- downloads the release zip into a temporary folder;
- checks every entry: no absolute path, no `..`, nothing that belongs to an installation;
- reads the stamp of `IvaoHub.Web.dll` and compares it with the commit the tag points to **on origin**.
  A package whose stamp is not the tag's is not the package of that tag, and nothing is moved;
- only then moves the current delivery, whole, to `publish_old/<its version>/`, and unpacks the new one
  into `publish/full-<version>/`.

It stops, without guessing, when `publish/` holds two `full-*` folders, when `publish_old/<version>/`
already exists, or when the new version is older than the current one (going back is the zip in
`publish_old/`, not a new delivery).

## 2. Decide what goes

```powershell
.\tools\prepare-delivery.ps1 -Action Diff
```

It compares the new release with the full package of the previous delivery, **by hash**, and writes
`publish/candidates-<version>.txt`. With no previous delivery the list is the whole release: a first
installation gets the full package.

**The hash says what differs; it does not say what must go.** Read every line, and remove or comment out
what must not be uploaded. The list is grouped, and each group has its rule:

- **The hub's own assemblies go all together.** Every release raises the version, the version is part of
  each assembly's identity, and an `IvaoHub.Web.dll` of the new version next to an `IvaoHub.Core.dll` of the
  previous one does not load. `IvaoHub.*.dll`, their `.pdb` (without them a stack trace loses its line
  numbers), `IvaoHub.Web.deps.json` and the apphost `IvaoHub.Web` go as a set; the Manifest action refuses a
  list that carries some of the assemblies and not all.
- **`wwwroot/` goes with its index.** `index.html` names the new hashed files, so every new file goes with
  it, and so does `IvaoHub.Web.staticwebassets.endpoints.json` when it changed. The old hashed files stay on
  the server, unused.
- **Language files, templates, configuration examples, legal files**: what changed, goes.
- **The runtime and third-party libraries** change only with a package or SDK upgrade. When the runtime
  itself changed (`libcoreclr.so`, `System.Private.CoreLib.dll`), deliver the full package: a half-replaced
  runtime does not start, and its error does not look like the cause.
- **`appsettings.Development.json` is never delivered.** It is in the release, it carries the local database
  password, and production does not read it. Diff writes it already commented out.
- **Removed files** are listed at the bottom: they stay on the server, harmless because nothing names them
  any more. Say so in the sheet.
- **Migrations** between the two commits are listed at the bottom too, when git has both commits. They run
  at start-up, alone, on a server nobody can restore in a minute: read the version rule again.

If the previous delivery was skipped or only half uploaded, compare with the one that is really on the
server: `-Against <version>`.

## 3. Declare it

```powershell
.\tools\prepare-delivery.ps1 -Action Manifest -List artifacts\publish\candidates-0.2.0.txt
```

For a full delivery, add `-Full`: the package is `full-<version>/` itself, and the files the list leaves
out stay in the folder but not in the zip.

Every line must be a file of the release, **byte for byte the one downloaded**. The files are copied into
`publish/only-<N>-files-<version>/` and `MANIFEST.txt` records them with their hashes. From here on, the
manifest says what goes into the zip — never the folder.

## 4. Build the zip

```powershell
.\tools\prepare-delivery.ps1 -Action Zip -Sheets docs\internal\deploy
```

`-Sheets` takes files or folders of the repository: the sheets for whoever uploads, copied into
`publish/docs/` every time, because a copy of its own ages by itself. From a folder it takes the `*.md` and
`*.json`, and among the names that carry a version only this version's (`…-0.2.0.md`). This division keeps
its sheets in `docs/internal/deploy/`; a fork keeps its own wherever it likes.

The zip has three branches, never mixed:

```
only-31-files-0.2.0/   what is uploaded, with MANIFEST.txt to check it against
docs/                  what is read
restart.txt            the tool: into tmp/, last
```

### The two nets, and why they are there

On 24 and 31 August 2026 the production secrets of the sister project this procedure comes from — a
connection string with its password, the IVAO client secret — were lying in the folder to upload, and a zip
built by walking the folder sent them by mail. A secrets file is protected only by its unguessable name;
inside an attachment it is protected by nothing.

1. **What is in the package folder and not declared stays out**, and is listed. A file that is byte for
   byte one the release held (a file a full delivery leaves out on purpose) is reported as left out, not
   as an intruder.
2. **Inside the declared files, the undeclared ones and the sheets**, a text file holding a credential stops
   the delivery: a value under a key ending in `password`, `pwd`, `secret`, `apikey` or `token`, a
   `Password=` in a connection string, a private key, a Data Protection key. Only the key is printed, never
   the value. A value that is empty, a `<placeholder>` or `UPPER-CASE-WORDS-WITH-DASHES` is a form to fill
   in, not a credential — that is how `config/*.example.json` and a template of the secrets file among the
   sheets pass.

A path that belongs to an installation — `secrets/`, `hub-keys/`, `media/`, `logs/`, `diagnostics/`,
`tmp/`, `config/division.json`, `config/ivao-oauth.json`, keys and certificates — stops the delivery in
any list, whatever it contains.

**When a net rings on something you believe is harmless, the question is not how to silence it: it is
whether that file must be delivered at all.** Almost always it must not, and leaving it out removes the
problem instead of going around it. There is no switch that turns a net off.

## 5. Hand it over

The zip and its `.sha256` go to whoever uploads, with the sheet. What the sheet must say beyond the list
of files, every time:

- upload **in binary**, and set the execute bit on `IvaoHub.Web` again after the upload;
- never touch `secrets/`, `hub-keys/`, `media/`, `config/division.json`, `config/ivao-oauth.json`;
- the files removed since the previous package, which stay on the server;
- `restart.txt` into `tmp/` **last**, then open the site once;
- the check that tells a working site from a half-uploaded one: `/api/version` says the new version and
  the tag's commit, `/health` answers, and a page that goes through the server shows real data. A stamp
  alone says which package started, not that it works.

Then write down, in the maintainer's notes, what was delivered: the version, the sha256 of the zip, and
what is left to do.
