# IVAO Division Hub — Piano di implementazione di M0 (per Claude Code / Opus)

> Documento **interno** (italiano). Prerequisiti di lettura per chi implementa: `CLAUDE.md` (radice), piano
> `00-piano-di-progettazione.md` v0.18 (§4, §6, §7, §9.3, §9.7, §16), design `01-design-m0.md` (firme e perimetro).
> Questo file dice **in che ordine** si costruisce, **cosa** consegna ogni fase, **come si verifica** che sia finita.

**Versione:** 1.6 — 4 settembre 2026 (**F9 chiusa, e con lei M0**: revisione §16.E su tutto il codice (`decisions/2026-09-04-m0-review.md`), `tools/demo-m0.md`, `FORKING.md` con i passi reali di un fork, tre stringhe visibili corrette, tag `v0.1.0-m0`)

---

## A. Come si lavora con Claude Code su M0

1. **Una fase per sessione** (al massimo due se piccole). Ogni sessione parte con il prompt di apertura (§C) che rimanda alla fase. Non si anticipa lavoro delle fasi successive «già che ci siamo».
2. **Branch per fase**: `m0/f<N>-<slug>` da `main`; una PR per fase con il template e la checklist §16.E compilata onestamente; merge solo con CI verde. Commit in inglese, Conventional Commits (`feat(core): …`, `test(int): …`).
3. **Regola (a)/(b)/(c)** di CLAUDE.md §5 sempre attiva: se durante una fase serve un meccanismo non previsto dal design, la sessione **si ferma**, scrive `docs/internal/decisions/YYYY-MM-DD-<argomento>.md` (mezza pagina) e chiede a Carmine. La fase può chiudersi senza quella parte.
4. **Criteri di accettazione = test**: una fase è chiusa quando i test elencati esistono e passano in CI, non quando «funziona a mano». I test della spina dorsale (design §8) non si spostano né si marcano `Skip`.
5. **Niente stringhe utente nel codice**, niente `fetch` a mano, niente `*_translations`, niente handler di autorizzazione oltre a `DepartmentAuthorizationHandler`, niente schermate CRUD scritte a mano: se una PR ne contiene, la checklist lo dichiara e Carmine decide.
6. Alla fine di ogni fase Claude Code aggiorna: `docs/internal/HANDOFF.md` (stato, cosa manca, come avviare), il changelog del piano **solo se** c'è stata una decisione, e `CLAUDE.md` **solo su indicazione di Carmine**.
7. Versioni: quelle del design §0.3; upgrade solo in PR dedicate.

## B. Sequenza delle fasi

| Fase | Nome | Dipende da | Risultato verificabile |
|---|---|---|---|
| F0 | Bootstrap del repository | — | `dotnet build`, `pnpm build`, CI verde, `/health` risponde, SPA servita |
| F1 | Configurazione, avvio, DB del nucleo | F0 | opzioni validate, `Migrate()` all'avvio su MariaDB reale, `diagnostics/startup.txt`, `/api/version` |
| F2 | Auth BFF, utenti, ruoli, superadmin, `/api/me` | F1 | login reale con credenziali di test; `/api/me` completo; test StaffRoleMap e permessi (FIR riconosciute solo dopo F3) |
| F3 | `IvaoApiClient` e dati `ref_` | F1 | job di sync, tabelle popolate (o fixture), posizioni FIR riconosciute |
| F4 | Spina dorsale del dominio | F2 | `Localized<T>`, interceptor, query filter, policy provider + handler unico, `IProjectable`, test integrazione verdi |
| F5 | `MapCrud` e `links` (server) | F4 | CRUD `links` senza codice a mano, OpenAPI, client TS generato in CI |
| F6 | Spina dorsale frontend | F2, F5 | layout, ricette router, `DataList`, `SchemaForm`, `LocaleFields`, back-office `links`, ui-kit, `UI-GUIDELINES.md` — **fatta** |
| F7 | Contenuti: entità, envelope, publish, blocchi, editor, template | F5, F6 | pagina da template → editor → publish → resa pubblica; `frozen` catturato — **fatta** |
| F8 | Moduli, admin, manutenzione, ricerca, forkabilità | F6, F7 | `IModule` + `atc`, `/staff/admin/{permissions,modules,audit,ui-kit}`, `/api/search`, test XX — **fatta** |
| F9 | Chiusura M0 | tutte | demo end-to-end da script, HANDOFF, tag `v0.1.0-m0` — **fatta** |

F3 può girare in parallelo a F4–F5 (non si toccano). Tutto il resto è sequenziale.

---

## C. Prompt di apertura di ogni sessione (da incollare, sostituendo `<N>`)

```
Stiamo implementando la fase F<N> di M0 dell'IVAO Division Hub.
Leggi nell'ordine: CLAUDE.md, docs/internal/01-design-m0.md (tutto), docs/internal/02-piano-implementazione-m0.md
(sezioni A, C e la fase F<N>), poi le sezioni del piano 00 richiamate dalla fase. Se esiste docs/internal/HANDOFF.md leggilo.
Vincoli: solo il perimetro della fase F<N>; codice, commenti, commit e docs pubbliche in inglese; nessuna stringa utente nel
codice; usa i meccanismi generici del design, non copie locali. Se serve qualcosa che il design non prevede, fermati e
scrivi una nota in docs/internal/decisions/ invece di improvvisare. Chiudi la fase solo con i test dei criteri di accettazione
verdi. Alla fine aggiorna docs/internal/HANDOFF.md e prepara la PR con la checklist compilata.
Prima di scrivere codice, elenca in 10 righe cosa farai e quali file toccherai; poi procedi.
```

---

## D. Le fasi

### F0 — Bootstrap del repository

**Obiettivo**: scheletro compilabile e CI verde; nessuna logica di dominio.

Task:
1. `git init`, `.gitignore` esistente, `.editorconfig`, `.gitattributes` (LF, `*.gen.ts linguist-generated`), `global.json` (SDK 10.0.x), `Directory.Build.props` (`Nullable`, `TreatWarningsAsErrors`, `ImplicitUsings`, `LangVersion latest`), `Directory.Packages.props` con le versioni del design §0.3.
2. Soluzione `IvaoHub.sln` con `src/IvaoHub.Core`, `src/IvaoHub.Web`, `src/IvaoHub.Modules.Atc` (vuoto, una classe `AtcModule` segnaposto senza `IModule` ancora), `tests/IvaoHub.UnitTests`, `tests/IvaoHub.IntegrationTests` (con Testcontainers referenziato ma un solo test «container parte»).
3. `IvaoHub.Web`: `Program.cs` minimo con `/health` (200 fisso in F0), static files da `wwwroot`, fallback SPA `MapFallbackToFile("index.html")` con l'elenco di esclusioni cablato temporaneamente (`/api`, `/auth/login`, `/auth/callback`, `/auth/logout`, `/health`, `/openapi`, `/scalar`, `/services/vsop`, `/vsop`, `/_content`, `/_framework`) — verrà sostituito dal registry in F8. Nota: `/login-error` è una route SPA, non va esclusa.
4. `web/`: Vite 7 + React 19 + TS strict + Tailwind 4 (`@tailwindcss/vite`) + `@ivao/atmosphere-react` 3.1.0 + `@tanstack/react-router` + plugin + `@tanstack/react-query` + `i18next`/`react-i18next` + `zod` 4 + `react-hook-form` + ESLint (typescript-eslint, react-hooks, regole `no-restricted-globals: fetch` fuori da `shared/api`, `no-restricted-syntax` per `<svg` fuori da `shared/icons` e `blocks`, `import/no-restricted-paths` tra `modules/<a>` ↔ `modules/<b>` e `features/` → `modules/`, design §6.5); cartelle `src/modules/` con `index.ts` vuoto (elenco esplicito dei manifest) + Prettier + Vitest. Pagina `/` che mostra `Navbar` Atmosphere con titolo da `division.name` (per ora hardcoded `t('app.title')` con `locales/en/common.json` e `locales/it/common.json`). Proxy dev verso `:5000`.
5. `locales/{it,en}/common.json` alla radice del repo, serviti da Vite in dev (`publicDir` o plugin) e copiati in `wwwroot/locales` al publish. Script `pnpm i18n:check` (chiavi identiche tra lingue).
6. Target MSBuild `PublishSpa` in `IvaoHub.Web.csproj` (esegue `pnpm install --frozen-lockfile && pnpm build`, copia `web/dist` in `wwwroot`) attivo solo in `dotnet publish`.
7. `docker-compose.yml`: `mariadb:11.4.10` (db `ivaohub`, utente dedicato, `utf8mb4_unicode_ci`), `mailpit`. `README.md` (EN) con avvio locale; `docs/FORKING.md` stub; `LICENSE` placeholder «TBD» + nota nel README; `.github/PULL_REQUEST_TEMPLATE.md` con la checklist di §16.E in inglese.
8. `.github/workflows/build-test.yml`: matrice unica ubuntu-latest; `dotnet restore/build/test` (unit + integration con Docker disponibile), `pnpm install/lint/typecheck/test/i18n:check/build`, `dotnet publish -c Release -r linux-x64 --self-contained` → artefatto `publish/`. `release.yml` su tag `v*`.

Accettazione: CI verde su PR; `dotnet run --project src/IvaoHub.Web` + `pnpm dev` mostra la home; `curl localhost:5000/health` = 200; `dotnet publish` produce `wwwroot/index.html`.

Non fare: DbContext, auth, opzioni, componenti custom.

### F1 — Configurazione, avvio, DB del nucleo

**Obiettivo**: l'app parte solo se configurata bene, migra il DB da sola, scrive la diagnostica.

Task:
1. `DivisionOptions` + validatore (design §2.1), `config/division.json` IT e `division.example.json`.
2. `IvaoOAuthOptions` + validatore fail-fast (§2.2), `ivao-oauth.example.json`; l'app **non parte** se il file manca o è incompleto, con messaggio senza secret.
3. Caricamento `secrets/*.json`, `appsettings.Development.json` con connection string docker (`MaximumPoolSize=15`), Serilog (file `logs/` + console, `RequestLoggingMiddleware` con correlation id), Data Protection su `hub-keys/` (`SetApplicationName("IvaoHub")`), `ForwardedHeaders` condizionali, `AllowedHosts` obbligatorio in Production.
4. `HubDbContext` in `Core/Data` con `MariaDbServerVersion(11.4.10)`, `UseSnakeCaseNamingConvention` (EFCore.NamingConventions) + `LocalizedColumnConvention` (suffisso `_i18n`, design §3.1) — tutte le tabelle/colonne snake_case, prefissi via `ToTable("hub_users")` esplicito per entità.
5. Entità e configurazione (solo scaffolding, senza logica): `HubUser`, `UserStaffPosition`, `UserGrant`, `UserToken`, `DivisionSetting`, `AuditLogEntry`, `JobLogEntry`, `IvaoCenter`, `IvaoAirport`, `Content`, `ContentVersion`, `Link`, `SearchIndexEntry` (una riga per lingua, FULLTEXT `(title, text)` creato nella migrazione `Initial` con `migrationBuilder.Sql`), `CalendarEntry`, `AwardSignal` — colonne come piano §7 e design §3.11, unicità contenuti `(kind, slug, is_template)`. `Localized<T>` con record, converter EF e comparer si fanno **qui** (design §3.1, parte EF) perché la migrazione ne ha bisogno; F4 aggiunge la parte API/validazione. Una sola implementazione.
6. Migrazione `Initial`; `Database.Migrate()` all'avvio dentro `IHostedService` che gira **prima** di `app.Run` accettare traffico (usare `IHostApplicationLifetime` o eseguire la migrazione in `Program` prima di `app.Run()`); `diagnostics/startup.txt` (§2.4).
7. `/api/version`, `/health` con ping DB (`HealthChecks` + `AddMySql` o query `SELECT 1`), `Cache-Control: no-store` su `/api/*` e `/health` via middleware.
8. Test: `DivisionOptionsValidationTests`, `IvaoOAuthOptionsValidationTests`, integrazione `MigrationsApplyOnRealMariaDb` (da zero + secondo avvio idempotente, verifica charset `utf8mb4`), `HealthAndVersionEndpoints`.

Accettazione: con docker-compose su, `dotnet run` crea tutte le tabelle; senza `ivao-oauth.json` l'app esce con codice ≠ 0 e messaggio chiaro; test verdi.

Non fare: login, interceptor, query filter, endpoint di dominio.

### F2 — Auth BFF, utenti, ruoli, superadmin, `/api/me`

**Obiettivo**: login reale con IVAO; l'identità applicativa e i permessi effettivi esistono.

Task:
1. `Department`, `StaffLevel`, `StaffRole`, `StaffPosition`, `StaffRoleMap.Parse` (design §3.8) + `StaffRoleMapTests` con **tutte** le righe della tabella §4.1 del piano per `IT`, `XX`, `XXX`, FIR `LIRR`/`LIMM`, casi negativi (`FR-DIR` con divisione IT, `T100`, `TA0`).
2. `RolePermissionMatrix` + `CorePermissions` (design §3.7) + `RolePermissionMatrixTests` riga per riga; `EffectivePermissionsCalculator` (derivati ∪ grant − deny, scadenza, sospensione, esclusioni globali) + test.
3. OIDC BFF (design §4) partendo dalle scelte di `Vipi.Host/Auth/VipiStandaloneAuthExtensions.cs` (Carmine fornisce il file o le sue righe chiave nella sessione; **non** si copia da chat il secret). `UserSyncService.UpsertAsync` (in F2 `StaffRoleMap.Parse` riceve `firIds` = **insieme vuoto**: `IFirDirectory` arriva in F3 e allora si collega), `IvaoUserTokenStore` (Data Protection, purpose `IvaoTokens`, `null` se illeggibile), claim compatti, `security_stamp` + `OnValidatePrincipal` + `ISecurityStampCache`, `/auth/login`, `/auth/callback` (gestito dal handler OIDC), `POST /auth/logout`, route SPA `/login-error`, CSRF header middleware, rate limiting su `/auth/*`.
4. `ICurrentUser` + `HttpContextCurrentUser` (design §3.3, incluso `HasAllDepartments`), `IClock`.
5. Bootstrap superadmin (design §4.5) + `SuperadminService` (add/remove con vincolo «mai l'ultimo», solo da superadmin). L'audit in F2 è una scrittura diretta di `AuditLogEntry` da parte del servizio; in F4 la sostituisce l'interceptor (`[Audited]`) e la scrittura diretta si rimuove. L'endpoint arriva in F8, il servizio e i test ora.
6. `GET /api/me` (design §3.10) con `navigation` e `registries` vuoti/statici per ora (nucleo: `nav.home`, `nav.staff` se staff).
7. Frontend minimo: `BootstrapProvider` (query `/api/me`), pulsante Login/Logout nella Navbar, route `/me` che mostra VID, nome, dipartimenti, permessi (lista grezza), `/login-error` tradotta. Header `X-Requested-With` nel client (creare già `shared/api/client.ts` con `openapi-fetch`; lo schema generato arriva in F5 — in F2 tipizzare a mano solo `Bootstrap`).
8. Test integrazione: `SuperadminBootstrapOnlyWhenNone`, `CannotRemoveLastSuperadmin`, `SecurityStampInvalidatesCookie` (login finto via `TestAuthHandler` che emette il cookie applicativo), `ApiMeAnonymousAndAuthenticated`, `CsrfHeaderRequired`.

Accettazione: login reale con le credenziali di test di Carmine → `/me` mostra i dati; `hub_users` e `hub_user_staff_positions` popolate; il VID 704798 è superadmin dal bootstrap; test verdi.

Non fare: grant UI, interceptor, contenuti.

### F3 — `IvaoApiClient` e dati `ref_`

**Obiettivo**: FIR e aeroporti dallo snapshot IVAO; posizioni FIR riconosciute.

Task:
1. `IvaoApiClient` typed client con `AddStandardResilienceHandler`, token `client_credentials` in cache (`IMemoryCache`, scadenza − 60 s), metodi `GetCentersAsync`, `GetAirportsAsync`, `GetMeAsync`; DTO minimi + `raw_json`.
2. `FixtureIvaoApiClient` (fixture in `tests/fixtures/ivao/centers-IT.json`, `airports-IT.json` — Claude Code le crea con 2–3 record realistici ma **non** copiati dalla chat) attivo con `Ivao:UseFixtures=true`.
3. Quartz in-process (`AddQuartzHostedService`, store in memoria), `RefDataSyncJob` giornaliero (tz divisione) + esecuzione all'avvio se le tabelle `ref_` sono vuote, `hub_jobs_log`, mai eccezioni all'avvio.
4. `IFirDirectory` (nucleo) che espone gli id delle FIR da `ref_ivao_centers` con cache; `StaffRoleMap` la usa nel `UserSyncService`.
5. Test: `IvaoApiClientTokenCaching` (handler HTTP finto), `RefDataSyncJobUpserts` (integrazione con fixture), `FirPositionsRecognizedAfterSync`.

Accettazione: dopo l'avvio con fixture, `ref_ivao_centers` contiene le FIR; una posizione `LIRR-CH` nei claim (test) produce `FirChief` con `Fir = LIRR`.

### F4 — Spina dorsale del dominio

**Obiettivo**: i meccanismi generici di §16 esistono, sono testati e non si possono aggirare.

Task:
1. `Localized<T>` parte API (design §3.1): `JsonConverter` per le API, `LocalizedRules` FluentValidation, `Resolve`/`HasAll` testati. (Lo schema OpenAPI `x-localized` arriva in F5 con `AddOpenApi`.) Test round-trip su MariaDB `json` (converter di F1).
2. Interfacce trasversali ed enum (§3.2); marcare le entità di F1 (`Link`, `Content`; `ContentVersion` non è `IOwnedByDepartment`: eredita dal contenuto; `UserGrant` è solo `IAuditable`).
3. `HubSaveChangesInterceptor` (§3.4): audit/timestamp, **guardia di scrittura per dipartimento**, `hub_audit_log` per `[Audited]`, proiezioni in due tempi (`SavingChanges` → transazione + raccolta; `SavedChanges` → snapshot + upsert + commit; flag di rientranza). `AddHubDbContext` lo registra; `AddModuleDbContext<T>` (già ora, anche se nessun modulo lo usa) lo eredita.
4. Global query filter per `IVisible`+`IOwnedByDepartment` (+`IPublishable`) come espressione su scalari del contesto (§3.5).
5. `BlockDocumentWalker` (design §5.3, puro `JsonNode`, nessuna dipendenza dai blocchi) + `BlockDocumentWalkerTests` — serve qui perché `Content` proietta il testo; `IProjectable`, `ProjectionSnapshot`, `ProjectionWriter` (upsert/delete per `source_module+source_id`, una riga per lingua di `division.locales`), `Link` e `Content` implementano `IProjectable`.
6. `PermissionRequirement`, `HubPolicyProvider`, `DepartmentAuthorizationHandler` (§3.7) — l'unico; `PolicyNamesTests`; `ArchitectureTests` (riferimenti tra progetti, unico handler).
7. Test integrazione (design §8): `InterceptorFillsAuditAndTimestamps`, `InterceptorBlocksCrossDepartmentWrite`, `AuditLogWritten`, `ProjectionUpsertedInSameTransaction` (incluso rollback), `DraftContentIsNotProjected`, `VisibilityFilterPerRole`, `AuthorizationHandlerIsTheOnlyOne`.

Accettazione: tutti i test sopra verdi; nessun endpoint ancora (i test usano il DbContext e `IAuthorizationService` direttamente).

Non fare: endpoint, frontend.

**Chiusa il 3 settembre 2026** (PR #6), con due correzioni al design confermate da Carmine:
`IProjectable.Project()` riceve un `ProjectionContext` (§3.6) e `ICurrentUser` espone due domande
separate, `Has(permission, department)` e `HasAny(permission)` (§3.3, §3.7). Note in
`docs/internal/decisions/`. `LocaleCatalog` è passato a F5.

### F5 — `MapCrud` e `links` (server)

**Obiettivo**: il CRUD di un'entità costa una configurazione, non codice.

Task:
1. `MapCrud` (design §3.9) in `Core/Data/Crud/`: modalità dipartimentale e globale, `ReadPolicy`/`WritePolicy`/`ReadOnly`, paginazione, `sort`/`dir` allow-list, `q` su `SearchFields` (per `Localized` usa `JSON_EXTRACT(col, '$.<locale>')` via `EF.Functions.JsonExtract` di Pomelo o `JsonValue` SQL raw parametrizzato, in **un** helper `LocalizedQuery`), `filter[...]`, filtro di dipartimento sulla lista, `AuthorizeAsync` sulla risorsa, `ValidationProblem`, 409 su concorrenza, `ExtraWritePolicy`, `PagedResult<T>`. Test «`IgnoreQueryFilters` compare solo in `Core/Data/Crud/`».
2. DTO `LinkListDto`, `LinkDetailDto`, `LinkWriteDto` + mapper Mapperly + `LinkWriteDtoValidator` (titolo `LocalizedRules.Required`, `Url` assoluta http/https, `Sort ≥ 0`).
3. `app.MapCrud<Link, …>("/api/links", o => { o.PermissionArea = "Links"; … })` in `Core/Content/LinksEndpoints.cs` (estensione chiamata da `Program`), `AddOpenApi` con transformer `x-localized` + Scalar in dev, `ProblemDetails` globali (`AddProblemDetails` + `IExceptionHandler` per `ForbiddenDomainException` → 403, `DbUpdateConcurrencyException` → 409).
4. OpenAPI a build-time con `Microsoft.Extensions.ApiDescription.Server` → `artifacts/openapi/IvaoHub.Web.json`; `pnpm gen:api` (`openapi-typescript` su quel file) → `web/src/shared/api/schema.d.ts`; step CI «build, genera, `git diff --exit-code`».
5. `LocaleCatalog` (design §1 e §7.6): legge `locales/{lang}/*.json` e li rende al backend, che ne ha bisogno per i `ProblemDetails` di `MapCrud` (le chiavi `errors.*` che il punto 1 produce) e, in M1, per le mail. **Spostato qui da F4**: il perimetro di F4 non lo elencava e prima di `MapCrud` nessuno aveva un messaggio da tradurre lato server. Con lui va sistemato anche il pacchetto pubblicato: le lingue **ci sono già dentro `wwwroot/locales/`** (le emette il plugin `divisionLocales` di Vite, ed è da lì che la SPA le carica), ma **non** alla radice, che è dove guarda `HubPaths.Locales`; e i `config/*.example.json` non ci sono affatto, quindi chi scompatta il pacchetto non trova il file da copiare. Un target MSBuild accanto a `PublishSpa` risolve entrambe, e nello stesso giro mette nel pacchetto anche `LICENSE` e `NOTICE`: Apache-2.0 §4(d) chiede che il `NOTICE` viaggi con ogni ridistribuzione, e oggi il pacchetto non lo porta.
6. Test integrazione `MapCrudLinksEndToEnd` (design §8) con utenti finti: superadmin, staff ED coordinator, staff FOD advisor, membro, anonimo.

Accettazione: `curl` autenticato (cookie di test) fa list/create/update/delete su `/api/links`; `schema.d.ts` generato e committato; `locales/`, gli `.example.json`, `LICENSE` e `NOTICE` presenti in `artifacts/publish`; test verdi.

**Chiusa il 3 settembre 2026**, con 244 test verdi (194 unit + 50 integrazione) e due precisazioni al
design da confermare: l'OpenAPI a build-time **esegue** l'entry point fino a `app.Run()` (§7.4 e §9
punto 12 vanno riformulate) e un campo dichiarato `Localized<T>?` e non valorizzato viaggia `null`
(§3.1 va precisata). Note in `docs/internal/decisions/`. Tre correzioni di rotta minori, nella
tabella delle scelte di `HANDOFF.md`: `CrudOptions` ha un `ContextType`, `SearchFields` è una
collezione con `Add` sovraccaricato perché una colonna tradotta non è una `string?`, e i parametri
di lista sono il record `CrudListRequest` perché letti dalla query string non finivano nel contratto.
Due bug reali trovati dai test: le policy dei permessi non dichiaravano lo schema del cookie e una
chiamata anonima a `/api` prendeva 302 invece di 401; il converter JSON di `Localized<T>` lanciava
su un riferimento nullo.

### F6 — Spina dorsale frontend

**Obiettivo**: layout, routing, motore lista+form, i18n, back-office `links` generato, ui-kit, regole UI.

Task:
1. Layout `_public`, `_member`, `_staff` (design §7.2) con Navbar/NavigationMenu/Sidebar Atmosphere, footer legale HQ (link da `locales`, non hardcoded), `DarkModeToggle`, `LocaleSwitcher` (cookie `hub.lang` + `PUT /api/me/locale` se autenticato — endpoint piccolo in `Core/Auth`), `NotFound`, `Forbidden`.
2. Root context `{ queryClient, bootstrap }`, ricette router 1–3 (design §7.3) implementate e documentate in `web/src/routes/README.md` (EN); `routeTree.gen.ts` in git; helper `deptParam` (`shared/api/department.ts`) per URL minuscolo ↔ enum API.
3. `shared/api`: client con header CSRF, middleware 401, convenzione `queries.ts`/`mutations.ts`.
4. `shared/forms`: `SchemaForm` (walk zod 4 con `.meta`), `localized()` helper, `LocaleFields`, `useProblemDetails`, `ProblemAlert`. `shared/list`: `DataList` su `DataTable` Atmosphere con search params della route, helper `col.*`. Test Vitest per ogni tipo di campo e per il mapping dei `ProblemDetails`.
5. Componenti custom dell'elenco chiuso di M0 (design §7.1) in `shared/ui/`, con dark mode.
6. Feature `links`: `features/links/{schema.ts,list.ts,queries.ts,mutations.ts}` + route `/staff/$dept/links` (lista) e `/staff/$dept/links/$id` (form) — **zero** JSX di tabella o form scritto a mano: solo configurazione. Sidebar staff con i dipartimenti dell'utente (tutti per Director/Web/superadmin) e voce `links`.
7. Route `/staff/admin/ui-kit` (richiede `Admin.Access`) con ogni componente e ogni blocco del registry (il registry esiste vuoto in F6, si riempie in F7) + test «registry ⇄ ui-kit» (in F8 diventa «server ⇄ manifest ⇄ ui-kit»). Tipo `ModuleManifest` in `shared/modules.ts` e caricatore dei manifest in `app/` (design §6.5), ancora senza moduli.
8. `docs/UI-GUIDELINES.md` (EN, design §7.1); regole ESLint attive dalla F0 verificate.
9. `pnpm i18n:check` esteso: chiavi statiche usate nel codice esistono in tutte le lingue.

Accettazione: staff ED vede solo `/staff/ed/links`, crea/modifica un link con titolo in due lingue, errore server mostrato sul campo giusto; superadmin vede tutti i dipartimenti; ui-kit mostra tutti i componenti; Vitest e `i18n:check` verdi.

Non fare: editor dei contenuti, blocchi.

**Chiusa il 3 set 2026.** Una decisione e tre precisazioni al design (v1.5):

1. **`react-markdown`** per `MarkdownContent`: il design lo mette nell'elenco chiuso ma §0.3 non pinnava nessun renderer. Decisa da Carmine; nota in `docs/internal/decisions/2026-09-03-markdown-content.md`.
2. **`DataList` prende `search` e `onSearchChange`**, non l'oggetto `route`: farlo entrare nel router significherebbe riallargare i search params a `unknown` e buttare via la tipizzazione della ricetta 2. Le due righe di collegamento stanno nel file di route; nessuna riga di JSX di tabella. Design §7.5 e `web/src/routes/README.md`.
3. **Il bootstrap dichiara `hasAllDepartments`** (design §3.10): la sidebar deve elencare tutti i dipartimenti a director/web/superadmin, e §3 dell'HANDOFF vieta di dedurlo dalla forma della lista dei permessi.
4. **`HubPolicies.SignedIn`** (design §3.7): `PUT /api/me/locale` chiede solo di essere autenticati, che non è un permesso del catalogo. Una policy sola, registrata una volta.

Fuori perimetro dichiarato: `col.department()` esiste come helper ma la lista dei link non lo usa, perché la route filtra già su un dipartimento solo e la colonna direbbe la stessa cosa a ogni riga. Il campo `ownerDepartment` del form è `hidden` e viene dal path: spostare un link fra dipartimenti non è nell'accettazione di F6 e il server lo rifiuterebbe comunque a chi non ha il permesso su entrambi.

### F7 — Contenuti: entità, envelope, publish, blocchi, editor, template

**Obiettivo**: il primo `cms_contents` creato da template, modificato, pubblicato e reso.

Task (server):
1. `BlockDocumentWalker.ValidateEnvelope` completo (il walker nasce in F4; qui si aggiungono le regole legate al registry) + test.
2. `IBlockDescriptor`/`BlockRegistry` (nucleo registra i 5 blocchi di §5.4), `IDataBlockProvider` + `LinkListProvider`, `GET /api/blocks/data/{type}`.
3. `MapCrud` per `Content` su `/api/content` (area `Content`, `ExtraWritePolicy` per `IsTemplate` → `Content.ManageTemplates`, validazione envelope nel validator del `ContentWriteDto` tramite il walker, `Source` che esclude i template dalla lista normale e li mostra con `filter[isTemplate]=true`).
4. `ContentPublishService` + `POST /api/content/{id}/publish` (design §5.5), `POST /api/content?templateId=` (copia profonda), `GET /api/content/public/{kind}/{slug}` (solo versione pubblicata, query filter).
5. Seed dei template di sistema da `seed/content-templates/*.json` (design §5.6) con testi in tutte le lingue della divisione presenti in `locales` (i seed contengono chiavi i18n risolte al seed, così una divisione `XX` in `en` non vede italiano).
6. Test integrazione: `ContentPublishFreezesDataBlocks`, `PublicReadsOnlyPublishedVersion`, `NewFromTemplateDeepCopies`, `TemplateEditRequiresManageTemplates`, `EnvelopeValidationRejectsUnknownBlockAndDepth`, `PublishRejectsMissingLocales`.

Task (frontend):
7. `web/src/blocks/`: schema zod dell'envelope (`sectionSchema`, `blockEnvelopeSchema`), `registry.ts`, i 5 blocchi (schema + componente + label), `MarkdownContent` con `rehype-sanitize`, `ContentRenderer` (sezioni con `layout`, colonne, sfondo/padding/larghezza con classi Atmosphere; blocchi Data: `frozen` se presente altrimenti query live), avviso «blocco sconosciuto» solo per staff.
8. Editor a lista (design §7.7) su `/staff/$dept/content/$id` + lista `/staff/$dept/content` (via `DataList`, filtro `kind`, pulsante «Nuovo da template» con select dei template) + metadati con `SchemaForm`; Pubblica con dialogo che mostra gli errori di lingua per percorso.
9. Route pubblica `/_public/$slug` (kind `page`) con `ContentRenderer`.
10. ui-kit aggiornata con i 5 blocchi; test Vitest degli schemi dei blocchi e del registry.

Accettazione (la demo di M0): superadmin crea «Test page» da `section-page`, compila hero (it/en), aggiunge un `text` e un `linkList` frozen su categoria «software», pubblica; anonimo apre `/test-page` e vede i link catturati; si aggiunge un link nella categoria → la pagina pubblica **non** cambia; si cambia il blocco in `live` e si ripubblica → cambia. Bozza non visibile agli anonimi. Test verdi.

### F8 — Moduli, admin, manutenzione, ricerca, forkabilità

**Obiettivo**: il nucleo compone i contributi dei moduli; le schermate admin minime esistono; il test XX passa.

Task:
1. `IModule`, `ModuleRegistry` alimentato dall'elenco esplicito `IvaoHub.Web/Modules.cs`, `AddModuleDbContext<T>`, `ModuleMaintenanceMiddleware`, `SpaFallbackExclusions` che sostituisce l'elenco cablato di F0; `/api/me` compone `modules`, `navigation`, `registries` da nucleo + moduli.
2. `IvaoHub.Modules.Atc` (design §6.4) reale, backend **e** `web/src/modules/atc/` con manifest, route `/atc`, namespace i18n `atc`; riga in `web/src/modules/index.ts`; script di build che copia `modules/*/locales` in `locales/`; `ArchitectureTests` esteso; test «server ⇄ manifest ⇄ ui-kit»; `docs/FORKING.md` con la sezione «Adding a module» (progetto + cartella + due righe negli elenchi).
3. `/staff/admin/permissions`: `MapCrud` in **modalità globale** su `UserGrant` (`/api/admin/grants`, `ReadPolicy = WritePolicy = Permissions.Manage`, validator che impone «solo a VID staff» e «mai globali») + `DataList`/`SchemaForm`; sezione superadmin (add/remove via `SuperadminService`, endpoint `/api/admin/superadmins`) visibile solo ai superadmin. Rigenerazione del `security_stamp` dell'utente toccato + `ISecurityStampCache.Invalidate(vid)`.
4. `/staff/admin/modules`: lista moduli con toggle `maintenance` (`PUT /api/admin/modules/{key}/maintenance`, `Modules.Manage`, audit).
5. `/staff/admin/audit`: `DataList` in sola lettura su `hub_audit_log` (`MapCrud` modalità globale, `ReadOnly = true`, `ReadPolicy = Audit.View`).
6. `GET /api/search?q=` sul `search_index` (riga della lingua corrente, `MATCH … AGAINST` in un helper di `Core/Data`; `SearchIndexEntry` implementa `IVisible`+`IOwnedByDepartment` così il **global query filter** di F4 si applica da solo) — solo endpoint + test, niente UI.
7. Dashboard `/me` che compone i widget del registry (`welcome`).
8. **`ForkabilityXxDivision`** (design §8) + fixture `config/division.xx.json` per i test; script `pnpm i18n:check` include `errors` e `mail`.
9. Test: `ModuleRegistryComposesNavAndExclusions`, `MaintenanceReturns503OnWrites`, `GrantsEndpointEnforcesStaffOnly`, `SearchRespectsVisibility`, `ForkabilityXxDivision`.

Accettazione: `/api/me` mostra `atc` e `nav.atc` e la SPA rende `/atc` dal manifest del modulo; un `import` da `features/` a `modules/atc/` fa fallire il lint; `/services/vsop/x` non è intercettato dalla SPA (404 del backend); manutenzione su `atc` → `POST /api/atc/…` 503, `GET` passa; grant `Links.Edit` su FOD a uno staff ED → può modificare i link FOD **subito** (stamp), rimozione → 403; test XX verde.

### F9 — Chiusura di M0

Task:
1. `docs/internal/HANDOFF.md` definitivo: come avviare, credenziali (dove stanno, non quali), cosa c'è, cosa manca per M1, debiti noti.
2. Script `tools/demo-m0.md` (EN) con i passi della demo di F7/F8 e la checklist «definizione di fatto» del design §0.1 spuntata.
3. Revisione della checklist §16.E su tutto il codice di M0: elenco di ogni eccezione con motivazione in `docs/internal/decisions/2026-XX-XX-m0-review.md`.
4. Aggiornamento del piano 00 (versione + changelog) con le decisioni emerse nelle fasi; `FORKING.md` con i passi reali (division.json, locales, oauth, seed).
5. Tag `v0.1.0-m0`, release CI con artefatto.

Accettazione: Carmine esegue `tools/demo-m0.md` da zero (clone, docker-compose, run) e ogni punto passa.

**Fatta il 4 settembre 2026.** I cinque task sono chiusi; il tag lo spinge Carmine dopo il merge, e
il comando esatto è nell'HANDOFF. Playwright **non** è entrato in F9: non è fra i cinque task, il
design §8 lo dichiara non bloccante in M0, e la demo end-to-end che il piano chiede è
`tools/demo-m0.md`, eseguita a mano. Deciso da Carmine all'apertura della fase; resta la prima voce
del backlog di M1.

La revisione §16.E (task 3) ha trovato **tre stringhe visibili all'utente nel codice** — un
`aria-label` inglese letto solo da uno screen reader, un `placeholder` di esempio, e il dominio di
questa divisione dentro il messaggio con cui l'app si rifiuta di partire senza `AllowedHosts` — e le
ha chiuse, che è quanto la regola (a) prescrive. Sono l'unica modifica al codice di produzione che
F9 contiene. Tutto il resto della revisione è nella nota.

---

## E. Rischi specifici dell'implementazione e come Claude Code deve reagire

| Situazione | Reazione attesa |
|---|---|
| Un componente Atmosphere non fa ciò che serve (es. `DataTable` server-side) | Wrappare in `shared/ui` restando nell'elenco chiuso; se serve un componente nuovo → decisione (c). |
| Pomelo/EF 9 non supporta un costrutto (JSON path, FULLTEXT) | SQL raw parametrizzato in **un solo** helper di `Core/Data`; mai sparso negli endpoint. |
| L'API IVAO di test non copre `centers`/`airports` | `Ivao:UseFixtures=true`; segnalare a Carmine cosa manca, non bloccare la fase. |
| Il generatore di form non copre un tipo zod | Estendere `SchemaForm` (b), non scrivere il form a mano. |
| Serve un permesso non nel catalogo | Aggiungerlo al catalogo e alla matrice (a), con test della matrice aggiornato. |
| Serve una colonna/tabella | Migrazione **additiva** nuova, mai modificare `Initial` dopo il merge di F1. |
| Tentazione di «un piccolo `fetch`» o «un handler solo per questo caso» | No: è esattamente ciò che la checklist della PR fa emergere. |
| Un test della spina dorsale «dà fastidio» | Non si skippa: si corregge il codice o si ferma la fase con una nota di decisione. |
