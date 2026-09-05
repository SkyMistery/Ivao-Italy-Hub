// Builds the bench the full end to end suite runs against, and runs it.
//
// The bench is the *published* application: one origin serving both the API and the SPA, with the
// server's own `MapFallbackToFile` behind every deep address. That matters more than it sounds.
// When the M0 package was verified by hand, four smoke tests came out red against a perfectly
// healthy build because the static server used to serve it answered 404 to `/staff/ed/links`
// (handoff, "Il tag"). A suite that serves the SPA some other way tests something the product does
// not do.
//
// What it is not: `--self-contained --runtime linux-x64`, which is what a release ships and what a
// developer on Windows cannot run. The layout, the fallback and the language files are identical;
// the runtime packaging is not, and the CI step that checks the shipped package is still its own.
//
// Environment:
//   E2E_URL                 where to listen (default http://127.0.0.1:5080)
//   E2E_CONNECTION_STRING   the bench database, which is never the development one
//   E2E_SKIP_PUBLISH=1      reuse the last publish, for when only the specs changed

import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repository = path.resolve(here, '..', '..');
const output = path.join(repository, 'artifacts', 'e2e-bench');

const url = process.env.E2E_URL ?? 'http://127.0.0.1:5080';

// Root, and a database of its own: the round writes real rows, and it must never be able to write
// them into the database somebody is developing against. Root because applying the migrations for
// the first time creates that database, which the application user is not granted.
const connectionString =
  process.env.E2E_CONNECTION_STRING ??
  'Server=localhost;Port=3306;Database=ivaohub_e2e;User ID=root;Password=ivaohub-root;MaximumPoolSize=15;DefaultCommandTimeout=30';

// `dotnet.exe` and never `shell: true`: this repository's own path contains spaces, and a shell
// concatenates the arguments back into one string without quoting them. The first run of this
// script published a project called "New".
const dotnet = process.platform === 'win32' ? 'dotnet.exe' : 'dotnet';

function run(command, args, options = {}) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, { stdio: 'inherit', ...options });
    child.on('error', reject);
    child.on('exit', (code) =>
      code === 0 ? resolve() : reject(new Error(`${command} ${args.join(' ')} exited with ${code}`)),
    );
  });
}

const executable = path.join(output, process.platform === 'win32' ? 'IvaoHub.Web.exe' : 'IvaoHub.Web');

if (process.env.E2E_SKIP_PUBLISH !== '1' || !existsSync(executable)) {
  await run(
    dotnet,
    [
      'publish',
      path.join('src', 'IvaoHub.Web'),
      '--configuration',
      'Release',
      '--output',
      output,
      // `BuildSpaOnPublish` is on by default, so the PublishSpa target puts the built SPA under
      // wwwroot/ exactly as it does for a release. Nothing to pass; nothing to remember.
    ],
    { cwd: repository },
  );
}

const server = spawn(executable, [], {
  cwd: output,
  stdio: 'inherit',
  env: {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: 'E2E',
    ASPNETCORE_URLS: url,
    ConnectionStrings__Default: connectionString,

    // The bench signs itself in as this person: the web coordinator of the division, which is a
    // coordinator of one department and never a super administrator, so the department guard is
    // something the round actually meets.
    //
    // WD and not, say, the events department, for a reason worth knowing: the system templates are
    // seeded owned by WD, and `Content.View` is scoped to a department, so a coordinator of any
    // other department sees no templates at all and "New from a template" does not appear for
    // them. That is a real question about the product and it is written down rather than worked
    // around here (decisions/2026-09-05-template-di-sistema-e-dipartimenti.md).
    E2E__Enabled: 'true',
    E2E__Vid: '999001',
    E2E__FirstName: 'Bench',
    E2E__LastName: 'Coordinator',
    E2E__Positions__0: 'IT-WM',

    // No IVAO credentials, and none needed: the reference data comes from the fixtures and the
    // sign in never talks to an identity provider. The OAuth block still has to parse, because the
    // application refuses to start without one — these are the values the integration tests use.
    Ivao__Authority: 'https://api.ivao.aero',
    Ivao__ClientId: 'e2e-client',
    Ivao__ClientSecret: 'e2e-secret',
    Ivao__LoginUrl: `${url}/auth/login`,
    Ivao__RedirectUri: `${url}/auth/callback`,
    Ivao__PostLogoutRedirectUri: `${url}/`,
    Ivao__Scopes__0: 'openid',
    Ivao__UseFixtures: 'true',

    AllowedHosts: '*',
    IVAOHUB_ROOT: repository,
  },
});

server.on('exit', (code) => process.exit(code ?? 0));

for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, () => server.kill(signal));
}
