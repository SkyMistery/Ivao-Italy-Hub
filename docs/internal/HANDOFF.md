# HANDOFF — stato di M0

> Documento **interno** (italiano). Si aggiorna alla fine di ogni fase (piano di implementazione §A.6).
> Fonte di verità: `00-piano-di-progettazione.md`; perimetro e firme: `01-design-m0.md`; ordine: `02-piano-implementazione-m0.md`.

**Ultimo aggiornamento:** 2 settembre 2026 — fine **F0** (bootstrap del repository).
**Branch:** `m0/f0-bootstrap` (da `main`) — PR https://github.com/SkyMistery/Ivao-Italy-Hub/pull/1, CI verde.
**Remote:** `https://github.com/SkyMistery/Ivao-Italy-Hub` (pubblico). **Prossima fase:** F1 — configurazione, avvio, DB del nucleo.

---

## 1. Come si avvia (locale)

```bash
docker compose up -d                          # MariaDB 11.4.10 + Mailpit
dotnet run --project src/IvaoHub.Web          # API su http://localhost:5000
cd web && pnpm install && pnpm dev            # SPA su http://localhost:5173 (proxy /api, /auth, /health)
```

Controlli:

```bash
dotnet build IvaoHub.sln
dotnet test --solution IvaoHub.sln            # richiede Docker attivo (Testcontainers)
cd web && pnpm lint && pnpm format:check && pnpm typecheck && pnpm test && pnpm i18n:check && pnpm build
dotnet publish src/IvaoHub.Web -c Release -r linux-x64 --self-contained -o artifacts/publish
```

Prerequisiti verificati su questa macchina: .NET SDK 10.0.301, Node 24, pnpm 11 (che passa da solo a
10.34.5 per via di `packageManager`), Docker 29.7.2.

## 2. Cosa c'è dopo F0

- Radice: `global.json` (SDK 10.0.x + opt-in `test.runner`), `Directory.Build.props`
  (nullable, warnings-as-errors, `InvariantGlobalization=false`), `Directory.Packages.props` con
  **tutte** le versioni del design §0.3, `IvaoHub.sln`, `.editorconfig`, `.gitattributes`.
- Progetti: `IvaoHub.Core` (vuoto), `IvaoHub.Web` (`/health` fisso + static files + fallback SPA con
  esclusioni cablate), `IvaoHub.Modules.Atc` (classe segnaposto, **senza** `IModule`),
  `IvaoHub.UnitTests`, `IvaoHub.IntegrationTests`.
- SPA: Vite 7 + React 19 + TS 5.9 strict + Tailwind 4 + Atmosphere 3.1.0 + TanStack Router
  (`routeTree.gen.ts` in git) + i18next; home con `Navbar` e testi da `locales/`.
- `locales/{en,it}/common.json` alla radice: serviti da Vite in dev, emessi in `dist/locales` al
  build e quindi presenti in `wwwroot/locales` nel pacchetto pubblicato.
- CI `build-test.yml` (dotnet build/test, pnpm lint/format/typecheck/test/i18n:check/build,
  `git diff --exit-code` sui generati, publish self-contained + verifica `wwwroot/index.html`) e
  `release.yml` sui tag `v*`.
- `README.md`, `docs/FORKING.md` (stub), `LICENSE` «TBD», `.github/PULL_REQUEST_TEMPLATE.md`.

Verifiche eseguite a mano: `/health` = 200; `/api/x` e `/services/vsop/x` = 404 (non intercettate
dalla SPA); con `wwwroot` popolato `/` e `/login-error` restituiscono `index.html`;
`pnpm dev` mostra la home con il titolo tradotto e nessun errore in console.

## 3. Regole già attive (non aggirarle nelle fasi successive)

- ESLint blocca davvero (provato con file di prova poi rimossi): `fetch` fuori da `src/shared/api`,
  `<svg>` fuori da `src/shared/icons` e `src/blocks`, import dal nucleo verso `src/modules/` e import
  tra due moduli.
- Le zone «modulo ↔ modulo» sono **derivate dalle cartelle presenti** in `web/src/modules/`: aggiungere
  un modulo estende la regola da solo, non c'è un elenco da mantenere.
- `web/src/modules/index.ts` è l'elenco esplicito dei manifest (speculare a `IvaoHub.Web/Modules.cs`,
  che nasce in F8). Il tipo `ModuleManifest` arriva in F6 con `shared/modules.ts`.
- Nessuna stringa utente nel codice: la home usa `t('app.title')`, `t('home.heading')`, `t('home.lead')`.

## 4. Scelte fatte in F0 che vale la pena conoscere

| Scelta | Perché |
|---|---|
| `global.json` contiene anche `"test": { "runner": "Microsoft.Testing.Platform" }` | xUnit v3 gira su Microsoft.Testing.Platform e l'SDK 10 rifiuta il percorso VSTest. Di conseguenza **non** si referenzia `Microsoft.NET.Test.Sdk` e il comando è `dotnet test --solution IvaoHub.sln`. |
| `@vitejs/plugin-react` **5.2.0** | La 6.x richiede Vite 8; il design fissa Vite 7. |
| ESLint 10 + `@eslint/js` 10 | Il design non fissa ESLint; la 9 è marcata deprecata da npm e tutti i plugin usati dichiarano `^10`. |
| Il target `PublishSpa` aggiunge `web/dist` come `ResolvedFileToPublish` sotto `wwwroot/` invece di copiarlo nel `wwwroot` del sorgente | Il pacchetto pubblicato contiene `wwwroot/index.html` (criterio di accettazione) senza sporcare l'albero di lavoro né rischiare item duplicati. `wwwroot/` del sorgente è in `.gitignore` tranne `.gitkeep`. |
| `IvaoHub.sln` in formato classico | L'SDK 10 genera `.slnx` per default; il design nomina `IvaoHub.sln`. |
| `GenerateDocumentationFile=true` | Serve perché IDE0005 (using inutilizzati) giri in build con `TreatWarningsAsErrors`; CS1591 resta soppresso. |

## 5. Debiti e cose da fare presto

- `config/division.json`, `division.example.json`, `ivao-oauth.example.json`: **F1** (non erano nel
  perimetro di F0).
- `IvaoHub.Core` è un progetto vuoto: la prima classe arriva in F1 (`Localized<T>` + `HubDbContext`).
- Il test di architettura «nessun modulo referenzia un altro modulo / Core non referenzia Web» è
  **F4**: in F0 il compilatore elide il riferimento a un assembly vuoto e il test sarebbe finto.
- `docs/UI-GUIDELINES.md` è **F6**; `docs/FORKING.md` va completato in **F8/F9**.
- Il chunk JS supera i 500 kB (avviso di Vite): lo split per route arriva con i layout di F6.
- Licenza ancora «TBD» (piano §15.5).
