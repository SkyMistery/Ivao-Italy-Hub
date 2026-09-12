/// <reference types="vitest/config" />
import { readFileSync } from 'node:fs';
import { readdir, readFile } from 'node:fs/promises';
import { join, posix, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

import tailwindcss from '@tailwindcss/vite';
import { tanstackRouter } from '@tanstack/router-plugin/vite';
import react from '@vitejs/plugin-react';
import { defineConfig, type Plugin } from 'vite';

import { BACKEND_PATHS } from './backendPaths';

/** The backend during development; Vite proxies the host endpoints to it. */
const KESTREL_ORIGIN = 'http://localhost:5000';

/**
 * The security headers, read from the one file the backend also reads (`config/security.json`).
 *
 * In production it is ASP.NET that serves both the SPA and the API, so it is ASP.NET that sends
 * these. Here they exist for the two servers Vite runs -- `dev` and `preview` -- and `preview` is
 * the one that matters: the smoke suite runs against it, so every one of those tests runs under the
 * real policy and a screen that a directive would break fails there rather than in production.
 *
 * ⚠️ The policy is **not** written twice. This reads the same file, and `SecurityHeadersTests`
 * asserts that what the backend sends is what this file says.
 */
interface SecurityConfiguration {
  readonly headers: Record<string, string>;
  readonly contentSecurityPolicy: {
    readonly enabled: boolean;
    readonly directives: Record<string, readonly string[]>;
  };
}

const security = JSON.parse(
  readFileSync(fileURLToPath(new URL('../config/security.json', import.meta.url)), 'utf8'),
) as SecurityConfiguration;

function policyOf(directives: Record<string, readonly string[]>): string {
  return Object.entries(directives)
    .map(([directive, sources]) => `${directive} ${sources.join(' ')}`)
    .join('; ');
}

const securityHeaders = (extra: Record<string, readonly string[]> = {}) => ({
  ...security.headers,
  ...(security.contentSecurityPolicy.enabled
    ? {
        'Content-Security-Policy': policyOf({
          ...security.contentSecurityPolicy.directives,
          ...extra,
        }),
      }
    : {}),
});

/**
 * ⚠️ Development is looser, and the reason is written here rather than discovered again: Vite's
 * client injects a module of its own and talks to itself over a web socket, so `script-src` takes
 * `'unsafe-inline'` and `connect-src` takes the socket. Neither reaches a built package, which is
 * why `preview` -- the build -- runs under the strict policy.
 */
const DEVELOPMENT_RELAXATIONS: Record<string, readonly string[]> = {
  'script-src': ["'self'", "'unsafe-inline'"],
  'connect-src': ["'self'", 'ws:', 'wss:'],
};

/** The framework itself: matched by package folder, so `react-markdown` is not one of them. */
const REACT_CORE = ['react', 'react-dom', 'scheduler'];

/** Language files live at the root of the repository and are shared with the backend. */
const LOCALES_DIR = fileURLToPath(new URL('../locales', import.meta.url));

async function listFiles(directory: string): Promise<string[]> {
  const entries = await readdir(directory, { withFileTypes: true, recursive: true });
  return entries.filter((entry) => entry.isFile()).map((entry) => join(entry.parentPath, entry.name));
}

/**
 * Serves `/locales/**` from the repository root during development and copies the same files into
 * `dist/locales` at build time, so that the published `wwwroot` carries them without a second
 * mechanism. There is exactly one set of language files (design M0 section 7.6).
 */
function divisionLocales(): Plugin {
  return {
    name: 'ivao-hub-locales',
    configureServer(server) {
      server.middlewares.use('/locales', (request, response, next) => {
        const requested = (request.url ?? '/').split('?')[0] ?? '/';
        const file = resolve(LOCALES_DIR, `.${requested}`);
        if (!file.startsWith(LOCALES_DIR)) {
          next();
          return;
        }
        readFile(file).then(
          (content) => {
            response.setHeader('Content-Type', 'application/json; charset=utf-8');
            response.end(content);
          },
          () => next(),
        );
      });
    },
    async generateBundle() {
      for (const file of await listFiles(LOCALES_DIR)) {
        this.emitFile({
          type: 'asset',
          fileName: posix.join('locales', relative(LOCALES_DIR, file).split(sep).join('/')),
          source: await readFile(file),
        });
      }
    },
  };
}

export default defineConfig({
  plugins: [
    tanstackRouter({ target: 'react', autoCodeSplitting: true }),
    react(),
    tailwindcss(),
    divisionLocales(),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: Object.fromEntries(BACKEND_PATHS.map((path) => [path, KESTREL_ORIGIN])),
    headers: securityHeaders(DEVELOPMENT_RELAXATIONS),
  },
  preview: {
    headers: securityHeaders(),
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    rollupOptions: {
      output: {
        /**
         * The router splits the screens on its own (`autoCodeSplitting`); what is left in one lump
         * is the libraries, and they do not all change together. Three groups, by how often they
         * move and who needs them: the framework every page needs, the design system every page
         * also needs but which ships on its own cadence, and the editor's markdown renderer, which
         * only the pages that show prose ever load.
         */
        manualChunks: (id) => {
          if (!id.includes('node_modules')) {
            return undefined;
          }

          if (REACT_CORE.some((packageName) => id.includes(`/node_modules/${packageName}/`))) {
            return 'react';
          }

          if (id.includes('@ivao') || id.includes('@radix-ui') || id.includes('lucide-react')) {
            return 'atmosphere';
          }

          if (id.includes('react-markdown') || id.includes('remark') || id.includes('micromark')) {
            return 'markdown';
          }

          return undefined;
        },
      },
    },
  },
  test: {
    environment: 'jsdom',
    globals: false,
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
  },
});
